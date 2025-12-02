using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.Events;
using ZLockstep.Flow;
using ZLockstep.Simulation.ECS;

namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 创建建筑命令
    /// 用于在地图上建造静态建筑（基地、防御塔等）
    /// </summary>
    [CommandType(CommandTypes.BuildStructure)]
    public class CreateBuildingCommand : BaseCommand
    {


        /// <summary>
        /// 建筑类型
        /// 0=基地, 1=防御塔等
        /// </summary>
        public BuildingType BuildingType { get; set; }

        /// <summary>
        /// 建筑位置（世界坐标）
        /// </summary>
        public zVector3 Position { get; set; }

        /// <summary>
        /// 建筑占据的格子数（宽度和高度）
        /// </summary>
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// 阵营ID
        /// </summary>
        public int CampId { get; set; }

        /// <summary>
        /// 预制体ID（用于表现层）
        /// </summary>
        public int PrefabId { get; set; }

        public CreateBuildingCommand(int playerId, BuildingType buildingType, zVector3 position, 
            int width, int height, int campId, int prefabId)
            : base(playerId)
        {
            BuildingType = buildingType;
            Position = position;
            Width = width;
            Height = height;
            CampId = campId;
            PrefabId = prefabId;
        }

        public override void Execute(zWorld world)
        {
            // 使用EntityCreationManager创建建筑实体
            var unitCreatedEvent = EntityCreationManager.CreateBuildingEntity(
                world, 
                PlayerId, 
                BuildingType, 
                Position, 
                Width, 
                Height, 
                PrefabId,
                world.GameInstance.GetMapManager(),
                world.GameInstance.GetFlowFieldManager()
            );

            // 发布事件通知表现层创建视图
            if (unitCreatedEvent.HasValue)
            {
                world.EventManager.Publish(unitCreatedEvent.Value);
            }

            UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 建造了建筑类型{BuildingType} " +
                $"在位置{Position}，尺寸{Width}x{Height}");
        }
        public override string ToString()
        {
            return $"[CreateBuildingCommand] 阵营{CampId} 建造建筑类型{BuildingType} " +
                $"在位置{Position}（格子坐标），尺寸{Width}x{Height}";
        }
    }
}