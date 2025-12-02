using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.Events;
using ZLockstep.Flow;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;

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
            // 检查并扣除资源
            if (!CheckAndDeductResources(world))
            {
                UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 资源不足，无法建造建筑类型{BuildingType}");
                return;
            }

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

        /// <summary>
        /// 检查并扣除建造建筑所需的资源
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <returns>是否有足够的资源</returns>
        private bool CheckAndDeductResources(zWorld world)
        {
            // 获取玩家的经济组件
            var economyEntities = world.ComponentManager.GetAllEntityIdsWith<EconomyComponent>();
            EconomyComponent economyComponent = new EconomyComponent();
            bool found = false;
            int economyEntityId = -1;
            
            // 查找属于当前玩家的经济组件
            foreach (var entityId in economyEntities)
            {
                var entity = new Entity(entityId);
                if (world.ComponentManager.HasComponent<CampComponent>(entity))
                {
                    var campComponent = world.ComponentManager.GetComponent<CampComponent>(entity);
                    if (campComponent.CampId == CampId)
                    {
                        economyComponent = world.ComponentManager.GetComponent<EconomyComponent>(entity);
                        economyEntityId = entityId;
                        found = true;
                        break;
                    }
                }
            }
            
            if (!found)
            {
                UnityEngine.Debug.LogWarning($"[CreateBuildingCommand] 未找到阵营 {CampId} 的经济组件");
                return false;
            }
            
            // 根据建筑类型确定所需资源
            int costMoney = 0;
            int costPower = 0;
            
            switch (BuildingType)
            {
                case BuildingType.PowerPlant: // 发电厂
                    costMoney = 500;
                    costPower = -10;
                    break;
                case BuildingType.Smelter: // 采矿场
                    costMoney = 800;
                    costPower = 5;
                    break;
                case BuildingType.Factory: // 坦克工厂
                    costMoney = 1000;
                    costPower = 5;
                    break;
                default:
                    // 其他建筑不消耗资源或免费
                    return true;
            }
            
            // 检查是否有足够的资源
            if (economyComponent.Money < costMoney)
            {
                UnityEngine.Debug.Log($"[CreateBuildingCommand] 资金不足。需要: {costMoney}, 当前: {economyComponent.Money}");
                return false;
            }
            
            // 扣除资源
            economyComponent.Money -= costMoney;
            economyComponent.Power -= costPower; // 增加电力供应
            
            // 更新经济组件
            var economyEntity = new Entity(economyEntityId);
            world.ComponentManager.AddComponent(economyEntity, economyComponent);
            
            UnityEngine.Debug.Log($"[CreateBuildingCommand] 扣除资源成功。花费资金: {costMoney}, 获得电力: {costPower}。剩余资金: {economyComponent.Money}, 总电力: {economyComponent.Power}");
            return true;
        }

        public override string ToString()
        {
            return $"[CreateBuildingCommand] 阵营{CampId} 建造建筑类型{BuildingType} " +
                $"在位置{Position}（格子坐标），尺寸{Width}x{Height}";
        }
    }
}