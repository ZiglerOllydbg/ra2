using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.Events;
using ZLockstep.Flow;

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
        public int BuildingType { get; set; }

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

        /// <summary>
        /// 地图管理器引用（用于更新地图）
        /// </summary>
        public IFlowFieldMap MapManager { get; set; }

        /// <summary>
        /// 流场管理器引用（用于标记脏区域）
        /// </summary>
        public FlowFieldManager FlowFieldManager { get; set; }

        public CreateBuildingCommand(int playerId, int buildingType, zVector3 position, 
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
            // 1. 创建建筑实体
            var entity = world.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            world.ComponentManager.AddComponent(entity, new Simulation.ECS.Components.TransformComponent
            {
                Position = Position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one
            });

            // 3. 计算格子坐标
            int gridX = 0, gridY = 0;
            if (MapManager != null)
            {
                MapManager.WorldToGrid(new zVector2(Position.x, Position.z), out gridX, out gridY);
            }

            // 4. 添加建筑组件
            var buildingComponent = Simulation.ECS.Components.BuildingComponent.Create(
                BuildingType, gridX, gridY, Width, Height);
            world.ComponentManager.AddComponent(entity, buildingComponent);

            // 5. 添加阵营组件
            var campComponent = Simulation.ECS.Components.CampComponent.Create(CampId);
            world.ComponentManager.AddComponent(entity, campComponent);

            // 6. 添加生命值组件
            var healthComponent = CreateHealthComponent(BuildingType);
            world.ComponentManager.AddComponent(entity, healthComponent);

            // 7. 如果建筑有攻击能力（如防御塔），添加攻击组件
            if (CanAttack(BuildingType))
            {
                var attackComponent = CreateAttackComponent(BuildingType);
                world.ComponentManager.AddComponent(entity, attackComponent);
            }

            // 8. 更新地图：将建筑占据的格子标记为不可行走
            if (MapManager != null)
            {
                for (int x = gridX; x < gridX + Width && x < MapManager.GetWidth(); x++)
                {
                    for (int y = gridY; y < gridY + Height && y < MapManager.GetHeight(); y++)
                    {
                        MapManager.SetWalkable(x, y, false);
                    }
                }
            }

            // 9. 标记流场为脏（需要重新计算）
            if (FlowFieldManager != null)
            {
                FlowFieldManager.MarkRegionDirty(gridX, gridY, gridX + Width, gridY + Height);
            }

            // 10. 发布事件通知表现层创建视图
            world.EventManager.Publish(new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = BuildingType,
                Position = Position,
                PlayerId = PlayerId,
                PrefabId = PrefabId
            });

            UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 建造了建筑类型{BuildingType} " +
                $"在位置{Position}（格子{gridX},{gridY}），尺寸{Width}x{Height}");
        }

        #region 辅助方法

        private Simulation.ECS.Components.HealthComponent CreateHealthComponent(int buildingType)
        {
            switch (buildingType)
            {
                case 0: // 基地
                    return new Simulation.ECS.Components.HealthComponent((zfloat)1000.0f);
                case 1: // 防御塔
                    return new Simulation.ECS.Components.HealthComponent((zfloat)500.0f);
                default:
                    return new Simulation.ECS.Components.HealthComponent((zfloat)500.0f);
            }
        }

        private Simulation.ECS.Components.AttackComponent CreateAttackComponent(int buildingType)
        {
            switch (buildingType)
            {
                case 1: // 防御塔
                    return new Simulation.ECS.Components.AttackComponent
                    {
                        Damage = (zfloat)40.0f,
                        Range = (zfloat)12.0f,
                        AttackInterval = (zfloat)1.5f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                default:
                    return Simulation.ECS.Components.AttackComponent.CreateDefault();
            }
        }

        private bool CanAttack(int buildingType)
        {
            switch (buildingType)
            {
                case 0: // 基地
                    return false;
                case 1: // 防御塔
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        public override string ToString()
        {
            return $"[CreateBuildingCommand] 阵营{CampId} 建造建筑类型{BuildingType} " +
                $"在位置{Position}（格子坐标），尺寸{Width}x{Height}";
        }
    }
}

