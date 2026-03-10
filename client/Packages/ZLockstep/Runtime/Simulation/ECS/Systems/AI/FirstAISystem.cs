using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Flow;
using zUnity;
using System.Collections.Generic;

namespace ZLockstep.Simulation.ECS.Systems.AI
{
    /// <summary>
    /// 简单 AI 系统
    /// 控制 AI 阵营（阵营 ID=2）的所有单位
    /// 策略：
    /// 1. 优先追逐附近的玩家单位（15 米侦测范围）
    /// 2. 如果附近没有玩家单位，则移动到玩家基地
    /// 3. 战斗由 CombatSystem 和 AutoChaseSystem 自动处理
    /// 4. 每 30 秒生产 10 个动员兵（大兵）
    /// </summary>
    public class FirstAISystem : BaseSystem
    {
        /// <summary>
        /// AI 阵营 ID
        /// </summary>
        private const int AI_CAMP_ID = 2;

        /// <summary>
        /// 玩家阵营 ID
        /// </summary>
        private const int PLAYER_CAMP_ID = 1;

        /// <summary>
        /// 重新评估目标的间隔（秒）
        /// </summary>
        private zfloat _evaluationInterval = new zfloat(3);

        /// <summary>
        /// 距离上次评估的时间
        /// </summary>
        private zfloat _timeSinceLastEvaluation = zfloat.Zero;

        /// <summary>
        /// 侦测范围（米）
        /// </summary>
        private zfloat _detectionRange = new zfloat(15);

        /// <summary>
        /// 导航系统引用
        /// </summary>
        private FlowFieldNavigationSystem _navSystem;

        /// <summary>
        /// 玩家基地位置（缓存）
        /// </summary>
        private zVector2 _playerBasePosition = zVector2.zero;

        // ==================== 生产相关字段 ====================
        
        /// <summary>
        /// 生产间隔时间（秒）
        /// </summary>
        private readonly zfloat _productionInterval = new zfloat(30);
        
        /// <summary>
        /// 距离上次生产的时间
        /// </summary>
        private zfloat _timeSinceLastProduction = zfloat.Zero;
        
        /// <summary>
        /// 兵营建筑类型
        /// </summary>
        private const int BARRACKS_BUILDING_TYPE = 5;
        
        /// <summary>
        /// 动员兵单位类型
        /// </summary>
        private const UnitType INFANTRY_UNIT_TYPE = UnitType.Infantry;
        
        /// <summary>
        /// 每次生产数量
        /// </summary>
        private const int UNITS_PER_PRODUCTION = 10;

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

            // 定期重新评估 AI 单位的行为
            if (_timeSinceLastEvaluation >= _evaluationInterval)
            {
                FindPlayerBuilding();
                EvaluateAIUnits();
                _timeSinceLastEvaluation = zfloat.Zero;
            }
            
            // 更新生产计时器
            _timeSinceLastProduction += DeltaTime;
            
            // 检查是否到达生产时间
            if (_timeSinceLastProduction >= _productionInterval)
            {
                ProduceInfantryPeriodically();
                _timeSinceLastProduction = zfloat.Zero;
            }
        }

        /// <summary>
        /// 查找玩家基地位置
        /// </summary>
        private void FindPlayerBuilding()
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

                // 找到玩家的建筑
                if (camp.CampId == PLAYER_CAMP_ID && ComponentManager.HasComponent<HealthComponent>(entity))
                {
                    if (ComponentManager.HasComponent<TransformComponent>(entity))
                    {
                        var transform = ComponentManager.GetComponent<TransformComponent>(entity);
                        _playerBasePosition = new zVector2(transform.Position.x, transform.Position.z);

                        // zUDebug.Log($"[SimpleAISystem] 找到玩家建筑 {building.BuildingType} 位置: {_playerBasePosition}");
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
            var allEntities = ComponentManager.GetAllEntityIdsWith<CampComponent>();
            int chaseCount = 0;
            int moveToBaseCount = 0;

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

                // 必须有Transform组件
                if (!ComponentManager.HasComponent<TransformComponent>(entity))
                    continue;

                // 检查是否已有移动目标
                var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
                var transform = ComponentManager.GetComponent<TransformComponent>(entity);

                // 如果已到达目标或没有目标，重新评估
                if (navigator.HasReachedTarget || !ComponentManager.HasComponent<MoveTargetComponent>(entity))
                {
                    // 1. 优先搜索附近的玩家单位
                    int nearestPlayerUnitId = FindNearestPlayerUnit(transform.Position);
                    
                    if (nearestPlayerUnitId >= 0)
                    {
                        // 找到玩家单位，追击
                        Entity target = new Entity(nearestPlayerUnitId);
                        var targetTransform = ComponentManager.GetComponent<TransformComponent>(target);
                        zVector2 targetPos = new zVector2(targetTransform.Position.x, targetTransform.Position.z);
                        _navSystem.SetMoveTarget(entity, targetPos);
                        chaseCount++;
                    }
                    else
                    {
                        // 没有找到玩家单位，移动到玩家基地
                        _navSystem.SetMoveTarget(entity, _playerBasePosition);
                        moveToBaseCount++;
                    }
                }
            }

            if (chaseCount > 0 || moveToBaseCount > 0)
            {
                zUDebug.Log($"[SimpleAISystem] 追击玩家单位: {chaseCount}，移动到基地: {moveToBaseCount}");
            }
        }

        /// <summary>
        /// 搜索最近的玩家单位
        /// </summary>
        private int FindNearestPlayerUnit(zVector3 position)
        {
            int nearestUnitId = -1;
            zfloat minDistSqr = _detectionRange * _detectionRange;

            var allEntities = ComponentManager.GetAllEntityIdsWith<CampComponent>();

            foreach (var entityId in allEntities)
            {
                Entity otherEntity = new Entity(entityId);

                // 检查是否为玩家阵营
                var otherCamp = ComponentManager.GetComponent<CampComponent>(otherEntity);
                if (otherCamp.CampId != PLAYER_CAMP_ID)
                    continue;

                // 跳过建筑，只追单位
                if (ComponentManager.HasComponent<BuildingComponent>(otherEntity))
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

                // 找到最近的单位
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    nearestUnitId = entityId;
                }
            }

            return nearestUnitId;
        }

        // ==================== 生产相关方法 ====================

        /// <summary>
        /// 定期生产动员兵（每 30 秒生产 10 个）
        /// </summary>
        private void ProduceInfantryPeriodically()
        {
            var barracks = FindAIBarracks();

            if (barracks.Count == 0)
            {
                zUDebug.LogWarning("[FirstAISystem] AI 没有可用的兵营");
                return;
            }

            zUDebug.Log($"[FirstAISystem] 开始生产大兵，找到 {barracks.Count} 个可用兵营");

            // 平均分配到每个兵营
            int unitsPerBarracks = UNITS_PER_PRODUCTION / barracks.Count;
            int remainingUnits = UNITS_PER_PRODUCTION % barracks.Count;

            for (int i = 0; i < barracks.Count; i++)
            {
                int barracksId = barracks[i];
                int unitsToProduce = unitsPerBarracks;

                // 余数分配给前面的兵营
                if (i < remainingUnits)
                    unitsToProduce++;

                if (unitsToProduce > 0)
                {
                    SendProduceCommand(barracksId, unitsToProduce);
                }
            }

            zUDebug.Log($"[FirstAISystem] 生产命令已发送，总计 {UNITS_PER_PRODUCTION} 个大兵");
        }

        /// <summary>
        /// 查找 AI 阵营的所有可用兵营建筑
        /// </summary>
        /// <returns>兵营实体 ID 列表</returns>
        private List<int> FindAIBarracks()
        {
            // 获取所有建筑
            var buildings = ComponentManager.GetAllEntityIdsWith<BuildingComponent>();

            List<int> barracksList = new List<int>();

            foreach (var entityId in buildings)
            {
                Entity entity = new Entity(entityId);

                // 检查是否有 Camp 组件
                if (!ComponentManager.HasComponent<CampComponent>(entity))
                    continue;

                var camp = ComponentManager.GetComponent<CampComponent>(entity);

                // 只检查 AI 阵营
                if (camp.CampId != AI_CAMP_ID)
                    continue;

                // 检查建筑类型是否为兵营
                var building = ComponentManager.GetComponent<BuildingComponent>(entity);
                if (building.BuildingType != BARRACKS_BUILDING_TYPE)
                    continue;

                // 检查是否有 ProduceComponent
                if (!ComponentManager.HasComponent<ProduceComponent>(entity))
                    continue;

                // 检查是否支持生产大兵
                var produce = ComponentManager.GetComponent<ProduceComponent>(entity);
                if (!produce.SupportedUnitTypes.Contains(INFANTRY_UNIT_TYPE))
                    continue;

                barracksList.Add(entityId);
            }

            return barracksList;
        }

        /// <summary>
        /// 发送生产命令
        /// </summary>
        /// <param name="barracksId">兵营实体 ID</param>
        /// <param name="count">生产数量</param>
        private void SendProduceCommand(int barracksId, int count)
        {
            // 获取 Game 实例
            var game = World.GameInstance;
            if (game == null)
            {
                zUDebug.LogError("[FirstAISystem] 无法获取 Game 实例，无法发送生产命令");
                return;
            }

            // 创建生产命令
            var command = new ZLockstep.Sync.Command.Commands.ProduceCommand(
                campId: AI_CAMP_ID,
                entityId: barracksId,
                unitType: INFANTRY_UNIT_TYPE,
                changeValue: count
            );

            // 提交命令
            game.SubmitCommand(command);

            zUDebug.Log($"[FirstAISystem] 发送生产命令：兵营{barracksId} 生产{count}个大兵");
        }
    }
}

