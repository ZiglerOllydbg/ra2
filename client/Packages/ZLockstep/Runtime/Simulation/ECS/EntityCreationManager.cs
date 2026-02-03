using zUnity;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using ZLockstep.Flow;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Utils;

namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 建筑类型枚举
    /// </summary>
    public enum BuildingType
    {
        None = 0,
        Base = 1,       // 基地
        Mine = 2,       // 矿
        Smelter = 3,    // 冶金厂
        PowerPlant = 4, // 电厂
        Barracks = 5,   // 兵营
        vehicleFactory = 6, // 战车工厂
        Tower = 7,      // 防御塔
    }

    /// <summary>
    /// 单位类型枚举
    /// </summary>
    public enum UnitType
    {
        Tank = 1,        // 坦克
        Infantry = 2,    // 动员兵
        Harvester = 3,    // 矿车
        
        Projectile = 100,   // 弹丸
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
        /// <param name="campId">玩家ID</param>
        /// <param name="buildingType">建筑类型</param>
        /// <param name="position">位置</param>
        /// <param name="confBuildingID">建筑配置表ID</param>
        /// <param name="mapManager">地图管理器（可选）</param>
        /// <param name="flowFieldManager">流场管理器（可选）</param>
        /// <returns>创建的实体事件</returns>
        public static UnitCreatedEvent? CreateBuildingEntity(
            zWorld world, 
            int campId, 
            BuildingType buildingType, 
            zVector3 position, 
            int confBuildingID,
            IFlowFieldMap mapManager = null,
            FlowFieldManager flowFieldManager = null)
        {
            var confBuilding = DataManager.Get<ConfBuilding>(confBuildingID.ToString());
            if (confBuilding == null)
            {
                zUDebug.LogError($"[EntityCreationManager] 创建建筑实体时无法获取建筑配置信息。ID:{confBuildingID}");
                return null;
            }

            int width = confBuilding.Size;
            int height = confBuilding.Size;

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
            var campComponent = CampComponent.Create(campId);
            world.ComponentManager.AddComponent(entity, campComponent);

            // 6. 添加生命值组件
            var healthComponent = CreateBuildingHealthComponent(confBuildingID);
            if (healthComponent.HasValue)
            {
                world.ComponentManager.AddComponent(entity, healthComponent.Value);
            }
            
            // 7. 如果建筑有攻击能力（如防御塔），添加攻击组件
            var attackComponent = CreateBuildingAttackComponent(confBuildingID);
            if (attackComponent.HasValue)
            {
                world.ComponentManager.AddComponent(entity, attackComponent.Value);
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
                if (globalInfoComponent.LocalPlayerCampId == campId)
                {
                    world.ComponentManager.AddComponent(entity, new LocalPlayerComponent());
                }
            }

            // 10. 如果是基地建筑，添加主基地组件
            if (buildingType == BuildingType.Base) // 基地建筑
            {
                world.ComponentManager.AddComponent(entity, new MainBaseComponent());
            }

            // 11. 如果是工厂建筑，添加生产组件
            if (buildingType == BuildingType.vehicleFactory) // 工厂建筑
            {
                // 支持生产动员兵和坦克
                var supportedUnitTypes = new HashSet<UnitType> { UnitType.Tank }; // 1=动员兵, 2=坦克
                var produceComponent = ProduceComponent.Create(supportedUnitTypes);
                world.ComponentManager.AddComponent(entity, produceComponent);
            }
            // 12. 如果是矿源，添加矿源组件
            else if (buildingType == BuildingType.Mine) // 矿源
            {
                // 添加矿源组件，初始资源5000
                var mineComponent = MineComponent.CreateDefault();
                world.ComponentManager.AddComponent(entity, mineComponent);
            }
            // 13. 如果是采矿场，添加采矿组件
            else if (buildingType == BuildingType.Smelter) // 采矿场
            {
                // 查找最近的矿源并关联
                int nearestMineEntityId = FindNearestMine(world, position);
                if (nearestMineEntityId != -1)
                {
                    // 添加采矿组件，关联到最近的矿源
                    var miningComponent = MiningComponent.Create(nearestMineEntityId);
                    world.ComponentManager.AddComponent(entity, miningComponent);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[EntityCreationManager] 未能找到附近的矿源，采矿场实体 {entity.Id} 将不会进行采矿");
                }
            }

            // 14. 添加建造组件（所有建筑都需要建造时间）
            var constructionTime = GetConstructionTime(confBuildingID);
            if (constructionTime > zfloat.Zero)
            {
                var buildingConstructionComponent = BuildingConstructionComponent.Create(constructionTime);
                world.ComponentManager.AddComponent(entity, buildingConstructionComponent);
            }

            // 15. 创建事件对象并返回
            var unitCreatedEvent = new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = (int)buildingType,
                ConfID = confBuildingID,
                Position = position,
                PlayerId = campId,
                PrefabId = confBuildingID
            };

            UnityEngine.Debug.Log($"[EntityCreationManager] 玩家{campId} 创建了建筑类型{buildingType} " +
                $"在位置{position}（格子{gridX},{gridY}），尺寸{width}x{height}，Entity ID: {entity.Id}");
            
            return unitCreatedEvent;
        }

        /// <summary>
        /// 创建单位实体（创世阶段使用）
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <param name="campId">玩家ID</param>
        /// <param name="unitType">单位类型</param>
        /// <param name="position">位置</param>
        /// <param name="prefabId">预制体ID</param>
        /// <returns>创建的实体事件</returns>
        public static UnitCreatedEvent? CreateUnitEntity(
            zWorld world,
            int campId,
            UnitType unitType,
            zVector3 position,
            int prefabId)
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
            var camp = CampComponent.Create(campId);
            world.ComponentManager.AddComponent(entity, camp);

            // 4. 添加Unit组件（根据类型）
            var unitComponent = CreateUnitComponent(unitType, campId);
            unitComponent.PrefabId = prefabId;
            unitComponent.MoveSpeed = maxSpeed;
            world.ComponentManager.AddComponent(entity, unitComponent);

            // 5. 添加Health组件
            var healthComponent = CreateUnitHealthComponent(unitType);
            world.ComponentManager.AddComponent(entity, healthComponent);

            // 6. 如果单位有攻击能力，添加Attack组件
            var attackComponent = CreateUnitAttackComponent(unitType);
            if (attackComponent.HasValue)
            {
                world.ComponentManager.AddComponent(entity, attackComponent.Value);
            }

            // 7. 添加速度组件
            world.ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));

            // 8. 添加导航能力
            // 注意：NavSystem需要在外部添加，因为我们可能没有NavSystem的引用
            world.GameInstance.GetNavSystem().AddNavigator(entity, radius, maxSpeed);

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
                if (globalInfoComponent.LocalPlayerCampId == campId)
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
                PlayerId = campId,
                PrefabId = prefabId
            };

            UnityEngine.Debug.Log($"[EntityCreationManager] 玩家{campId} 创建了单位类型{unitType} 在位置{position}，Entity ID: {entity.Id}");
            
            return unitCreatedEvent;
        }

        #region 建筑辅助方法

        private static HealthComponent? CreateBuildingHealthComponent(int confBuildingID)
        {
            var confBuilding = DataManager.Get<ConfBuilding>(confBuildingID.ToString());
            if (confBuilding == null)
            {
                zUDebug.LogError($"[EntityCreationManager] 创建建筑时无法获取建筑配置信息。ID:{confBuildingID}");
                return null;
            }

            if (confBuilding.Hp > 0)
            {
                return new HealthComponent((zfloat)confBuilding.Hp);
            }
            else
            {
                return null;
            }
        }

        private static AttackComponent? CreateBuildingAttackComponent(int confBuildingID)
        {
            var confBuilding = DataManager.Get<ConfBuilding>(confBuildingID.ToString());
            if (confBuilding == null)
            {
                zUDebug.LogError($"[EntityCreationManager] 创建建筑时无法获取建筑配置信息。ID:{confBuildingID}");
                return null;
            }

            if (confBuilding.Type == (int)BuildingType.Tower)
            {
                return new AttackComponent
                {
                    Damage = (zfloat)confBuilding.Atk,
                    Range = (zfloat)12.0f,
                    AttackInterval = (zfloat)1.5f,
                    TimeSinceLastAttack = zfloat.Zero,
                    TargetEntityId = -1
                };
            }

            return null;
        }

        #endregion

        #region 单位辅助方法

        private static UnitComponent CreateUnitComponent(UnitType unitType, int campId)
        {
            switch (unitType)
            {
                case UnitType.Infantry: // 动员兵
                    return UnitComponent.CreateInfantry(campId);
                case UnitType.Tank: // 犀牛坦克
                    return UnitComponent.CreateTank(campId);
                case UnitType.Harvester: // 矿车
                    return UnitComponent.CreateHarvester(campId);
                default:
                    return UnitComponent.CreateInfantry(campId);
            }
        }

        private static HealthComponent CreateUnitHealthComponent(UnitType unitType)
        {
            switch (unitType)
            {
                case UnitType.Infantry: // 动员兵
                    return new HealthComponent((zfloat)50.0f);
                case UnitType.Tank: // 犀牛坦克
                    return new HealthComponent((zfloat)200.0f);
                case UnitType.Harvester: // 矿车
                    return new HealthComponent((zfloat)150.0f);
                default:
                    return new HealthComponent((zfloat)100.0f);
            }
        }

        private static AttackComponent? CreateUnitAttackComponent(UnitType unitType)
        {
            switch (unitType)
            {
                case UnitType.Infantry: // 动员兵
                    return new AttackComponent
                    {
                        Damage = (zfloat)10.0f,
                        Range = (zfloat)4.0f,
                        AttackInterval = (zfloat)1.0f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                case UnitType.Tank: // 犀牛坦克
                    return new AttackComponent
                    {
                        Damage = (zfloat)30.0f,
                        Range = (zfloat)10.0f,
                        AttackInterval = (zfloat)1.0f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                default:
                    return null;
            }
        }

        #endregion

        #region 采矿辅助方法
        
        /// <summary>
        /// 查找距离指定位置最近的矿源
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <param name="position">当前位置</param>
        /// <returns>最近的矿源实体ID，如果没找到则返回-1</returns>
        private static int FindNearestMine(zWorld world, zVector3 position)
        {
            int nearestMineEntityId = -1;
            zfloat minDistanceSquared = zfloat.Infinity;
            
            // 获取所有具有矿源组件的实体
            var mineEntities = world.ComponentManager.GetAllEntityIdsWith<MineComponent>();
            
            foreach (var entityId in mineEntities)
            {
                var entity = new Entity(entityId);
                
                // 检查实体是否具有Transform组件
                if (world.ComponentManager.HasComponent<TransformComponent>(entity))
                {
                    var transformComponent = world.ComponentManager.GetComponent<TransformComponent>(entity);
                    zVector3 minePosition = transformComponent.Position;
                    
                    // 计算距离的平方（避免开方运算）
                    zVector3 delta = position - minePosition;
                    zfloat distanceSquared = delta.x * delta.x + delta.z * delta.z; // 只考虑水平距离
                    
                    // 检查是否是最近的
                    if (distanceSquared < minDistanceSquared)
                    {
                        minDistanceSquared = distanceSquared;
                        nearestMineEntityId = entityId;
                    }
                }
            }
            
            return nearestMineEntityId;
        }
        
        /// <summary>
        /// 为采矿场分配关联的矿源
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <param name="smelterEntity">采矿场实体</param>
        /// <param name="mineEntityId">矿源实体ID</param>
        public static void AssignMineToSmelter(zWorld world, Entity smelterEntity, int mineEntityId)
        {
            // 添加采矿组件，关联到指定的矿源
            var miningComponent = MiningComponent.Create(mineEntityId);
            world.ComponentManager.AddComponent(smelterEntity, miningComponent);
            
            UnityEngine.Debug.Log($"[EntityCreationManager] 为采矿场实体 {smelterEntity.Id} 分配了矿源实体 {mineEntityId}");
        }
        
        #endregion

        #region 建造辅助方法
        
        /// <summary>
        /// 获取建筑的建造时间
        /// </summary>
        /// <param name="buildingType">建筑类型</param>
        /// <returns>建造时间（秒）</returns>
        private static zfloat GetConstructionTime(int confBuildingID)
        {
            var confBuilding = DataManager.Get<ConfBuilding>(confBuildingID.ToString());
            if (confBuilding == null)
            {
                zUDebug.LogError($"[BuildingPlacementUtils] 获取建筑配置信息失败。ID:{confBuildingID}");
                return zfloat.Zero;
            }

            return (zfloat)confBuilding.ConstructionTime;
        }
        
        #endregion
    }
}