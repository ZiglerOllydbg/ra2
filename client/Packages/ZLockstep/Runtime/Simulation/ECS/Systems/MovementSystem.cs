using ZLockstep.Simulation.ECS.Components;
using zUnity;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 移动系统
    /// 处理所有单位的移动逻辑
    /// 1. 根据移动命令更新速度
    /// 2. 根据速度更新位置
    /// </summary>
    public class MovementSystem : BaseSystem
    {
        public override void Update()
        {
            // 处理所有有移动命令的实体
            ProcessMoveCommands();

            // 应用速度到位置
            ApplyVelocity();
        }

        /// <summary>
        /// 处理移动命令，更新速度和朝向
        /// </summary>
        private void ProcessMoveCommands()
        {
            var commandEntities = ComponentManager.GetAllEntityIdsWith<MoveCommandComponent>();

            foreach (var entityId in commandEntities)
            {
                var entity = new Entity(entityId);

                // 必须有Transform和Unit组件
                if (!ComponentManager.HasComponent<TransformComponent>(entity) ||
                    !ComponentManager.HasComponent<UnitComponent>(entity))
                    continue;

                var moveCmd = ComponentManager.GetComponent<MoveCommandComponent>(entity);
                var transform = ComponentManager.GetComponent<TransformComponent>(entity);
                var unit = ComponentManager.GetComponent<UnitComponent>(entity);

                // 计算到目标的方向和距离
                zVector3 toTarget = moveCmd.TargetPosition - transform.Position;
                zfloat distanceSqr = toTarget.sqrMagnitude;

                // 检查是否到达
                if (distanceSqr <= moveCmd.StopDistance * moveCmd.StopDistance)
                {
                    // 到达目标，移除移动命令和速度
                    ComponentManager.RemoveComponent<MoveCommandComponent>(entity);
                    
                    if (ComponentManager.HasComponent<VelocityComponent>(entity))
                    {
                        ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));
                    }
                    continue;
                }

                // 计算移动方向
                zVector3 direction = toTarget.normalized;

                // 更新朝向
                if (direction.sqrMagnitude > zfloat.Zero)
                {
                    zQuaternion targetRotation = zQuaternion.LookRotation(direction);
                    // TODO: 可以添加平滑旋转逻辑
                    transform.Rotation = targetRotation;
                    zUDebug.Log("[MovementSystem] Update entity " + entity.Id + " rotation to " + targetRotation);
                }

                // 更新或添加速度组件
                var velocity = new VelocityComponent(direction * unit.MoveSpeed);
                ComponentManager.AddComponent(entity, velocity);

                // 写回Transform
                ComponentManager.AddComponent(entity, transform);
            }
        }

        /// <summary>
        /// 应用速度到位置
        /// </summary>
        private void ApplyVelocity()
        {
            var velocityEntities = ComponentManager.GetAllEntityIdsWith<VelocityComponent>();

            foreach (var entityId in velocityEntities)
            {
                var entity = new Entity(entityId);

                if (!ComponentManager.HasComponent<TransformComponent>(entity))
                    continue;

                var velocity = ComponentManager.GetComponent<VelocityComponent>(entity);
                var transform = ComponentManager.GetComponent<TransformComponent>(entity);

                // 更新位置
                transform.Position += velocity.Value * DeltaTime;

                // 写回
                ComponentManager.AddComponent(entity, transform);
            }
        }
    }
}

