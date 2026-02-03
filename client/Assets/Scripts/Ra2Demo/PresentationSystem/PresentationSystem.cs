using System.Collections.Generic;
using UnityEngine;
using ZFrame;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using ZLockstep.Sync;
using ZLockstep.View.Components;
using static ZLockstep.Simulation.ECS.Systems.SettlementSystem;

namespace ZLockstep.View.Systems
{
    /// <summary>
    /// 表现层系统
    /// 负责处理所有与Unity显示相关的逻辑
    /// 
    /// 职责：
    /// 1. 监听逻辑层事件并创建/销毁视图
    /// 2. 同步逻辑位置到Unity Transform
    /// 3. 处理动画播放
    /// 4. 管理UI显示
    /// 5. 管理单位描边效果
    /// </summary>
    public class PresentationSystem : PresentationBaseSystem
    {
        // Unity资源
        private Transform _viewRoot;
        private Dictionary<int, GameObject> _unitPrefabs;

        // 存储正在建造的建筑的建造模型
        private Dictionary<int, GameObject> _constructionModels = new Dictionary<int, GameObject>();

        /// <summary>
        /// 是否启用平滑插值（在FixedUpdate之间）
        /// </summary>
        public bool EnableSmoothInterpolation { get; set; } = false;

        /// <summary>
        /// 是否启用表现系统（追帧时可以禁用以提升性能）
        /// </summary>
        public bool Enabled { get; set; } = true;

        // 添加对Game对象的引用
        private ZLockstep.Sync.Game _game;

        /// <summary>
        /// 初始化系统（由GameWorldBridge调用）
        /// </summary>
        public void Initialize(Transform viewRoot, Dictionary<int, GameObject> prefabs)
        {
            _viewRoot = viewRoot;
            _unitPrefabs = prefabs;
        }

        /// <summary>
        /// 销毁所有已创建的视图对象
        /// </summary>
        public void DestroyAllViews()
        {
            if (_viewRoot == null)
                return;

            // 销毁所有子对象（游戏对象）
            for (int i = _viewRoot.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_viewRoot.GetChild(i).gameObject);
            }
            
            // 清理死亡实体列表
            _dyingEntities.Clear();
            
            // 清理建造模型
            _constructionModels.Clear();
        }

        /// <summary>
        /// 设置Game对象引用（由GameWorldBridge调用）
        /// </summary>
        public void SetGame(ZLockstep.Sync.Game game)
        {
            _game = game;
        }

        public override void Update()
        {
            // 1. 处理单位创建事件
            ProcessCreationEvents();

            // 2. 处理死亡事件（播放死亡动画）
            ProcessEntityDiedEvents();

            // 3. 检查死亡动画状态
            CheckDyingEntities();

            // 4. 处理经济变化事件
            ProcessEconomyEvents();

            // 5. 处理游戏结束事件
            ProcessGameOverEvents();

            // 6. 同步已有的View
            SyncAllViews();

            // 7. 消息提示
            ProcessShowMessage();
        }

        private void ProcessShowMessage()
        {
            var events = EventManager.GetEvents<MessageEvent>();
            foreach (var evt in events)
            {
                // 显示消息提示
                Debug.Log($"[PresentationSystem] 显示消息：{evt.Message}");
                Frame.DispatchEvent(new ShowMessageEvent(evt.Message));
            }
        }


        /// <summary>
        /// 处理经济变化事件（资金和电力变化）
        /// </summary>
        private void ProcessEconomyEvents()
        {
            // 处理资金变化事件
            var moneyEvents = EventManager.GetEvents<MoneyChangedEvent>();
            foreach (var evt in moneyEvents)
            {
                // 这里可以添加处理资金变化的逻辑
                // 例如：更新UI上的资金显示
                // Debug.Log($"[PresentationSystem] 阵营{evt.CampId}资金变化: {evt.OldMoney} -> {evt.NewMoney} ({evt.Reason})");
                Frame.DispatchEvent(new EconomyEvent());
            }

            // 处理电力变化事件
            var powerEvents = EventManager.GetEvents<PowerChangedEvent>();
            foreach (var evt in powerEvents)
            {
                // 这里可以添加处理电力变化的逻辑
                // 例如：更新UI上的电力显示
                Debug.Log($"[PresentationSystem] 阵营{evt.CampId}电力变化: {evt.OldPower} -> {evt.NewPower} ({evt.Reason})");
            }
        }

        /// <summary>
        /// 处理实体死亡事件
        /// </summary>
        private void ProcessEntityDiedEvents()
        {
            var events = EventManager.GetEvents<EntityDiedEvent>();
            foreach (var evt in events)
            {
                var entity = new Entity(evt.EntityId);

                // 检查是否有ViewComponent
                if (!ComponentManager.HasComponent<ViewComponent>(entity))
                    continue;

                var viewComponent = ComponentManager.GetComponent<ViewComponent>(entity);

                // 触发死亡动画
                if (viewComponent.Animator != null)
                {
                    viewComponent.Animator.SetTrigger("Death");
                    Debug.Log($"[PresentationSystem] 实体{evt.EntityId}触发死亡动画");
                }
                else
                {
                    Debug.Log($"[PresentationSystem] 实体{evt.EntityId}死亡，但没有Animator组件");
                }
                
                // 添加到死亡列表，用于后续跟踪动画状态
                _dyingEntities.Add((evt.EntityId, viewComponent.Animator));
                
                // 移除建造模型（如果存在）
                if (_constructionModels.ContainsKey(evt.EntityId))
                {
                    var constructionModel = _constructionModels[evt.EntityId];
                    if (constructionModel != null)
                    {
                        Object.Destroy(constructionModel);
                    }
                    _constructionModels.Remove(evt.EntityId);
                }
            }
        }

        /// <summary>
        /// 检查正在播放死亡动画的实体
        /// 当动画播放完毕后，移除对应的视图组件
        /// </summary>
        private void CheckDyingEntities()
        {
            for (int i = _dyingEntities.Count - 1; i >= 0; i--)
            {
                var (entityId, animator) = _dyingEntities[i];
                Entity entity = new Entity(entityId);

                // 检查动画是否播放完毕
                if (IsDeathAnimationFinished(animator))
                {
                    // 动画播放完毕，移除ViewComponent
                    if (ComponentManager.HasComponent<ViewComponent>(entity))
                    {
                        var viewComponent = ComponentManager.GetComponent<ViewComponent>(entity);

                        // 销毁GameObject
                        if (viewComponent.GameObject != null)
                        {
                            Object.Destroy(viewComponent.GameObject);
                        }

                        // 移除ViewComponent
                        ComponentManager.RemoveComponent<ViewComponent>(entity);
                        Debug.Log($"[PresentationSystem] 实体{entityId}死亡动画播放完毕，已移除视图");
                    }

                    // 从死亡列表中移除
                    _dyingEntities.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 判断死亡动画是否播放完毕
        /// </summary>
        private bool IsDeathAnimationFinished(Animator animator)
        {
            // 检查Animator是否仍然有效
            if (animator == null || !animator.gameObject.activeInHierarchy)
                return true;

            // 获取当前动画状态
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 检查是否是死亡动画状态（需要根据实际动画状态名称调整）
            if (stateInfo.IsName("Death"))
            {
                // normalizedTime大于等于1.0表示动画已经播放完成
                return stateInfo.normalizedTime >= 1.0f;
            }

            // 如果不是死亡状态，可能动画已经切换，也可以认为完成
            return true;
        }

        /// <summary>
        /// 处理游戏结束事件
        /// </summary>
        private void ProcessGameOverEvents()
        {
            var events = EventManager.GetEvents<GameOverEvent>();
            foreach (var evt in events)
            {
                // 触发结算事件
                Frame.DispatchEvent(new SettleEvent(evt.IsVictory));
            }
        }

        /// <summary>
        /// 处理单位创建事件
        /// </summary>
        private void ProcessCreationEvents()
        {
            var events = EventManager.GetEvents<UnitCreatedEvent>();
            foreach (var evt in events)
            {
                CreateViewForEntity(evt);
            }
        }

        /// <summary>
        /// 为实体创建Unity视图
        /// </summary>
        private void CreateViewForEntity(UnitCreatedEvent evt)
        {
            GameObject viewObject = null;

            if (evt.ConfID > 0)
            {
                ConfBuilding confBuilding = DataManager.Get<ConfBuilding>(evt.ConfID);
                if (confBuilding != null)
                {
                    // 创建建筑模型
                    GameObject buildingPrefab = ResourceCache.GetPrefab("Prefabs/" + confBuilding.Prefab);
                    if (buildingPrefab != null)
                    {
                        // 创建建筑模型
                        viewObject = Object.Instantiate(buildingPrefab, _viewRoot);
                    }
                }
            }

            if (viewObject == null)
            {
                // 尝试获取预制体
                if (_unitPrefabs.TryGetValue(evt.PrefabId, out var prefab))
                {
                    // 实例化预制体
                    viewObject = Object.Instantiate(prefab, _viewRoot);
                }
                else
                {
                    // 找不到预制体，根据类型创建默认可视化
                    if (evt.UnitType == (int)UnitType.Projectile) // 100=弹道类型
                    {
                        viewObject = CreateDefaultProjectileView(evt);
                    }
                    else
                    {
                        Debug.LogWarning($"[PresentationSystem] 找不到预制体: UnitType={evt.UnitType}, PrefabId={evt.PrefabId}");
                        return;
                    }
                }
            }

            if (viewObject == null)
                return;

            viewObject.name = $"Unit_{evt.EntityId}_Type{evt.UnitType}_P{evt.PlayerId}";
            viewObject.transform.position = evt.Position.ToVector3();

            // 检查实体是否是正在建造的建筑
            // 如果是，则在上方额外显示预制体ID 8（建造模型）
            if (_game != null && _game.World != null)
            {
                var entity = new Entity(evt.EntityId);
                if (_game.World.ComponentManager.HasComponent<BuildingConstructionComponent>(entity))
                {
                    // 在当前预制体上方显示建造模型
                    ShowConstructionModel(viewObject, entity);
                }
            }

            // 添加描边组件（默认不启用）
            try 
            {
                var outlineComponent = viewObject.AddComponent<OutlineComponent>();
                outlineComponent.enabled = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PresentationSystem] 无法为对象 {viewObject.name} 添加描边组件: {ex.Message}");
            }

            // 创建并添加ViewComponent
            var entityForView = new Entity(evt.EntityId);
            var viewComponent = ViewComponent.Create(viewObject, EnableSmoothInterpolation);
            ComponentManager.AddComponent(entityForView, viewComponent);

            // 添加玩家单位指示器组件（弹道不需要）
            if (evt.UnitType != 100)
            {
                var indicator = viewObject.AddComponent<PlayerUnitIndicator>();
                indicator.Initialize(evt.PlayerId);
            }

            Debug.Log($"[PresentationSystem] 为Entity_{evt.EntityId}创建了视图: {viewObject.name}, position:{viewObject.transform.position}");
        }

        /// <summary>
        /// 创建默认的弹道可视化
        /// </summary>
        private GameObject CreateDefaultProjectileView(UnitCreatedEvent evt)
        {
            // 创建弹道根节点
            GameObject projectile = new GameObject($"Projectile_{evt.EntityId}");
            projectile.transform.SetParent(_viewRoot);
            projectile.transform.position = evt.Position.ToVector3();

            // 添加球体作为弹头
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ProjectileHead";
            sphere.transform.SetParent(projectile.transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.3f; // 0.3米直径

            // 设置颜色（根据阵营）
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = evt.PlayerId == 0 ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f); // 蓝/红
                material.SetFloat("_Metallic", 0.5f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color * 2f);
                renderer.material = material;
            }

            // 添加拖尾效果
            var trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.5f; // 拖尾持续0.5秒
            trail.startWidth = 0.2f;
            trail.endWidth = 0.05f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = evt.PlayerId == 0 ? new Color(0.2f, 0.5f, 1f, 1f) : new Color(1f, 0.3f, 0.3f, 1f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);

            Debug.Log($"[PresentationSystem] 创建了默认弹道可视化: Entity_{evt.EntityId}");

            return projectile;
        }

        /// <summary>
        /// 为缺失视图的实体创建 GameObject
        /// </summary>
        private void CreateViewForMissingEntity(Entity entity)
        {
            var unit = ComponentManager.GetComponent<UnitComponent>(entity);
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);

            // 获取预制体
            if (!_unitPrefabs.TryGetValue(unit.PrefabId, out var prefab))
            {
                Debug.LogWarning($"[PresentationSystem] 找不到预制体: UnitType={unit.UnitType}");
                return;
            }

            // 实例化GameObject
            GameObject viewObject = Object.Instantiate(prefab, _viewRoot);
            viewObject.name = $"Unit_{entity.Id}_Type{unit.UnitType}_P{unit.PlayerId}_Resynced";
            viewObject.transform.position = transform.Position.ToVector3();
            viewObject.transform.rotation = transform.Rotation.ToQuaternion();
            viewObject.transform.localScale = transform.Scale.ToVector3();

            // 检查实体是否是正在建造的建筑
            // 如果是，则在上方额外显示预制体ID 8（建造模型）
            if (ComponentManager.HasComponent<BuildingConstructionComponent>(entity))
            {
                // 在当前预制体上方显示建造模型
                ShowConstructionModel(viewObject, entity);
            }

            // 添加描边组件（默认不启用）
            try 
            {
                var outlineComponent = viewObject.AddComponent<OutlineComponent>();
                outlineComponent.enabled = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PresentationSystem] 无法为对象 {viewObject.name} 添加描边组件: {ex.Message}");
            }

            // 创建并添加ViewComponent
            var viewComponent = ViewComponent.Create(viewObject, EnableSmoothInterpolation);
            ComponentManager.AddComponent(entity, viewComponent);

            Debug.Log($"[PresentationSystem] 重新创建视图: Entity_{entity.Id}");
        }

        /// <summary>
        /// 同步所有View实体
        /// </summary>
        private void SyncAllViews()
        {
            var viewEntities = ComponentManager.GetAllEntityIdsWith<ViewComponent>();

            foreach (var entityId in viewEntities)
            {
                var entity = new Entity(entityId);

                // 只同步有Transform组件的实体
                if (!ComponentManager.HasComponent<TransformComponent>(entity))
                    continue;

                SyncEntityToView(entity);
            }
        }

        /// <summary>
        /// 重新同步所有实体（追帧完成后调用）
        /// 用于处理追帧期间禁用渲染导致的视图缺失
        /// </summary>
        public void ResyncAllEntities()
        {
            if (_viewRoot == null || _unitPrefabs == null)
            {
                Debug.LogWarning("[PresentationSystem] ResyncAllEntities: 未初始化");
                return;
            }

            int createdCount = 0;
            int syncedCount = 0;

            // 获取所有逻辑实体
            var allEntities = ComponentManager.GetAllEntityIdsWith<TransformComponent>();

            foreach (var entityId in allEntities)
            {
                var entity = new Entity(entityId);

                // 检查是否已有 ViewComponent
                if (ComponentManager.HasComponent<ViewComponent>(entity))
                {
                    // 已有视图，强制同步位置
                    var view = ComponentManager.GetComponent<ViewComponent>(entity);
                    var transform = ComponentManager.GetComponent<TransformComponent>(entity);

                    if (view.Transform != null)
                    {
                        view.Transform.position = transform.Position.ToVector3();
                        view.Transform.rotation = transform.Rotation.ToQuaternion();
                        view.Transform.localScale = transform.Scale.ToVector3();
                        syncedCount++;
                    }
                }
                else
                {
                    // 没有视图，需要创建
                    if (ComponentManager.HasComponent<UnitComponent>(entity))
                    {
                        CreateViewForMissingEntity(entity);
                        createdCount++;
                    }
                }
            }

            Debug.Log($"[PresentationSystem] ResyncAllEntities 完成: 新建={createdCount}, 同步={syncedCount}");
        }

        /// <summary>
        /// 将逻辑实体的数据同步到Unity GameObject
        /// </summary>
        private void SyncEntityToView(Entity entity)
        {
            var view = ComponentManager.GetComponent<ViewComponent>(entity);
            var logicTransform = ComponentManager.GetComponent<TransformComponent>(entity);

            if (view.GameObject == null || view.Transform == null)
                return;

            // 检查是否是正在建造的建筑
            if (ComponentManager.HasComponent<BuildingConstructionComponent>(entity))
            {
                // 确保建造模型存在
                if (!_constructionModels.ContainsKey(entity.Id))
                {
                    ShowConstructionModel(view.GameObject, entity);
                }
                else
                {
                    // 更新建造模型的位置
                    var constructionModel = _constructionModels[entity.Id];
                    if (constructionModel != null)
                    {
                        constructionModel.transform.position = logicTransform.Position.ToVector3() + Vector3.up * 2; // 在上方显示
                    }
                }
            }
            else
            {
                // 建筑已经建造完成，移除建造模型
                if (_constructionModels.ContainsKey(entity.Id))
                {
                    var constructionModel = _constructionModels[entity.Id];
                    if (constructionModel != null)
                    {
                        Object.Destroy(constructionModel);
                    }
                    _constructionModels.Remove(entity.Id);
                }
            }

            // 记录上一帧位置（用于插值）
            if (view.EnableInterpolation)
            {
                view.LastLogicPosition = view.Transform.position;
                view.LastLogicRotation = view.Transform.rotation;
            }

            // 直接同步（无插值）
            view.Transform.position = logicTransform.Position.ToVector3();
            view.Transform.rotation = logicTransform.Rotation.ToQuaternion();
            view.Transform.localScale = logicTransform.Scale.ToVector3();

            // 同步动画状态
            SyncAnimationState(entity, view);

            // 写回ViewComponent（如果有修改）
            ComponentManager.AddComponent(entity, view);
        }

        /// <summary>
        /// 同步动画状态
        /// </summary>
        private void SyncAnimationState(Entity entity, ViewComponent view)
        {
            if (view.Animator == null)
                return;

            // 根据速度设置移动动画
            if (ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                var velocity = ComponentManager.GetComponent<VelocityComponent>(entity);
                bool isMoving = velocity.IsMoving;
                float speed = velocity.SqrMagnitude.ToFloat();

                // view.Animator.SetBool("Move", isMoving);
                view.Animator.SetInteger("speed", (int)speed);
            }

            // 根据生命值设置死亡动画
            if (ComponentManager.HasComponent<HealthComponent>(entity))
            {
                var health = ComponentManager.GetComponent<HealthComponent>(entity);
                if (health.IsDead)
                {
                    view.Animator.SetTrigger("Death");
                }
            }

            // 根据攻击组件设置攻击动画
            if (ComponentManager.HasComponent<AttackComponent>(entity))
            {
                var attack = ComponentManager.GetComponent<AttackComponent>(entity);
                if (attack.HasTarget)
                {
                    view.Animator.SetTrigger("Fire");
                }
                else
                {
                    // view.Animator.SetBool("Fire", false);
                }
            }
        }

        /// <summary>
        /// Unity的Update中调用，用于平滑插值（可选）
        /// </summary>
        public void LerpUpdate(float deltaTime, float interpolationSpeed = 10f)
        {
            if (!Enabled || !EnableSmoothInterpolation)
                return;

            var viewEntities = ComponentManager.GetAllEntityIdsWith<ViewComponent>();

            foreach (var entityId in viewEntities)
            {
                var entity = new Entity(entityId);

                if (!ComponentManager.HasComponent<TransformComponent>(entity))
                    continue;

                var view = ComponentManager.GetComponent<ViewComponent>(entity);
                var logicTransform = ComponentManager.GetComponent<TransformComponent>(entity);

                if (view.GameObject == null || view.Transform == null || !view.EnableInterpolation)
                    continue;

                // 插值到目标位置
                Vector3 targetPos = logicTransform.Position.ToVector3();
                Quaternion targetRot = logicTransform.Rotation.ToQuaternion();

                view.Transform.position = Vector3.Lerp(
                    view.Transform.position,
                    targetPos,
                    deltaTime * interpolationSpeed
                );

                view.Transform.rotation = Quaternion.Lerp(
                    view.Transform.rotation,
                    targetRot,
                    deltaTime * interpolationSpeed
                );
            }
        }

        /// <summary>
        /// 启用或禁用实体的描边效果
        /// </summary>
        /// <param name="entityId">实体ID</param>
        /// <param name="enable">是否启用描边</param>
        public void EnableEntityOutline(int entityId, bool enable)
        {
            var entity = new Entity(entityId);
            if (!ComponentManager.HasComponent<ViewComponent>(entity))
                return;

            var viewComponent = ComponentManager.GetComponent<ViewComponent>(entity);
            if (viewComponent.GameObject == null)
                return;

            var outlineComponent = viewComponent.GameObject.GetComponent<OutlineComponent>();
            if (outlineComponent != null)
            {
                outlineComponent.enabled = enable;
            }
        }

        /// <summary>
        /// 设置实体的描边颜色
        /// </summary>
        /// <param name="entityId">实体ID</param>
        /// <param name="color">描边颜色</param>
        public void SetEntityOutlineColor(int entityId, Color color)
        {
            var entity = new Entity(entityId);
            if (!ComponentManager.HasComponent<ViewComponent>(entity))
                return;

            var viewComponent = ComponentManager.GetComponent<ViewComponent>(entity);
            if (viewComponent.GameObject == null)
                return;

            var outlineComponent = viewComponent.GameObject.GetComponent<OutlineComponent>();
            if (outlineComponent != null)
            {
                outlineComponent.OutlineColor = color;
            }
        }

        /// <summary>
        /// 为正在建造的建筑显示建造模型
        /// </summary>
        /// <param name="parentObject">父对象（建筑模型）</param>
        /// <param name="entity">实体</param>
        private void ShowConstructionModel(GameObject parentObject, Entity entity)
        {
            // 获取建造模型预制体
            if (_unitPrefabs.TryGetValue(8, out var constructionPrefab))
            {
                // 实例化建造模型
                GameObject constructionModel = Object.Instantiate(constructionPrefab, parentObject.transform);
                constructionModel.name = $"ConstructionModel_Entity_{entity.Id}";
                
                // 设置建造模型的位置（在父对象上方）
                constructionModel.transform.localPosition = Vector3.up * 2;
                
                // 将建造模型添加到字典中
                _constructionModels[entity.Id] = constructionModel;
                
                Debug.Log($"[PresentationSystem] 为正在建造的实体 {entity.Id} 显示建造模型");
            }
            else
            {
                Debug.LogWarning($"[PresentationSystem] 找不到预制体ID为8的建造模型");
            }
        }

        /// <summary>
        /// 正在播放死亡动画的实体列表
        /// 用于跟踪动画播放状态，在动画播放完毕后移除视图组件
        /// </summary>
        private List<(int entityId, Animator animator)> _dyingEntities = new List<(int, Animator)>();
    }
}