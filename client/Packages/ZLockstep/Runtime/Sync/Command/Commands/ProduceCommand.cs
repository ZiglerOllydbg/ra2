using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using System.Text;

namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 生产命令
    /// 用于增加或减少特定单位类型的生产数量
    /// </summary>
    [CommandType(CommandTypes.Produce)]
    public class ProduceCommand : BaseCommand
    {
        /// <summary>
        /// 建筑实体ID
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// 单位类型
        /// </summary>
        public int UnitType { get; set; }

        /// <summary>
        /// 变化值 (+1 或 -1)
        /// </summary>
        public int ChangeValue { get; set; }

        public ProduceCommand(int playerId, int entityId, int unitType, int changeValue)
            : base(playerId)
        {
            EntityId = entityId;
            UnitType = unitType;
            ChangeValue = changeValue;
        }

        public override void Execute(zWorld world)
        {
            var entity = new Entity(EntityId);
            
            // 检查实体是否存在且具有ProduceComponent组件
            if (!world.ComponentManager.HasComponent<ProduceComponent>(entity))
            {
                UnityEngine.Debug.LogWarning($"[ProduceCommand] 实体 {EntityId} 不具有 ProduceComponent 组件");
                return;
            }

            var produceComponent = world.ComponentManager.GetComponent<ProduceComponent>(entity);
            
            // 检查单位类型是否支持生产
            if (!produceComponent.SupportedUnitTypes.Contains(UnitType))
            {
                UnityEngine.Debug.LogWarning($"[ProduceCommand] 实体 {EntityId} 不支持生产单位类型 {UnitType}");
                return;
            }

            // 更新生产数量
            if (produceComponent.ProduceNumbers.ContainsKey(UnitType))
            {
                int currentNumber = produceComponent.ProduceNumbers[UnitType];
                int newNumber = currentNumber + ChangeValue;
                
                // 限制在0-99范围内
                newNumber = UnityEngine.Mathf.Clamp(newNumber, 0, 99);
                
                produceComponent.ProduceNumbers[UnitType] = newNumber;
                
                // 更新组件
                world.ComponentManager.AddComponent(entity, produceComponent);
                
                zUDebug.Log($"[ProduceCommand] 实体 {EntityId} 的单位类型 {UnitType} 生产数量更新为 {newNumber}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ProduceCommand] 实体 {EntityId} 的生产组件中未找到单位类型 {UnitType}");
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"[ProduceCommand] 玩家{PlayerId} 修改实体{EntityId}的单位类型{UnitType}生产数量，变化值{ChangeValue}");
            return sb.ToString();
        }
    }
}