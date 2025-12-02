using zUnity;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using ZLockstep.Flow;
using System.Collections.Generic;

namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 建筑类型枚举
    /// </summary>
    public enum BuildingType
    {
        None = 0,
        Base = 1,        // 基地

        Mine = 2,        // 矿
        Smelter = 3,    // 冶金厂
         
        PowerPlant = 4,   // 电厂
        Factory = 5,     // 工厂/防御塔
    }

    /// <summary>
    /// 单位类型枚举
    /// </summary>
    public enum UnitType
    {
        Infantry = 1,    // 动员兵
        Tank = 2,        // 坦克
        Harvester = 3    // 矿车
    }

    /// <summary>
    /// 实体创建管理器
    /// 负责统一管理游戏中各种实体（建筑、单位等）的创建逻辑
    /// </summary>
    public class EntityCreationManager
    {
        /// <summary>
        /// 创建建筑实体（创世阶段使用）
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="buildingType">建筑类型</param>
        /// <param name="position">位置</param>
        /// <param name="width">建筑宽度</param>
        /// <param name="height">建筑高度</param>
        /// <param name="prefabId">预制体ID</param>
        /// <param name="mapManager">地图管理器（可选）</param>
        /// <param name="flowFieldManager">流场管理器（可选）</param>
        /// <returns>创建的实体事件</returns>
        public static UnitCreatedEvent? CreateBuildingEntity(
            zWorld world, 
            int playerId, 
            BuildingType buildingType, 
            zVector3 position, 
            int width, 
            int height, 
            int prefabId,
            IFlowFieldMap mapManager = null,
            FlowFieldManager flowFieldManager = null)
        {
            // 1. 创建建筑实体
            var entity = world.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            world.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one
            });

            // 3. 计算格子坐标
            int gridX = 0, gridY = 0;
            if (mapManager != null)
            {
                mapManager.WorldToGrid(new zVector2(position.x, position.z), out gridX, out gridY);
            }

            // 4. 添加建筑组件
            var buildingComponent = BuildingComponent.Create((int)buildingType, gridX, gridY, width, height);
            world.ComponentManager.AddComponent(entity, buildingComponent);

            // 5. 添加阵营组件
            var campComponent = CampComponent.Create(playerId);
            world.ComponentManager.AddComponent(entity, campComponent);

            // 6. 添加生命值组件
            var healthComponent = CreateBuildingHealthComponent((int)buildingType);
            world.ComponentManager.AddComponent(entity, healthComponent);

            // 7. 如果建筑有攻击能力（如防御塔），添加攻击组件
            if (CanBuildingAttack((int)buildingType))
            {
                var attackComponent = CreateBuildingAttackComponent((int)buildingType);
                world.ComponentManager.AddComponent(entity, attackComponent);
            }

            // 8. 更新地图：将建筑占据的格子标记为不可行走
            if (mapManager != null)
            {
                mapManager.SetWalkableRect(gridX - width / 2, gridY - height / 2, gridX + width / 2, gridY + height / 2, false);
            }

            // 9. 标记流场为脏（需要重新计算）
            if (flowFieldManager != null)
            {
                flowFieldManager.MarkRegionDirty(gridX, gridY, gridX + width, gridY + height);
            }

            // 判断是本地玩家，添加本地玩家组件
            if (world.ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            {
                var globalInfoComponent = world.ComponentManager.GetGlobalComponent<GlobalInfoComponent>();
                if (globalInfoComponent.LocalPlayerCampId == playerId)
                {
                    world.ComponentManager.AddComponent(entity, new LocalPlayerComponent());
                }
            }

            // 10. 如果是工厂建筑，添加生产组件
            if (buildingType == BuildingType.Factory) // 工厂建筑
            {
                // 支持生产动员兵和坦克
                var supportedUnitTypes = new HashSet<UnitType> { UnitType.Tank }; // 1=动员兵, 2=坦克
                var produceComponent = ProduceComponent.Create(supportedUnitTypes);
                world.ComponentManager.AddComponent(entity, produceComponent);
            }

            // 11. 创建事件对象并返回
            var unitCreatedEvent = new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = (int)buildingType,
                Position = position,
                PlayerId = playerId,
                PrefabId = prefabId
            };

            UnityEngine.Debug.Log($"[EntityCreationManager] 玩家{playerId} 创建了建筑类型{buildingType} " +
                $"在位置{position}（格子{gridX},{gridY}），尺寸{width}x{height}，Entity ID: {entity.Id}");
            
            return unitCreatedEvent;
        }

        /// <summary>
        /// 创建单位实体（创世阶段使用）
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="unitType">单位类型</param>
        /// <param name="position">位置</param>
        /// <param name="prefabId">预制体ID</param>
        /// <returns>创建的实体事件</returns>
        public static UnitCreatedEvent CreateUnitEntity(
            zWorld world,
            int playerId,
            UnitType unitType,
            zVector3 position,
            int prefabId,
            FlowFieldNavigationSystem navSystem)
        {
            // 根据单位类型确定单位参数
            zfloat radius = (zfloat)2;
            zfloat maxSpeed = (zfloat)3.0f;
            
            switch (unitType)
            {
                case UnitType.Tank: // 坦克
                    radius = (zfloat)2;
                    maxSpeed = (zfloat)6.0f;
                    break;
                default: // 默认为动员兵或其他单位
                    radius = (zfloat)2;
                    maxSpeed = (zfloat)3.0f;
                    break;
            }

            // 1. 创建单位实体
            var entity = world.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            world.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one * radius
            });

            // 3. 添加阵营组件
            var camp = CampComponent.Create(playerId);
            world.ComponentManager.AddComponent(entity, camp);

            // 4. 添加Unit组件（根据类型）
            var unitComponent = CreateUnitComponent((int)unitType, playerId);
            unitComponent.PrefabId = prefabId;
            unitComponent.MoveSpeed = maxSpeed;
            world.ComponentManager.AddComponent(entity, unitComponent);

            // 5. 添加Health组件
            var healthComponent = CreateUnitHealthComponent((int)unitType);
            world.ComponentManager.AddComponent(entity, healthComponent);

            // 6. 如果单位有攻击能力，添加Attack组件
            if (CanUnitAttack((int)unitType))
            {
                var attackComponent = CreateUnitAttackComponent((int)unitType);
                world.ComponentManager.AddComponent(entity, attackComponent);
            }

            // 7. 添加速度组件
            world.ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));

            // 8. 添加导航能力
            // 注意：NavSystem需要在外部添加，因为我们可能没有NavSystem的引用
            navSystem.AddNavigator(entity, radius, maxSpeed);

            // 9. 添加旋转相关组件
            // 根据单位类型添加不同的载具类型组件
            VehicleTypeComponent vehicleType;
            if (unitType == UnitType.Infantry) // 动员兵
            {
                vehicleType = VehicleTypeComponent.CreateInfantry();
            }
            else if (unitType == UnitType.Tank) // 坦克
            {
                vehicleType = VehicleTypeComponent.CreateHeavyTank();
            }
            else
            {
                vehicleType = VehicleTypeComponent.CreateInfantry(); // 默认
            }
            world.ComponentManager.AddComponent(entity, vehicleType);

            // 添加旋转状态组件
            var rotationState = RotationStateComponent.Create();
            world.ComponentManager.AddComponent(entity, rotationState);

            // 如果有炮塔，添加炮塔组件
            if (vehicleType.HasTurret)
            {
                var turret = TurretComponent.CreateDefault();
                world.ComponentManager.AddComponent(entity, turret);
            }

            // 判断是本地玩家，添加本地玩家组件
            if (world.ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            {
                var globalInfoComponent = world.ComponentManager.GetGlobalComponent<GlobalInfoComponent>();
                if (globalInfoComponent.LocalPlayerCampId == playerId)
                {
                    world.ComponentManager.AddComponent(entity, new LocalPlayerComponent());
                }
            }

            // 10. 创建事件对象并返回
            var unitCreatedEvent = new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = (int)unitType,
                Position = position,
                PlayerId = playerId,
                PrefabId = prefabId
            };

            UnityEngine.Debug.Log($"[EntityCreationManager] 玩家{playerId} 创建了单位类型{unitType} 在位置{position}，Entity ID: {entity.Id}");
            
            return unitCreatedEvent;
        }

        #region 建筑辅助方法

        private static HealthComponent CreateBuildingHealthComponent(int buildingType)
        {
            switch (buildingType)
            {
                case 0: // 基地
                    return new HealthComponent((zfloat)1000.0f);
                case 1: // 防御塔/工厂
                    return new HealthComponent((zfloat)500.0f);
                case 2: // 采矿场
                    return new HealthComponent((zfloat)400.0f);
                case 3: // 电厂
                    return new HealthComponent((zfloat)300.0f);
                default:
                    return new HealthComponent((zfloat)500.0f);
            }
        }

        private static AttackComponent CreateBuildingAttackComponent(int buildingType)
        {
            switch (buildingType)
            {
                case 1: // 防御塔
                    return new AttackComponent
                    {
                        Damage = (zfloat)40.0f,
                        Range = (zfloat)12.0f,
                        AttackInterval = (zfloat)1.5f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                default:
                    return AttackComponent.CreateDefault();
            }
        }

        private static bool CanBuildingAttack(int buildingType)
        {
            // 只有防御塔类型的建筑可以攻击
            return buildingType == 1;
        }

        #endregion

        #region 单位辅助方法

        private static UnitComponent CreateUnitComponent(int unitType, int playerId)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                    return UnitComponent.CreateInfantry(playerId);
                case 2: // 犀牛坦克
                    return UnitComponent.CreateTank(playerId);
                case 3: // 矿车
                    return UnitComponent.CreateHarvester(playerId);
                default:
                    return UnitComponent.CreateInfantry(playerId);
            }
        }

        private static HealthComponent CreateUnitHealthComponent(int unitType)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                    return new HealthComponent((zfloat)50.0f);
                case 2: // 犀牛坦克
                    return new HealthComponent((zfloat)200.0f);
                case 3: // 矿车
                    return new HealthComponent((zfloat)150.0f);
                default:
                    return new HealthComponent((zfloat)100.0f);
            }
        }

        private static AttackComponent CreateUnitAttackComponent(int unitType)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                    return new AttackComponent
                    {
                        Damage = (zfloat)10.0f,
                        Range = (zfloat)4.0f,
                        AttackInterval = (zfloat)1.0f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                case 2: // 犀牛坦克
                    return new AttackComponent
                    {
                        Damage = (zfloat)30.0f,
                        Range = (zfloat)10.0f,
                        AttackInterval = (zfloat)2.0f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                default:
                    return AttackComponent.CreateDefault();
            }
        }

        private static bool CanUnitAttack(int unitType)
        {
            // 动员兵和坦克有攻击能力
            return unitType == 1 || unitType == 2;
        }

        #endregion
    }
}