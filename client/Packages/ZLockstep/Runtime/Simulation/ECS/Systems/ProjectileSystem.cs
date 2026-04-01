using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using ZLockstep.Flow;
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

                // 3. 检查是否命中（使用弹道配置的命中距离）
                zfloat hitDistance = proj.HitDistance > zfloat.Zero ? proj.HitDistance : HIT_DISTANCE;
                if (distance <= hitDistance)
                {
                    // 命中！判断是单体伤害还是 AOE 伤害
                    if (proj.DamageRadius > zfloat.Zero)
                    {
                        // AOE 多目标伤害
                        DealAreaDamage(transform.Position, proj);
                    }
                    else
                    {
                        // 单目标伤害（保持原有逻辑）
                        if (proj.TargetEntityId >= 0)
                        {
                            DealDamage(proj.TargetEntityId, proj.Damage);
                        }
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
        /// 对目标造成伤害（单目标）
        /// </summary>
        private void DealDamage(int targetEntityId, zfloat damage)
        {
            Entity target = new Entity(targetEntityId);

            if (!ComponentManager.HasComponent<HealthComponent>(target))
                return;

            int def = 0;
            // 单位目标，获取防御属性
            if (ComponentManager.HasComponent<UnitComponent>(target))
            {
                var unit = ComponentManager.GetComponent<UnitComponent>(target);
                ConfUnit confUnit = ConfigManager.Get<ConfUnit>((int)unit.UnitType);
                if (confUnit != null)
                {
                    def = confUnit.Def;
                }
            }

            zfloat finalDamage = damage - new zfloat(def);

            var health = ComponentManager.GetComponent<HealthComponent>(target);
            health.TakeDamage(damage, World.TimeManager.Tick); // 使用 TakeDamage 方法并传入当前 tick

            // 写回生命值组件（由 HealthSystem 统一处理死亡检测）
            ComponentManager.AddComponent(target, health);

            // zUDebug.Log($"[ProjectileSystem] 对实体{targetEntityId}造成{damage}伤害，剩余生命{health.CurrentHealth}");
        }

        /// <summary>
        /// AOE 范围伤害（多目标）
        /// </summary>
        /// <param name="hitPosition">命中位置</param>
        /// <param name="proj">弹道组件</param>
        private void DealAreaDamage(zVector3 hitPosition, ProjectileComponent proj)
        {
            // 1. 确定搜索参数
            float searchRadius = (float)proj.DamageRadius;
            int maxTargets = proj.MaxDamageTargets > 0 ? proj.MaxDamageTargets : -1;

            // 2. 使用 SpatialIndex 搜索范围内的目标
            var targets = SpatialIndex.Instance.RadialSearch(
                hitPosition,
                searchRadius,
                maxTargets,
                entry => IsValidTarget(entry, proj.SourceEntityId, proj.SourceCampId)
            );

            if (targets.Count == 0)
                return;

            // 3. 计算实际伤害
            zfloat actualDamage = proj.Damage;
            if (proj.ShareDamage && targets.Count > 0)
            {
                // 均摊模式：总伤害平均分配
                actualDamage = proj.Damage / new zfloat(targets.Count);
            }

            // 4. 对每个目标造成伤害
            foreach (var target in targets)
            {
                DealDamage(target.EntityId, actualDamage);
            }
        }

        /// <summary>
        /// 判断目标是否为有效的伤害目标
        /// </summary>
        /// <param name="entry">空间索引条目</param>
        /// <param name="sourceEntityId">弹道发射者ID</param>
        /// <param name="sourceCampId">弹道发射者阵营ID</param>
        /// <returns>是否为有效目标</returns>
        private bool IsValidTarget(SpatialEntry entry, int sourceEntityId, int sourceCampId)
        {
            // 1. 不能伤害自己
            if (entry.EntityId == sourceEntityId)
                return false;

            // 2. 判断敌我关系
            var targetCamp = new CampComponent { CampId = entry.CampId };
            var sourceCamp = new CampComponent { CampId = sourceCampId };
            if (!sourceCamp.IsEnemy(targetCamp))
                return false;

            // 3. 目标必须有生命值组件
            Entity targetEntity = new Entity(entry.EntityId);
            if (!ComponentManager.HasComponent<HealthComponent>(targetEntity))
                return false;

            // 4. 目标不能是死亡状态
            if (ComponentManager.HasComponent<DeathComponent>(targetEntity))
                return false;

            return true;
        }

        /// <summary>
        /// 销毁弹道
        /// </summary>
        private void DestroyProjectile(int projectileId)
        {
            Entity projectile = new Entity(projectileId);
            // 添加死亡组件
            var deathComponent = new DeathComponent(World.TimeManager.Tick, 0);
            ComponentManager.AddComponent(projectile, deathComponent);

            // 发布死亡事件（通知表现层播放死亡动画）
            var diedEvent = new EntityDiedEvent
            {
                EntityId = projectileId
            };
            EventManager.Publish(diedEvent);
            
            // 只移除逻辑组件，保留ViewComponent给表现层处理
            ComponentManager.RemoveComponent<ProjectileComponent>(projectile);
            ComponentManager.RemoveComponent<TransformComponent>(projectile);
            if (ComponentManager.HasComponent<VelocityComponent>(projectile))
                ComponentManager.RemoveComponent<VelocityComponent>(projectile);

            // zUDebug.Log($"[ProjectileSystem] 弹道{projectileId}已销毁");
        }
    }
}