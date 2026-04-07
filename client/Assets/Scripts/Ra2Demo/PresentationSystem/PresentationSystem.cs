using System;
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

        /// <summary>
        /// 是否启用平滑插值（在FixedUpdate之间）
        /// </summary>
        public bool EnableSmoothInterpolation { get; set; } = true;

        /// <summary>
        /// 是否启用表现系统（追帧时可以禁用以提升性能）
        /// </summary>
        public bool Enabled { get; set; } = true;

        // 添加对Game对象的引用
        private ZLockstep.Sync.Game _game;

        /// <summary>
        /// 初始化系统（由GameWorldBridge调用）
        /// </summary>
        public void Initialize(Transform viewRoot)
        {
            _viewRoot = viewRoot;
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
                UnityEngine.Object.Destroy(_viewRoot.GetChild(i).gameObject);
            }
            
            // 清理死亡实体列表
            _dyingEntities.Clear();
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

            // 2. 检查死亡动画状态
            CheckDyingEntities();

            // 3. 处理死亡事件（ProcessEntityDiedEvents播放死亡动画）
            ProcessEntityDiedEvents();

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
                zUDebug.Log($"[PresentationSystem] 显示消息：{evt.Message}");
                Frame.DispatchEvent(new ShowMessageEvent(evt.Message));
            }
        }


        /// <summary>
        /// 处理经济变化事件（金币和能量变化）
        /// </summary>
        private void ProcessEconomyEvents()
        {
            // 处理金币变化事件
            var moneyEvents = EventManager.GetEvents<MoneyChangedEvent>();
            foreach (var evt in moneyEvents)
            {
                // 这里可以添加处理金币变化的逻辑
                // 例如：更新UI上的金币显示
                // Debug.Log($"[PresentationSystem] 阵营{evt.CampId}金币变化: {evt.OldMoney} -> {evt.NewMoney} ({evt.Reason})");
                Frame.DispatchEvent(new EconomyEvent());
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
                    TrySetTrigger(viewComponent.Animator, "Death");
                }
                else
                {
                }
                
                // 添加到死亡列表，用于后续跟踪动画状态
                _dyingEntities.Add((evt.EntityId, viewComponent.Animator));
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
                            GameObjectPoolManager.ReturnToPool(viewComponent.GameObject);
                        }

                        // 移除ViewComponent
                        ComponentManager.RemoveComponent<ViewComponent>(entity);
                        // Debug.Log($"[PresentationSystem] 实体{entityId}死亡动画播放完毕，已移除视图");

                        // 移除血条
                        Frame.DispatchEvent(new HealthEvent(entityId, false, false));
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
            AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(0);
            if (nextStateInfo.IsName("Death"))
            {
                return false;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 检查是否是死亡动画状态
            // 不同单位可能有不同的状态命名：Death、Cartridge_Bomb等
            bool isDeathState = stateInfo.IsName("Death");
            if (isDeathState)
            {
                // normalizedTime大于等于1.0表示动画已经播放完成
                return stateInfo.normalizedTime >= 1.0f;
            }

            // 如果不是已知的死亡状态，可能动画已经切换，也可以认为完成
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
            String viewName = "";

            if (evt.ConfBuildingID > 0)
            {
                ConfBuilding confBuilding = ConfigManager.Get<ConfBuilding>(evt.ConfBuildingID);
                if (confBuilding != null)
                {
                    // 创建建筑模型 - 使用对象池
                    string buildingPrefabPath = "Prefabs/" + confBuilding.BuildPrefab;
                    viewObject = GameObjectPoolManager.GetFromPool(buildingPrefabPath, _viewRoot);

                    if (confBuilding.Hp > 0)
                    {
                        // 血条显示
                        Frame.DispatchEvent(new HealthEvent(evt.EntityId, evt.PlayerId == 1, true));
                    }

                    viewName = "[" + evt.PlayerId + "]Building_" + confBuilding.Type + "_" + evt.EntityId;
                }
            }

            if (evt.ConfUnitID > 0)
            {
                ConfUnit confUnit = ConfigManager.Get<ConfUnit>(evt.ConfUnitID);
                if (confUnit != null)
                {
                    // 创建单位模型 - 使用对象池
                    string unitPrefabPath = "Prefabs/" + confUnit.Prefab;
                    viewObject = GameObjectPoolManager.GetFromPool(unitPrefabPath, _viewRoot);

                    viewName = "[" + evt.PlayerId + "]Unit_" + confUnit.Type + "_" + evt.EntityId;
                }

                // 血条显示
                Frame.DispatchEvent(new HealthEvent(evt.EntityId, evt.PlayerId == 1, true));
            }

            if (evt.ConfProjectileID > 0)
            {
                ConfProjectile confProjectile = ConfigManager.Get<ConfProjectile>(evt.ConfProjectileID);
                if (confProjectile != null)
                {
                    // 创建弹道模型 - 使用对象池
                    string projectilePrefabPath = "Prefabs/" + confProjectile.Prefab;
                    viewObject = GameObjectPoolManager.GetFromPool(projectilePrefabPath, _viewRoot);
                    
                    // 获取音频组件，并播放音频
                    AudioSource audioSource = viewObject?.GetComponent<AudioSource>();
                    if (audioSource != null)
                    {
                        AudioClip clip = ResourceCache.GetAudioClip("Audio/" + confProjectile.AudioClip);
                        audioSource.clip = clip;
                        audioSource.Play();
                    }

                    viewName = "[" + evt.PlayerId + "]Projectile_" + confProjectile.Type + "_" + evt.EntityId;
                }
            }

            if (viewObject == null)
                return;

            viewObject.name = viewName;
            viewObject.transform.position = evt.Position.ToVector3();

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

            // Debug.Log($"[PresentationSystem] 为Entity_{evt.EntityId}创建了视图: {viewObject.name}, position:{viewObject.transform.position}");
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

            // Debug.Log($"[PresentationSystem] 创建了默认弹道可视化: Entity_{evt.EntityId}");

            return projectile;
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
            if (_viewRoot == null)
            {
                Debug.LogWarning("[PresentationSystem] ResyncAllEntities: 未初始化");
                return;
            }

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
                    zUDebug.LogError($"[PresentationSystem] ResyncAllEntities: Entity_{entity.Id} 缺少 ViewComponent");
                }
            }

            // zUDebug.Log($"[PresentationSystem] ResyncAllEntities 完成: 新建={createdCount}, 同步={syncedCount}");
        }

        /// <summary>
        /// 将逻辑实体的数据同步到Unity GameObject
        /// </summary>
        private void SyncEntityToView(Entity entity)
        {
            var view = ComponentManager.GetComponent<ViewComponent>(entity);
            var transformComponent = ComponentManager.GetComponent<TransformComponent>(entity);

            if (view.Transform == null)
                return;

            // 检查是否是正在建造的建筑
            if (!ComponentManager.HasComponent<BuildingConstructionComponent>(entity))
            {
                if (ComponentManager.HasComponent<BuildingComponent>(entity))
                {
                    var buildingComponent = ComponentManager.GetComponent<BuildingComponent>(entity);
                    if (!view.BuildingOK)
                    {
                        ConfBuilding confBuilding = ConfigManager.Get<ConfBuilding>(buildingComponent.BuildingType);
                        if (confBuilding == null)
                        {
                            Debug.LogError($"[PresentationSystem] 找不到建筑配置：BuildingType={buildingComponent.BuildingType}");
                            return;
                        }

                        // 移除旧建筑模型（保存位置信息）
                        Vector3 oldPosition = Vector3.zero;
                        Quaternion oldRotation = Quaternion.identity;

                        String gameObjectName = view.GameObject.name;
                        
                        if (view.GameObject != null)
                        {
                            // 保存旧模型的位置和旋转
                            oldPosition = view.Transform.position;
                            oldRotation = view.Transform.rotation;
                            
                            // 回收到对象池
                            GameObjectPoolManager.ReturnToPool(view.GameObject);
                            view.GameObject = null;
                        }

                        // 创建建筑模型 - 使用对象池
                        string prefabPath = "Prefabs/" + confBuilding.Prefab;
                        view.GameObject = GameObjectPoolManager.GetFromPool(prefabPath, _viewRoot);
                        view.GameObject.name = gameObjectName;
;
                        // 应用旧模型的位置和旋转
                        view.GameObject.transform.position = oldPosition;
                        view.GameObject.transform.rotation = oldRotation;
                        
                        // 更新 Transform 缓存
                        view.Transform = view.GameObject.transform;

                        int campId = ComponentManager.GetComponent<CampComponent>(entity).CampId;

                        var indicator = view.GameObject.AddComponent<PlayerUnitIndicator>();
                        indicator.Initialize(campId);

                        view.BuildingOK = true;
                        ComponentManager.AddComponent(entity, view);
                    }
                }
                
            }

            // 如果启用插值，记录上一帧的逻辑位置（用于下一帧插值）
            if (view.EnableInterpolation)
            {
                view.LastLogicPosition = view.Transform.position;
                view.LastLogicRotation = view.Transform.rotation;
            }

            // 直接同步（无插值）
            if (!EnableSmoothInterpolation)
            {
                // 获取当前逻辑位置和旋转
                Vector3 currentLogicPos = transformComponent.Position.ToVector3();
                Quaternion currentLogicRot = transformComponent.Rotation.ToQuaternion();
                view.Transform.SetPositionAndRotation(currentLogicPos, currentLogicRot);
            }

            // 同步缩放
            view.Transform.localScale = transformComponent.Scale.ToVector3();

            // 写回 ViewComponent（如果有修改）
            ComponentManager.AddComponent(entity, view);
            
            // 同步动画状态
            // SyncAnimationState(entity, view);

            // zUDebug.Log($"[PresentationSystem] SyncEntityToView: entityId={entity.Id}, transformComponent.Rotation.eulerAngles:{transformComponent.Rotation.eulerAngles}");
        }

        /// <summary>
        /// Unity 的 Update 中调用，用于平滑插值（可选）
        /// <param name="deltaTime">时间间隔</param>
        /// </summary>
        public void LerpUpdate(float deltaTime)
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

                // 插值到目标位置：使用 LastLogicPosition 作为起点，而不是当前视觉位置
                Vector3 targetPos = logicTransform.Position.ToVector3();
                Quaternion targetRot = logicTransform.Rotation.ToQuaternion();

                // 检测目标位置是否变化，如果变化则重置插值时间
                bool targetChanged = targetPos != view.LastLogicPosition;
                
                if (targetChanged)
                {
                    zUDebug.Log($"[PresentationSystem] LerpUpdate: entityId={entity.Id}, targetPos:{targetPos}");
                    view.CurrentInterpolationTime = 0f;
                }

                // 累加插值时间
                view.CurrentInterpolationTime += deltaTime;

                // 计算插值因子（0 到 1 之间），确保不超过 1
                float t = Mathf.Clamp01(view.CurrentInterpolationTime / view.InterpolationDuration);

                // 正确的插值逻辑：从上一帧的逻辑位置插值到当前帧的逻辑位置
                // 使用固定时间控制，最大 0.25 秒完成插值
                view.Transform.position = Vector3.Lerp(
                    view.LastLogicPosition,
                    targetPos,
                    t
                );

                view.Transform.rotation = Quaternion.Lerp(
                    view.LastLogicRotation,
                    targetRot,
                    t
                );
            }
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
                TrySetInteger(view.Animator, "speed", (int)speed);
            }

            // 根据生命值设置死亡动画
            if (ComponentManager.HasComponent<HealthComponent>(entity))
            {
                var health = ComponentManager.GetComponent<HealthComponent>(entity);
                if (health.IsDead)
                {
                    TrySetTrigger(view.Animator, "Death");
                }
            }

            // 根据攻击组件设置攻击动画
            if (ComponentManager.HasComponent<AttackComponent>(entity))
            {
                var attack = ComponentManager.GetComponent<AttackComponent>(entity);
                if (attack.HasTarget)
                {
                    TrySetTrigger(view.Animator, "Fire");
                }
                else
                {
                    // view.Animator.SetBool("Fire", false);
                }
            }
        }

        private static bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName))
                return false;

            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == parameterType && parameter.name == parameterName)
                    return true;
            }

            return false;
        }

        private static bool TrySetTrigger(Animator animator, string triggerName)
        {
            if (!HasAnimatorParameter(animator, triggerName, AnimatorControllerParameterType.Trigger))
                return false;

            animator.SetTrigger(triggerName);
            return true;
        }

        private static bool TrySetInteger(Animator animator, string parameterName, int value)
        {
            if (!HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Int))
                return false;

            animator.SetInteger(parameterName, value);
            return true;
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
        /// 正在播放死亡动画的实体列表
        /// 用于跟踪动画播放状态，在动画播放完毕后移除视图组件
        /// </summary>
        private List<(int entityId, Animator animator)> _dyingEntities = new List<(int, Animator)>();
    }
}
