using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using zUnity;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 战斗系统
    /// 处理单位和建筑的战斗逻辑
    /// 1. 自动搜索攻击范围内的敌方单位
    /// 2. 更新攻击冷却
    /// 3. 发射弹道（创建Projectile Entity）
    /// </summary>
    public class CombatSystem : BaseSystem
    {
        public override void Update()
        {
            // 处理所有有攻击能力的实体
            ProcessAttacks();
        }

        /// <summary>
        /// 处理攻击逻辑
        /// </summary>
        private void ProcessAttacks()
        {
            var attackerEntities = ComponentManager.GetAllEntityIdsWith<AttackComponent>();

            foreach (var entityId in attackerEntities)
            {
                var entity = new Entity(entityId);

                // 必须有Transform和Camp组件
                if (!ComponentManager.HasComponent<TransformComponent>(entity) ||
                    !ComponentManager.HasComponent<CampComponent>(entity))
                    continue;

                // 检查攻击者是否还活着（只检查DeathComponent，不检查生命值）
                if (ComponentManager.HasComponent<DeathComponent>(entity))
                {
                    continue; // 死亡单位不能攻击
                }

                var attack = ComponentManager.GetComponent<AttackComponent>(entity);
                var transform = ComponentManager.GetComponent<TransformComponent>(entity);
                var camp = ComponentManager.GetComponent<CampComponent>(entity);

                // 更新攻击冷却
                attack.TimeSinceLastAttack += DeltaTime;

                // 1. 检查是否有目标
                if (attack.TargetEntityId < 0 || !IsValidTarget(attack.TargetEntityId))
                {
                    // 搜索新目标
                    attack.TargetEntityId = FindNearestEnemy(entity, camp, transform.Position, attack.Range);
                }

                // 2. 如果有目标，检查是否在范围内
                if (attack.TargetEntityId >= 0)
                {
                    Entity target = new Entity(attack.TargetEntityId);
                    
                    if (!ComponentManager.HasComponent<TransformComponent>(target))
                    {
                        attack.TargetEntityId = -1;
                        ComponentManager.AddComponent(entity, attack);
                        
                        // 清空炮塔目标
                        if (ComponentManager.HasComponent<TurretComponent>(entity))
                        {
                            var turret = ComponentManager.GetComponent<TurretComponent>(entity);
                            turret.HasTarget = false;
                            ComponentManager.AddComponent(entity, turret);
                        }
                        
                        continue;
                    }

                    var targetTransform = ComponentManager.GetComponent<TransformComponent>(target);
                    zfloat distanceSqr = (targetTransform.Position - transform.Position).sqrMagnitude;
                    zfloat rangeSqr = attack.Range * attack.Range;

                    // 如果超出范围，清空目标
                    if (distanceSqr > rangeSqr)
                    {
                        attack.TargetEntityId = -1;
                        
                        // 清空炮塔目标
                        if (ComponentManager.HasComponent<TurretComponent>(entity))
                        {
                            var turret = ComponentManager.GetComponent<TurretComponent>(entity);
                            turret.HasTarget = false;
                            ComponentManager.AddComponent(entity, turret);
                        }
                    }
                    // 如果在范围内，设置炮塔朝向目标
                    else
                    {
                        // 更新炮塔朝向目标
                        if (ComponentManager.HasComponent<TurretComponent>(entity))
                        {
                            var turret = ComponentManager.GetComponent<TurretComponent>(entity);
                            turret.HasTarget = true;
                            
                            // 计算朝向目标的方向（2D）
                            zVector3 toTarget = targetTransform.Position - transform.Position;
                            zVector2 toTarget2D = new zVector2(toTarget.x, toTarget.z);
                            if (toTarget2D.magnitude > zfloat.Epsilon)
                            {
                                turret.DesiredTurretDirection = toTarget2D.normalized;
                            }
                            
                            ComponentManager.AddComponent(entity, turret);
                        }
                        
                        // 如果冷却完成，发射攻击
                        if (attack.CanAttack)
                        {
                            FireProjectile(entity, target, attack.Damage, transform.Position, targetTransform.Position, camp.CampId);
                            attack.TimeSinceLastAttack = zfloat.Zero;
                        }
                    }
                }

                // 写回攻击组件
                ComponentManager.AddComponent(entity, attack);
            }
        }

        /// <summary>
        /// 搜索最近的敌方单位
        /// </summary>
        private int FindNearestEnemy(Entity self, CampComponent selfCamp, zVector3 position, zfloat range)
        {
            int nearestEnemyId = -1;
            zfloat minDistSqr = range * range;

            var allEntities = ComponentManager.GetAllEntityIdsWith<CampComponent>();

            foreach (var entityId in allEntities)
            {
                Entity otherEntity = new Entity(entityId);

                // 跳过自己
                if (otherEntity.Id == self.Id)
                    continue;

                // 检查是否为敌人
                var otherCamp = ComponentManager.GetComponent<CampComponent>(otherEntity);
                if (!selfCamp.IsEnemy(otherCamp))
                    continue;

                // 必须有Transform和Health组件
                if (!ComponentManager.HasComponent<TransformComponent>(otherEntity) ||
                    !ComponentManager.HasComponent<HealthComponent>(otherEntity))
                    continue;

                // 检查是否还活着（只检查DeathComponent）
                if (ComponentManager.HasComponent<DeathComponent>(otherEntity))
                    continue;

                // 计算距离
                var otherTransform = ComponentManager.GetComponent<TransformComponent>(otherEntity);
                zfloat distSqr = (otherTransform.Position - position).sqrMagnitude;

                // 找到最近的敌人
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    nearestEnemyId = entityId;
                }
            }

            return nearestEnemyId;
        }

        /// <summary>
        /// 检查目标是否有效
        /// </summary>
        private bool IsValidTarget(int targetEntityId)
        {
            Entity target = new Entity(targetEntityId);

            // 检查是否存在
            if (!ComponentManager.HasComponent<TransformComponent>(target))
                return false;

            // 检查是否还活着（只检查DeathComponent）
            if (ComponentManager.HasComponent<DeathComponent>(target))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 发射弹道
        /// </summary>
        private void FireProjectile(Entity source, Entity target, zfloat damage, 
            zVector3 sourcePos, zVector3 targetPos, int sourceCampId)
        {
            // 创建弹道实体
            var projectile = World.EntityManager.CreateEntity();

            // 添加Transform（从源位置开始）
            ComponentManager.AddComponent(projectile, new TransformComponent
            {
                Position = sourcePos + new zVector3(zfloat.Zero, new zfloat(0, 5000), zfloat.Zero), // 稍微抬高0.5米
                Rotation = zQuaternion.identity,
                Scale = zVector3.one
            });

            // 添加弹道组件
            var projComponent = ProjectileComponent.Create(
                sourceEntityId: source.Id,
                targetEntityId: target.Id,
                damage: damage,
                speed: new zfloat(10), // 弹道速度10米/秒（调慢以便观察）
                targetPosition: targetPos,
                isHoming: true, // 追踪型导弹
                sourceCampId: sourceCampId
            );
            ComponentManager.AddComponent(projectile, projComponent);

            // 添加速度组件（初始速度朝向目标）
            zVector3 direction = (targetPos - sourcePos).normalized;
            ComponentManager.AddComponent(projectile, new VelocityComponent(direction * projComponent.Speed));

            // 添加UnitComponent（提供视图标识信息）
            ComponentManager.AddComponent(projectile, new UnitComponent
            {
                UnitType = 100, // 100=弹道类型
                PrefabId = 9,   // 弹道预制体ID
                PlayerId = sourceCampId,
                MoveSpeed = projComponent.Speed
            });

            // 添加Camp组件（继承发射者阵营，便于统一查询和碰撞检测）
            ComponentManager.AddComponent(projectile, CampComponent.Create(sourceCampId));

            // 发布事件通知表现层创建弹道视图
            World.EventManager.Publish(new UnitCreatedEvent
            {
                EntityId = projectile.Id,
                UnitType = (int)UnitType.Projectile, // 100表示弹道类型
                Position = sourcePos,
                PlayerId = sourceCampId,
                PrefabId = 7 // 弹道预制体ID
            });

            zUDebug.Log($"[CombatSystem] 发射弹道Entity_{projectile.Id}: {source.Id} -> {target.Id}, 伤害{damage}");
        }
    }
}