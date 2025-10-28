using UnityEngine;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;

namespace ZLockstep.View.Systems
{
    /// <summary>
    /// 表现同步系统：将逻辑层数据同步到Unity表现层
    /// 
    /// 职责：
    /// 1. 监听逻辑事件，创建Unity GameObject
    /// 2. 同步逻辑Transform到Unity Transform
    /// 3. 同步动画状态
    /// 4. 可选：提供平滑插值
    /// 
    /// 执行时机：
    /// - 在所有逻辑System之后执行
    /// - 在同一逻辑帧内处理事件（确保及时创建View）
    /// </summary>
    public class PresentationSystem : BaseSystem
    {
        // Unity资源
        private Transform _viewRoot;
        private Dictionary<int, GameObject> _unitPrefabs;

        /// <summary>
        /// 是否启用平滑插值（在FixedUpdate之间）
        /// </summary>
        public bool EnableSmoothInterpolation { get; set; } = false;

        /// <summary>
        /// 是否启用表现系统（追帧时可以禁用以提升性能）
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 初始化系统（由GameWorldBridge调用）
        /// </summary>
        public void Initialize(Transform viewRoot, Dictionary<int, GameObject> prefabs)
        {
            _viewRoot = viewRoot;
            _unitPrefabs = prefabs;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
        }

        public override void Update()
        {
            // 追帧时跳过（提升性能）
            if (!Enabled)
            {
                return;
            }

            // 1. 处理事件（创建新的View）
            ProcessCreationEvents();

            // 2. 同步已有的View
            SyncAllViews();
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
            // 获取预制体
            if (!_unitPrefabs.TryGetValue(evt.UnitType, out var prefab))
            {
                if (!_unitPrefabs.TryGetValue(evt.PrefabId, out prefab))
                {
                    Debug.LogWarning($"[PresentationSystem] 找不到预制体: UnitType={evt.UnitType}, PrefabId={evt.PrefabId}");
                    return;
                }
            }

            // 实例化GameObject
            GameObject viewObject = Object.Instantiate(prefab, _viewRoot);
            viewObject.name = $"Unit_{evt.EntityId}_Type{evt.UnitType}_P{evt.PlayerId}";
            viewObject.transform.position = evt.Position.ToVector3();

            // 创建并添加ViewComponent
            var entity = new Entity(evt.EntityId);
            var viewComponent = ViewComponent.Create(viewObject, EnableSmoothInterpolation);
            ComponentManager.AddComponent(entity, viewComponent);

            Debug.Log($"[PresentationSystem] 为Entity_{evt.EntityId}创建了视图: {viewObject.name}");
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
        /// 为缺失视图的实体创建 GameObject
        /// </summary>
        private void CreateViewForMissingEntity(Entity entity)
        {
            var unit = ComponentManager.GetComponent<UnitComponent>(entity);
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);

            // 获取预制体
            if (!_unitPrefabs.TryGetValue(unit.UnitType, out var prefab))
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

            // 创建并添加ViewComponent
            var viewComponent = ViewComponent.Create(viewObject, EnableSmoothInterpolation);
            ComponentManager.AddComponent(entity, viewComponent);

            Debug.Log($"[PresentationSystem] 重新创建视图: Entity_{entity.Id}");
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

                view.Animator.SetBool("IsMoving", isMoving);
                view.Animator.SetFloat("MoveSpeed", speed);
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
                    view.Animator.SetBool("IsAttacking", true);
                }
                else
                {
                    view.Animator.SetBool("IsAttacking", false);
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
    }
}
