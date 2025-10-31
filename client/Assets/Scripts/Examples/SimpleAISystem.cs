using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Flow;
using zUnity;
using Game.Examples;

namespace Game.Examples
{
    /// <summary>
    /// 简单AI系统
    /// 控制AI阵营（阵营ID=1）的所有单位
    /// 策略：
    /// 1. 自动移动到玩家基地附近
    /// 2. 战斗由CombatSystem自动处理
    /// 3. 定期重新评估目标
    /// </summary>
    public class SimpleAISystem : BaseSystem
    {
        /// <summary>
        /// AI阵营ID
        /// </summary>
        private const int AI_CAMP_ID = 1;

        /// <summary>
        /// 玩家阵营ID
        /// </summary>
        private const int PLAYER_CAMP_ID = 0;

        /// <summary>
        /// 重新评估目标的间隔（秒）
        /// </summary>
        private zfloat _evaluationInterval = new zfloat(3);

        /// <summary>
        /// 距离上次评估的时间
        /// </summary>
        private zfloat _timeSinceLastEvaluation = zfloat.Zero;

        /// <summary>
        /// 导航系统引用
        /// </summary>
        private FlowFieldNavigationSystem _navSystem;

        /// <summary>
        /// 玩家基地位置（缓存）
        /// </summary>
        private zVector2 _playerBasePosition = zVector2.zero;

        /// <summary>
        /// 是否已找到玩家基地
        /// </summary>
        private bool _hasFoundPlayerBase = false;

        /// <summary>
        /// 初始化AI系统
        /// </summary>
        public void Initialize(FlowFieldNavigationSystem navSystem)
        {
            _navSystem = navSystem;
        }

        public override void Update()
        {
            if (_navSystem == null)
            {
                zUDebug.LogWarning("[SimpleAISystem] 导航系统未初始化");
                return;
            }

            // 更新评估计时器
            _timeSinceLastEvaluation += DeltaTime;

            // 如果还没找到玩家基地，尝试查找
            if (!_hasFoundPlayerBase)
            {
                FindPlayerBase();
            }

            // 定期重新评估AI单位的行为
            if (_timeSinceLastEvaluation >= _evaluationInterval)
            {
                EvaluateAIUnits();
                _timeSinceLastEvaluation = zfloat.Zero;
            }
        }

        /// <summary>
        /// 查找玩家基地位置
        /// </summary>
        private void FindPlayerBase()
        {
            var buildings = ComponentManager.GetAllEntityIdsWith<BuildingComponent>();

            foreach (var entityId in buildings)
            {
                Entity entity = new Entity(entityId);

                // 检查是否有Camp组件
                if (!ComponentManager.HasComponent<CampComponent>(entity))
                    continue;

                var camp = ComponentManager.GetComponent<CampComponent>(entity);
                var building = ComponentManager.GetComponent<BuildingComponent>(entity);

                // 找到玩家的基地（BuildingType=0）
                if (camp.CampId == PLAYER_CAMP_ID && building.BuildingType == 0)
                {
                    if (ComponentManager.HasComponent<TransformComponent>(entity))
                    {
                        var transform = ComponentManager.GetComponent<TransformComponent>(entity);
                        _playerBasePosition = new zVector2(transform.Position.x, transform.Position.z);
                        _hasFoundPlayerBase = true;

                        zUDebug.Log($"[SimpleAISystem] 找到玩家基地位置: {_playerBasePosition}");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 评估所有AI单位的行为
        /// </summary>
        private void EvaluateAIUnits()
        {
            if (!_hasFoundPlayerBase)
                return;

            var allEntities = ComponentManager.GetAllEntityIdsWith<CampComponent>();
            int commandedCount = 0;

            foreach (var entityId in allEntities)
            {
                Entity entity = new Entity(entityId);

                var camp = ComponentManager.GetComponent<CampComponent>(entity);

                // 只处理AI阵营的单位
                if (camp.CampId != AI_CAMP_ID)
                    continue;

                // 必须是可移动单位（有FlowFieldNavigatorComponent）
                if (!ComponentManager.HasComponent<FlowFieldNavigatorComponent>(entity))
                    continue;

                // 检查是否已有移动目标
                var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

                // 如果已到达目标或没有目标，设置新目标
                if (navigator.HasReachedTarget || !ComponentManager.HasComponent<MoveTargetComponent>(entity))
                {
                    // 设置移动到玩家基地
                    _navSystem.SetMoveTarget(entity, _playerBasePosition);
                    commandedCount++;
                }
            }

            if (commandedCount > 0)
            {
                zUDebug.Log($"[SimpleAISystem] 命令 {commandedCount} 个AI单位进攻玩家基地");
            }
        }
    }
}

