using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using zUnity;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 弹道系统
    /// 处理所有弹道的移动、追踪和命中逻辑
    /// 1. 更新弹道位置
    /// 2. 追踪目标（如果是追踪型）
    /// 3. 检测命中并造成伤害
    /// 4. 销毁已命中的弹道
    /// </summary>
    public class ProjectileSystem : BaseSystem
    {
        /// <summary>
        /// 命中距离阈值
        /// </summary>
        private readonly zfloat HIT_DISTANCE = new zfloat(0, 5000); // 0.5米

        public override void Update()
        {
            // 处理所有弹道
            ProcessProjectiles();
        }

        /// <summary>
        /// 处理弹道逻辑
        /// </summary>
        private void ProcessProjectiles()
        {
            var projectileEntities = ComponentManager.GetAllEntityIdsWith<ProjectileComponent>();
            var toDestroy = new System.Collections.Generic.List<int>();

            foreach (var entityId in projectileEntities)
            {
                var entity = new Entity(entityId);

                if (!ComponentManager.HasComponent<TransformComponent>(entity))
                    continue;

                var proj = ComponentManager.GetComponent<ProjectileComponent>(entity);
                var transform = ComponentManager.GetComponent<TransformComponent>(entity);

                // 1. 更新目标位置（如果是追踪型导弹）
                if (proj.IsHoming && proj.TargetEntityId >= 0)
                {
                    Entity target = new Entity(proj.TargetEntityId);
                    
                    if (ComponentManager.HasComponent<TransformComponent>(target))
                    {
                        var targetTransform = ComponentManager.GetComponent<TransformComponent>(target);
                        proj.TargetPosition = targetTransform.Position;
                    }
                    else
                    {
                        // 目标已不存在，销毁弹道
                        toDestroy.Add(entityId);
                        continue;
                    }
                }

                // 2. 计算到目标的方向和距离
                zVector3 toTarget = proj.TargetPosition - transform.Position;
                zfloat distance = toTarget.magnitude;

                // 3. 检查是否命中
                if (distance <= HIT_DISTANCE)
                {
                    // 命中！造成伤害
                    if (proj.TargetEntityId >= 0)
                    {
                        DealDamage(proj.TargetEntityId, proj.Damage);
                    }

                    // 标记为待销毁
                    toDestroy.Add(entityId);
                    continue;
                }

                // 4. 更新弹道方向和速度
                zVector3 direction = toTarget.normalized;
                
                // 更新速度组件
                if (ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    ComponentManager.AddComponent(entity, new VelocityComponent(direction * proj.Speed));
                }

                // 更新位置
                transform.Position += direction * proj.Speed * DeltaTime;

                // 更新朝向（让弹道朝向飞行方向）
                if (direction.sqrMagnitude > zfloat.Zero)
                {
                    transform.Rotation = zQuaternion.LookRotation(direction);
                }

                // 写回组件
                ComponentManager.AddComponent(entity, transform);
                ComponentManager.AddComponent(entity, proj);
            }

            // 销毁已命中的弹道
            foreach (var entityId in toDestroy)
            {
                DestroyProjectile(entityId);
            }
        }

        /// <summary>
        /// 对目标造成伤害
        /// </summary>
        private void DealDamage(int targetEntityId, zfloat damage)
        {
            Entity target = new Entity(targetEntityId);

            if (!ComponentManager.HasComponent<HealthComponent>(target))
                return;

            var health = ComponentManager.GetComponent<HealthComponent>(target);
            health.CurrentHealth -= damage;

            // 检查是否死亡
            if (health.CurrentHealth <= zfloat.Zero)
            {
                health.CurrentHealth = zfloat.Zero;
                OnEntityDeath(target);
            }

            // 写回生命值组件
            ComponentManager.AddComponent(target, health);

            // zUDebug.Log($"[ProjectileSystem] 对实体{targetEntityId}造成{damage}伤害，剩余生命{health.CurrentHealth}");
        }

        /// <summary>
        /// 实体死亡处理
        /// </summary>
        private void OnEntityDeath(Entity entity)
        {
            // 添加死亡组件，设置2秒后尸体消失
            ComponentManager.AddComponent(entity, new DeathComponent(World.TimeManager.Tick, 2 * 20)); // 2秒 * 20帧/秒

            // 发布销毁事件
            var destroyEvent = new UnitDestroyedEvent
            {
                EntityId = entity.Id
            };
            
            EventManager.Publish(destroyEvent);
            UnityEngine.Debug.Log($"[EntityDestructionSystem] 发布实体 {entity} 的销毁事件");
        }

        /// <summary>
        /// 销毁弹道
        /// </summary>
        private void DestroyProjectile(int projectileId)
        {
            // 发布单位销毁事件，确保表现层能同步销毁视觉对象
            var destroyEvent = new UnitDestroyedEvent
            {
                EntityId = projectileId
            };
            EventManager.Publish(destroyEvent);
            
            Entity projectile = new Entity(projectileId);
            // 只移除逻辑组件，保留ViewComponent给表现层处理
            ComponentManager.RemoveComponent<ProjectileComponent>(projectile);
            ComponentManager.RemoveComponent<TransformComponent>(projectile);
            if (ComponentManager.HasComponent<VelocityComponent>(projectile))
                ComponentManager.RemoveComponent<VelocityComponent>(projectile);

            // zUDebug.Log($"[ProjectileSystem] 弹道{projectileId}已销毁");
        }
    }
}

