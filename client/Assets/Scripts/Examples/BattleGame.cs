using ZLockstep.Sync;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Sync.Command.Commands;
using ZLockstep.Flow;
using ZLockstep.RVO;
using zUnity;
using ZLockstep.Simulation.ECS;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using ZLockstep.Simulation.Events;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command;

namespace Game.Examples
{
    /// <summary>
    /// 战斗游戏类
    /// 继承自Game，添加战斗相关的系统和管理器
    /// </summary>
    public class BattleGame : ZLockstep.Sync.Game
    {
        /// <summary>
        /// 地图管理器（256x256，每格1米）
        /// </summary>
        public SimpleMapManager MapManager { get; private set; }

        /// <summary>
        /// 流场管理器
        /// </summary>
        public FlowFieldManager FlowFieldManager { get; private set; }

        /// <summary>
        /// RVO模拟器
        /// </summary>
        public RVO2Simulator RvoSimulator { get; private set; }

        /// <summary>
        /// 导航系统
        /// </summary>
        public FlowFieldNavigationSystem NavSystem { get; private set; }

        /// <summary>
        /// AI系统
        /// </summary>
        public SimpleAISystem AISystem { get; private set; }

        public BattleGame(GameMode mode = GameMode.Standalone, int frameRate = 30, int localPlayerId = 0)
            : base(mode, frameRate, localPlayerId)
        {
        }

        /// <summary>
        /// 获取导航系统（重写基类方法）
        /// </summary>
        /// <returns>导航系统实例</returns>
        public override FlowFieldNavigationSystem GetNavSystem()
        {
            return NavSystem; // 返回BattleGame的NavSystem
        }

        /// <summary>
        /// 初始化战斗游戏
        /// </summary>
        public new void Init()
        {
            // 初始化命令映射
            CommandMapper.Initialize();
            // 初始化地图和导航系统
            InitializeMapAndNavigation();
            // 先调用基类初始化
            base.Init();

            zUDebug.Log("[BattleGame] 战斗游戏初始化完成");
        }

        /// <summary>
        /// 初始化地图和导航系统
        /// </summary>
        private void InitializeMapAndNavigation()
        {
            // 1. 创建256x256地图，每格1.0米
            MapManager = new SimpleMapManager();
            MapManager.Initialize(256, 256, zfloat.One);
            zUDebug.Log("[BattleGame] 地图初始化: 256x256格，每格1.0米");

            // 2. 设置地图边界为障碍物
            SetupMapBorders();

            // 3. 创建流场管理器
            FlowFieldManager = new FlowFieldManager();
            FlowFieldManager.Initialize(MapManager, maxFields: 30);
            FlowFieldManager.SetMaxUpdatesPerFrame(2);
            zUDebug.Log("[BattleGame] 流场管理器初始化完成");

            // 4. 创建RVO模拟器
            RvoSimulator = new RVO2Simulator();
            zUDebug.Log("[BattleGame] RVO模拟器初始化完成");
        }

        /// <summary>
        /// 设置地图边界
        /// </summary>
        private void SetupMapBorders()
        {
            // 地图边界设为障碍物
            for (int i = 0; i < 256; i++)
            {
                MapManager.SetWalkable(0, i, false);
                MapManager.SetWalkable(255, i, false);
                MapManager.SetWalkable(i, 0, false);
                MapManager.SetWalkable(i, 255, false);
            }
        }

        /// <summary>
        /// 注册游戏系统
        /// </summary>
        protected override void RegisterSystems()
        {
            //  创建并注册导航系统
            NavSystem = new FlowFieldNavigationSystem();
            World.SystemManager.RegisterSystem(NavSystem);
            NavSystem.InitializeNavigation(FlowFieldManager, RvoSimulator, MapManager);
            zUDebug.Log("[BattleGame] 导航系统注册完成");

            // 注册基础移动系统
            World.SystemManager.RegisterSystem(new MovementSystem());

            // 注册自动追击系统（在战斗系统之前，确保单位能追到敌人）
            var autoChaseSystem = new AutoChaseSystem();
            World.SystemManager.RegisterSystem(autoChaseSystem);
            autoChaseSystem.Initialize(NavSystem);

            // 注册战斗系统
            World.SystemManager.RegisterSystem(new CombatSystem());

            // 注册旋转系统（在战斗系统之后，处理车体和炮塔旋转）
            World.SystemManager.RegisterSystem(new RotationSystem());
            zUDebug.Log("[BattleGame] 旋转系统注册完成");

            World.SystemManager.RegisterSystem(new ProjectileSystem());

            // 注册生命值系统（在ProjectileSystem之后，DeathRemovalSystem之前）
            World.SystemManager.RegisterSystem(new HealthSystem());
            zUDebug.Log("[BattleGame] 生命值系统注册完成");

            // 注册销毁系统
            World.SystemManager.RegisterSystem(new DeathRemovalSystem());

            // 注册AI系统
            AISystem = new SimpleAISystem();
            World.SystemManager.RegisterSystem(AISystem);
            AISystem.Initialize(NavSystem);

            // 注册结算系统
            // World.SystemManager.RegisterSystem(new SettlementSystem());

            zUDebug.Log("[BattleGame] 游戏系统注册完成");
        }

        /// <summary>
        /// 根据匹配成功协议初始化游戏世界（创世阶段）
        /// </summary>
        /// <param name="initialState">初始状态数据</param>
        public new void InitializeWorldFromMatchData(JObject initialState)
        {
            if (World == null)
            {
                zUDebug.LogError("[BattleGame] 世界未初始化，无法执行创世阶段");
                return;
            }

            zUDebug.Log($"[BattleGame] 开始创世阶段，初始化游戏世界状态");

            // 创建一个临时列表来存储创世阶段创建的实体信息
            var createdEntities = new List<UnitCreatedEvent>();

            // 解析initialState数据并创建初始单位和建筑
            foreach (var playerData in initialState)
            {
                string playerIdStr = playerData.Key;
                int playerId = int.Parse(playerIdStr);
                
                var playerObj = (JObject)playerData.Value;
                
                // 创建建筑
                var buildings = (JArray)playerObj["buildings"];
                if (buildings != null)
                {
                    foreach (JObject building in buildings)
                    {
                        string id = building["id"]?.ToString();
                        string type = building["type"]?.ToString();
                        float x = building["x"]?.ToObject<float>() ?? 0;
                        float y = building["y"]?.ToObject<float>() ?? 0;

                        // 根据类型创建建筑实体
                        var entityEvent = EntityCreationManager.CreateBuildingEntity(World, playerId, 1,
                        new zVector3((zfloat)x, zfloat.Zero, (zfloat)y),
                        width:16, height:10, prefabId:0, mapManager:MapManager, flowFieldManager:FlowFieldManager);
                        if (entityEvent.HasValue)
                        {
                            createdEntities.Add(entityEvent.Value);
                        }
                    }
                }

                var playerCount = 0;
                
                // 创建单位
                var units = (JArray)playerObj["units"];
                if (units != null)
                {
                    foreach (JObject unit in units)
                    {
                        string id = unit["id"]?.ToString();
                        string type = unit["type"]?.ToString();
                        float x = unit["x"]?.ToObject<float>() ?? 0;
                        float y = unit["y"]?.ToObject<float>() ?? 0;
                        
                        // 根据类型创建单位实体
                        var entityEvent = CreateUnitEntity(playerId, type, new zVector3((zfloat)x, zfloat.Zero, (zfloat)y));
                        if (entityEvent.HasValue)
                        {
                            createdEntities.Add(entityEvent.Value);
                        }

                        // TODO 测试代码，创建player2的单位
                        // if (playerCount < 1) {
                        //     var entityEvent2 = CreateUnitEntity(2, type, new zVector3((zfloat)x, zfloat.Zero, (zfloat)y + 32));
                        //     if (entityEvent2.HasValue)
                        //     {
                        //         createdEntities.Add(entityEvent2.Value);
                        //     }
                        //     playerCount++;
                        // }
                    }
                }
            }
            
            // 将创世阶段创建的实体信息存储起来，以便在第一帧时发布事件
            _genesisEntities = createdEntities;
            
            zUDebug.Log($"[BattleGame] 创世阶段完成，游戏世界已初始化，共创建 {createdEntities.Count} 个实体");
        }

        private UnitCreatedEvent? CreateBuildingEntity(int playerId, string type, zVector3 position)
        {
            // 根据建筑类型确定建筑类型ID和尺寸
            int buildingType = 0; // 默认为基地
            int width = 2;
            int height = 2;
            int prefabId = 0;
            
            switch (type)
            {
                case "tankFactory":
                    buildingType = 1; // 假设1为坦克工厂
                    width = 10;
                    height = 10;
                    prefabId = 0; // 假设2为坦克工厂预制体ID
                    break;
                default:
                    buildingType = 0; // 基地
                    width = 2;
                    height = 2;
                    prefabId = 0; // 假设1为基地预制体ID
                    break;
            }

            // 1. 创建建筑实体
            var entity = World.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            World.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one * (zfloat)10
            });

            // 3. 添加建筑组件
            var buildingComponent = BuildingComponent.Create(
                buildingType, 0, 0, width, height); // 网格坐标暂时设为0，需要实际计算
            World.ComponentManager.AddComponent(entity, buildingComponent);

            // 4. 添加阵营组件
            var campComponent = CampComponent.Create(playerId);
            World.ComponentManager.AddComponent(entity, campComponent);

            // 5. 添加生命值组件
            var healthComponent = CreateBuildingHealthComponent(buildingType);
            World.ComponentManager.AddComponent(entity, healthComponent);

            // 6. 如果建筑有攻击能力（如防御塔），添加攻击组件
            if (CanBuildingAttack(buildingType))
            {
                var attackComponent = CreateBuildingAttackComponent(buildingType);
                World.ComponentManager.AddComponent(entity, attackComponent);
            }

            // 判断是本地玩家，添加本地玩家组件
            if (World.ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            {
                var globalInfoComponent = World.ComponentManager.GetGlobalComponent<GlobalInfoComponent>();
                if (globalInfoComponent.LocalPlayerCampId == playerId)
                {
                    World.ComponentManager.AddComponent(entity, new LocalPlayerComponent());
                }
            }

            // 7. 创建但不发布事件，返回事件对象
            var unitCreatedEvent = new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = buildingType,
                Position = position,
                PlayerId = playerId,
                PrefabId = prefabId
            };

            zUDebug.Log($"[BattleGame] 创建建筑: Player {playerId}, Type {type} (ID: {buildingType}), Position {position}");
            
            // 返回事件对象，而不是直接发布
            return unitCreatedEvent;
        }

        public UnitCreatedEvent? CreateUnitEntity(int playerId, string type, zVector3 position)
        {
            // 根据单位类型确定单位类型ID
            int unitType = 1; // 默认为动员兵
            int prefabId = 1; // 默认预制体ID
            zfloat radius = (zfloat)2;
            zfloat maxSpeed = (zfloat)3.0f;
            
            switch (type)
            {
                case "tank":
                    unitType = 2; // 坦克
                    prefabId = 2; // 坦克预制体ID
                    radius = (zfloat)2;
                    maxSpeed = (zfloat)6.0f;
                    break;
                default:
                    unitType = 1; // 动员兵
                    prefabId = 1; // 动员兵预制体ID
                    radius = (zfloat)2;
                    maxSpeed = (zfloat)3.0f;
                    break;
            }

            // 1. 创建单位实体
            var entity = World.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            World.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one * radius
            });

            // 3. 添加阵营组件
            var camp = CampComponent.Create(playerId);
            World.ComponentManager.AddComponent(entity, camp);

            // 4. 添加单位组件
            var unit = new UnitComponent
            {
                UnitType = unitType,
                PlayerId = playerId,
                PrefabId = prefabId,
                MoveSpeed = maxSpeed
            };
            World.ComponentManager.AddComponent(entity, unit);

            // 5. 添加生命值组件
            var health = new HealthComponent
            {
                MaxHealth = unitType == 1 ? (zfloat)50 : unitType == 2 ? (zfloat)100 : (zfloat)150,
                CurrentHealth = unitType == 1 ? (zfloat)50 : unitType == 2 ? (zfloat)100 : (zfloat)150
            };
            World.ComponentManager.AddComponent(entity, health);

            // 6. 如果单位有攻击能力，添加攻击组件
            if (unitType == 1 || unitType == 2) // 动员兵和坦克有攻击能力
            {
                var attack = new AttackComponent
                {
                    Damage = unitType == 1 ? (zfloat)10 : (zfloat)30,
                    Range = unitType == 1 ? (zfloat)4 : (zfloat)10,
                    AttackInterval = (zfloat)1,
                    TimeSinceLastAttack = zfloat.Zero,
                    TargetEntityId = -1
                };
                World.ComponentManager.AddComponent(entity, attack);
            }

            // 7. 添加速度组件
            World.ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));

            // 8. 添加导航能力
            NavSystem.AddNavigator(entity, radius, maxSpeed);

            // 9. 添加旋转相关组件
            // 根据单位类型添加不同的载具类型组件
            VehicleTypeComponent vehicleType;
            if (unitType == 1) // 动员兵
            {
                vehicleType = VehicleTypeComponent.CreateInfantry();
            }
            else if (unitType == 2) // 坦克
            {
                vehicleType = VehicleTypeComponent.CreateHeavyTank();
            }
            else
            {
                vehicleType = VehicleTypeComponent.CreateInfantry(); // 默认
            }
            World.ComponentManager.AddComponent(entity, vehicleType);

            // 添加旋转状态组件
            var rotationState = RotationStateComponent.Create();
            World.ComponentManager.AddComponent(entity, rotationState);

            // 如果有炮塔，添加炮塔组件
            if (vehicleType.HasTurret)
            {
                var turret = TurretComponent.CreateDefault();
                World.ComponentManager.AddComponent(entity, turret);
            }

            // 判断是本地玩家，添加本地玩家组件
            if (World.ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            {
                var globalInfoComponent = World.ComponentManager.GetGlobalComponent<GlobalInfoComponent>();
                if (globalInfoComponent.LocalPlayerCampId == playerId)
                {
                    World.ComponentManager.AddComponent(entity, new LocalPlayerComponent());
                }
            }

            // 10. 创建但不发布事件，返回事件对象
            var unitCreatedEvent = new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = unitType,
                Position = position,
                PlayerId = playerId,
                PrefabId = prefabId
            };

            zUDebug.Log($"[BattleGame] 创建单位: Player {playerId}, Type {type} (ID: {unitType}), Position {position}");
            
            // 返回事件对象，而不是直接发布
            return unitCreatedEvent;
        }

        private HealthComponent CreateBuildingHealthComponent(int buildingType)
        {
            switch (buildingType)
            {
                case 0: // 基地
                    return new HealthComponent((zfloat)1000.0f);
                case 1: // 防御塔/工厂
                    return new HealthComponent((zfloat)500.0f);
                default:
                    return new HealthComponent((zfloat)500.0f);
            }
        }

        private AttackComponent CreateBuildingAttackComponent(int buildingType)
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

        private bool CanBuildingAttack(int buildingType)
        {
            switch (buildingType)
            {
                case 0: // 基地
                    return false;
                case 1: // 防御塔/工厂
                    return true; // 假设工厂可以攻击
                default:
                    return false;
            }
        }

        /// <summary>
        /// 创建建筑
        /// </summary>
        public void CreateBuilding(int campId, int buildingType, zVector3 position, int width, int height, int prefabId)
        {
            var command = new CreateBuildingCommand(
                playerId: campId,
                buildingType: buildingType,
                position: position,
                width: width,
                height: height,
                campId: campId,
                prefabId: prefabId
            );

            // 设置地图和流场管理器引用
            command.MapManager = MapManager;
            command.FlowFieldManager = FlowFieldManager;

            // 提交命令
            SubmitCommand(command);
        }

        /// <summary>
        /// 初始化初始基地和单位
        /// </summary>
        public void InitializeStartingUnits()
        {
            // 玩家基地（左下角）
            CreateBuilding(
                campId: 0,
                buildingType: 0,
                position: new zVector3(new zfloat(20), zfloat.Zero, new zfloat(20)),
                width: 10,
                height: 10,
                prefabId: 0 // 蓝色基地预制体
            );

            // AI基地（右上角）
            CreateBuilding(
                campId: 1,
                buildingType: 0,
                position: new zVector3(new zfloat(230), zfloat.Zero, new zfloat(230)),
                width: 10,
                height: 10,
                prefabId: 1 // 红色基地预制体
            );

            zUDebug.Log("[BattleGame] 初始基地创建完成");
        }

        /// <summary>
        /// 创建坦克单位（辅助方法）
        /// </summary>
        public Entity CreateTank(int campId, zVector2 position, int prefabId, zfloat radius, zfloat maxSpeed)
        {
            // 1. 创建Entity
            Entity entity = World.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            var transform = ZLockstep.Simulation.ECS.Components.TransformComponent.Default;
            transform.Position = new zVector3(position.x, zfloat.Zero, position.y);
            World.ComponentManager.AddComponent(entity, transform);

            // 3. 添加阵营组件
            var camp = ZLockstep.Simulation.ECS.Components.CampComponent.Create(campId);
            World.ComponentManager.AddComponent(entity, camp);

            // 4. 添加单位组件
            var unit = new ZLockstep.Simulation.ECS.Components.UnitComponent
            {
                UnitType = 2, // 坦克类型
                PlayerId = campId,
                PrefabId = prefabId,
                MoveSpeed = maxSpeed
            };
            World.ComponentManager.AddComponent(entity, unit);

            // 5. 添加生命值组件
            var health = new ZLockstep.Simulation.ECS.Components.HealthComponent
            {
                MaxHealth = new zfloat(100),
                CurrentHealth = new zfloat(100)
            };
            World.ComponentManager.AddComponent(entity, health);

            // 6. 添加攻击组件
            var attack = new ZLockstep.Simulation.ECS.Components.AttackComponent
            {
                Damage = new zfloat(30),
                Range = new zfloat(10),
                AttackInterval = new zfloat(2),
                TimeSinceLastAttack = zfloat.Zero,
                TargetEntityId = -1
            };
            World.ComponentManager.AddComponent(entity, attack);

            // 7. 添加速度组件
            World.ComponentManager.AddComponent(entity, new ZLockstep.Simulation.ECS.Components.VelocityComponent(zVector3.zero));

            // 8. 添加导航能力
            NavSystem.AddNavigator(entity, radius, maxSpeed);

            // 9. 添加旋转相关组件
            var vehicleType = VehicleTypeComponent.CreateHeavyTank(); // 坦克类型
            World.ComponentManager.AddComponent(entity, vehicleType);

            var rotationState = RotationStateComponent.Create();
            World.ComponentManager.AddComponent(entity, rotationState);

            var turret = TurretComponent.CreateDefault();
            World.ComponentManager.AddComponent(entity, turret);

            // 10. 发布事件通知表现层创建视图
            World.EventManager.Publish(new ZLockstep.Simulation.Events.UnitCreatedEvent
            {
                EntityId = entity.Id,
                PrefabId = prefabId,
                Position = transform.Position
            });

            return entity;
        }

        /// <summary>
        /// 关闭游戏
        /// </summary>
        public new void Shutdown()
        {
            base.Shutdown();
            zUDebug.Log("[BattleGame] 战斗游戏已关闭");
        }
    }
}

