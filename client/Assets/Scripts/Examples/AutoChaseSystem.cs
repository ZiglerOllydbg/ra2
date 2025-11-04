using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Flow;
using zUnity;

namespace Game.Examples
{
    /// <summary>
    /// 自动追击系统
    /// 让所有有攻击能力的单位自动追击视野内的敌人
    /// 
    /// 策略：
    /// 1. 侦测范围为 15 米（大于攻击范围 10 米）
    /// 2. 如果正在攻击目标且目标有效，继续追击该目标
    /// 3. 如果没有目标或目标无效，搜索最近的敌人
    /// 4. 只有在没有移动目标或已到达目标时才重新评估
    /// </summary>
    public class AutoChaseSystem : BaseSystem
    {
        /// <summary>
        /// 侦测范围（米）
        /// </summary>
        private zfloat _detectionRange = new zfloat(15);

        /// <summary>
        /// 重新评估目标的间隔（秒）
        /// </summary>
        private zfloat _evaluationInterval = new zfloat(1);

        /// <summary>
        /// 距离上次评估的时间
        /// </summary>
        private zfloat _timeSinceLastEvaluation = zfloat.Zero;

        /// <summary>
        /// 导航系统引用
        /// </summary>
        private FlowFieldNavigationSystem _navSystem;

        /// <summary>
        /// 初始化系统
        /// </summary>
        public void Initialize(FlowFieldNavigationSystem navSystem)
        {
            _navSystem = navSystem;
        }

        public override void Update()
        {
            if (_navSystem == null)
            {
                zUDebug.LogWarning("[AutoChaseSystem] 导航系统未初始化");
                return;
            }

            // 更新评估计时器
            _timeSinceLastEvaluation += DeltaTime;

            // 定期评估所有单位
            if (_timeSinceLastEvaluation >= _evaluationInterval)
            {
                EvaluateAllUnits();
                _timeSinceLastEvaluation = zfloat.Zero;
            }
        }

        /// <summary>
        /// 评估所有有攻击能力的单位
        /// </summary>
        private void EvaluateAllUnits()
        {
            var attackerEntities = ComponentManager.GetAllEntityIdsWith<AttackComponent>();

            foreach (var entityId in attackerEntities)
            {
                Entity entity = new Entity(entityId);

                // 必须有 Transform、Camp 和 FlowFieldNavigator 组件
                if (!ComponentManager.HasComponent<TransformComponent>(entity) ||
                    !ComponentManager.HasComponent<CampComponent>(entity) ||
                    !ComponentManager.HasComponent<FlowFieldNavigatorComponent>(entity))
                    continue;

                var attack = ComponentManager.GetComponent<AttackComponent>(entity);
                var transform = ComponentManager.GetComponent<TransformComponent>(entity);
                var camp = ComponentManager.GetComponent<CampComponent>(entity);
                var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

                // 1. 检查当前攻击目标是否有效且在侦测范围内
                if (attack.TargetEntityId >= 0 && IsValidChaseTarget(attack.TargetEntityId, camp, transform.Position))
                {
                    // 目标有效，继续追击
                    Entity target = new Entity(attack.TargetEntityId);
                    if (ComponentManager.HasComponent<TransformComponent>(target))
                    {
                        var targetTransform = ComponentManager.GetComponent<TransformComponent>(target);
                        zVector2 targetPos = new zVector2(targetTransform.Position.x, targetTransform.Position.z);
                        
                        // 只有在没有移动目标或已到达目标时才设置新目标
                        if (!ComponentManager.HasComponent<MoveTargetComponent>(entity) || navigator.HasReachedTarget)
                        {
                            _navSystem.SetMoveTarget(entity, targetPos);
                        }
                    }
                    continue;
                }

                // 2. 搜索新的敌人
                int nearestEnemyId = FindNearestEnemy(entity, camp, transform.Position, _detectionRange);
                
                if (nearestEnemyId >= 0)
                {
                    // 找到敌人，追击
                    Entity target = new Entity(nearestEnemyId);
                    if (ComponentManager.HasComponent<TransformComponent>(target))
                    {
                        var targetTransform = ComponentManager.GetComponent<TransformComponent>(target);
                        zVector2 targetPos = new zVector2(targetTransform.Position.x, targetTransform.Position.z);
                        
                        // 只有在没有移动目标或已到达目标时才设置新目标
                        if (!ComponentManager.HasComponent<MoveTargetComponent>(entity) || navigator.HasReachedTarget)
                        {
                            _navSystem.SetMoveTarget(entity, targetPos);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 搜索最近的敌人
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

                // 必须有 Transform 和 Health 组件
                if (!ComponentManager.HasComponent<TransformComponent>(otherEntity) ||
                    !ComponentManager.HasComponent<HealthComponent>(otherEntity))
                    continue;

                // 检查是否还活着
                var health = ComponentManager.GetComponent<HealthComponent>(otherEntity);
                if (health.CurrentHealth <= zfloat.Zero)
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
        /// 检查追击目标是否有效
        /// </summary>
        private bool IsValidChaseTarget(int targetEntityId, CampComponent selfCamp, zVector3 position)
        {
            Entity target = new Entity(targetEntityId);

            // 检查是否存在
            if (!ComponentManager.HasComponent<TransformComponent>(target))
                return false;

            // 检查是否还活着
            if (ComponentManager.HasComponent<HealthComponent>(target))
            {
                var health = ComponentManager.GetComponent<HealthComponent>(target);
                if (health.CurrentHealth <= zfloat.Zero)
                    return false;
            }

            // 检查是否为敌人
            if (ComponentManager.HasComponent<CampComponent>(target))
            {
                var targetCamp = ComponentManager.GetComponent<CampComponent>(target);
                if (!selfCamp.IsEnemy(targetCamp))
                    return false;
            }
            else
            {
                return false;
            }

            // 检查是否在侦测范围内
            var targetTransform = ComponentManager.GetComponent<TransformComponent>(target);
            zfloat distSqr = (targetTransform.Position - position).sqrMagnitude;
            zfloat rangeSqr = _detectionRange * _detectionRange;

            return distSqr <= rangeSqr;
        }
    }
}

