using UnityEngine;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;

namespace ZLockstep.View.Systems
{
    /// <summary>
    /// 表现同步系统：将逻辑层数据同步到Unity表现层
    /// 这个System应该在所有逻辑System之后执行
    /// </summary>
    public class PresentationSystem : BaseSystem
    {
        /// <summary>
        /// 是否启用平滑插值（在FixedUpdate之间）
        /// </summary>
        public bool EnableSmoothInterpolation { get; set; } = false;

        protected override void OnInitialize()
        {
            base.OnInitialize();
        }

        public override void Update()
        {
            // 在逻辑帧时，同步所有有View的实体
            SyncAllViews();
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

            // 同步其他视觉效果
            SyncVisualEffects(entity, view);

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
        /// 同步其他视觉效果（血条、选中框等）
        /// </summary>
        private void SyncVisualEffects(Entity entity, ViewComponent view)
        {
            // TODO: 这里可以发送事件给UI系统来更新血条
            if (ComponentManager.HasComponent<HealthComponent>(entity))
            {
                var health = ComponentManager.GetComponent<HealthComponent>(entity);
                // EventManager.Dispatch(new HealthChangedEvent 
                // { 
                //     EntityId = entity.Id, 
                //     HealthPercent = health.HealthPercent 
                // });
            }
        }

        /// <summary>
        /// Unity的Update中调用，用于平滑插值（可选）
        /// </summary>
        public void LerpUpdate(float deltaTime, float interpolationSpeed = 10f)
        {
            if (!EnableSmoothInterpolation)
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

