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

        public bool EnableSettlementSystem { get; set; }

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

        public override IFlowFieldMap GetMapManager()
        {
            return MapManager; // 返回BattleGame的NavSystem
        }

        public override FlowFieldManager GetFlowFieldManager()
        {
            return FlowFieldManager; // 返回BattleGame的NavSystem
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
            //  创建并注册导航系统(去掉MovementSystem基础移动系统)
            NavSystem = new FlowFieldNavigationSystem();
            World.SystemManager.RegisterSystem(NavSystem);
            NavSystem.InitializeNavigation(FlowFieldManager, RvoSimulator, MapManager);
            zUDebug.Log("[BattleGame] 导航系统注册完成");

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

            // 注册生产系统
            World.SystemManager.RegisterSystem(new ProduceSystem());
            // 注册经济系统
            World.SystemManager.RegisterSystem(new EconomySystem());

            // 注册结算系统
            if (EnableSettlementSystem)
            {
                World.SystemManager.RegisterSystem(new SettlementSystem());
            }

            World.SystemManager.RegisterSystem(new BuildingConstructionSystem());

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
                // 处理中立建筑
                if (playerData.Key == "neutral")
                {
                    var neutralObj = (JObject)playerData.Value;
                    
                    // 创建中立建筑
                    var neutralBuildings = (JArray)neutralObj["buildings"];
                    if (neutralBuildings != null)
                    {
                        foreach (JObject building in neutralBuildings)
                        {
                            string id = building["id"]?.ToString();
                            string type = building["type"]?.ToString();
                            float x = building["x"]?.ToObject<float>() ?? 0;
                            float y = building["y"]?.ToObject<float>() ?? 0;

                            // 根据类型创建建筑实体 (中立建筑使用特殊playerId，比如-1)
                            var entityEvent = EntityCreationManager.CreateBuildingEntity(World, -1, BuildingType.Mine,
                            new zVector3((zfloat)x, zfloat.Zero, (zfloat)y),
                            width:4, height:4, prefabId:2, mapManager:MapManager, flowFieldManager:FlowFieldManager);
                            if (entityEvent.HasValue)
                            {
                                createdEntities.Add(entityEvent.Value);
                            }
                        }
                    }
                    continue; // 处理完中立建筑后继续下一个
                }

                string campIdStr = playerData.Key;
                int campId = int.Parse(campIdStr);
                
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

                        // 根据类型确定建筑参数
                        BuildingType buildingType = BuildingType.None; // 默认基地
                        int width = 10;
                        int height = 10;

                        switch (type)
                        {
                            case "base":
                                buildingType = BuildingType.Base; // 基地
                                width = 10;
                                height = 10;
                                break;
                        }

                        // 根据类型创建建筑实体
                        var entityEvent = EntityCreationManager.CreateBuildingEntity(World, campId, buildingType,
                        new zVector3((zfloat)x, zfloat.Zero, (zfloat)y),
                        width: width, height: height, prefabId: 1, mapManager:MapManager, flowFieldManager:FlowFieldManager);
                        if (entityEvent.HasValue)
                        {
                            createdEntities.Add(entityEvent.Value);
                        }
                    }
                }

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
                        
                        // 使用EntityCreationManager创建单位
                        int prefabId = 6; // 默认预制体ID
                        var unitEvent = EntityCreationManager.CreateUnitEntity(World, campId, UnitType.Tank, 
                            new zVector3((zfloat)x, zfloat.Zero, (zfloat)y), prefabId);
                        if (unitEvent.HasValue)
                        {
                            createdEntities.Add(unitEvent.Value);
                        }
                    }
                }
                
                // 添加经济组件（每个玩家一个全局实体）
                var money = playerObj["money"]?.ToObject<int>() ?? 2200; // 默认2200资金
                var economyEntity = World.EntityManager.CreateEntity();
                World.ComponentManager.AddComponent(economyEntity, EconomyComponent.Create(money, 10));
                World.ComponentManager.AddComponent(economyEntity, CampComponent.Create(campId));
            }
            
            // TODO 调试用
            if (initialState.Count == 2)
            {
                // 添加一个camp2的主基地，在64,64位置
                var entityEvent = EntityCreationManager.CreateBuildingEntity(World, 2, BuildingType.Base,
                    new zVector3((zfloat)64, zfloat.Zero, (zfloat)64),
                    width: 10, height: 10, prefabId: 1, mapManager:MapManager, flowFieldManager:FlowFieldManager);
                if (entityEvent.HasValue)
                {
                    createdEntities.Add(entityEvent.Value);
                }

                // 添加阵营2的经济组件
                var economyEntity = World.EntityManager.CreateEntity();
                World.ComponentManager.AddComponent(economyEntity, EconomyComponent.Create(2200, 10));
                World.ComponentManager.AddComponent(economyEntity, CampComponent.Create(2));
            }

            // 将创世阶段创建的实体信息存储起来，以便在第一帧时发布事件
            _genesisEntities = createdEntities;
            
            zUDebug.Log($"[BattleGame] 创世阶段完成，游戏世界已初始化，共创建 {createdEntities.Count} 个实体");
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

