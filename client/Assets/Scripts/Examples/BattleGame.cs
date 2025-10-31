using ZLockstep.Sync;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Sync.Command.Commands;
using ZLockstep.Flow;
using ZLockstep.RVO;
using zUnity;
using ZLockstep.Simulation.ECS;

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
        /// 初始化战斗游戏
        /// </summary>
        public new void Init()
        {
            // 先调用基类初始化
            base.Init();

            // 初始化地图和导航系统
            InitializeMapAndNavigation();

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

            // 5. 创建并注册导航系统
            NavSystem = new FlowFieldNavigationSystem();
            World.SystemManager.RegisterSystem(NavSystem);
            NavSystem.InitializeNavigation(FlowFieldManager, RvoSimulator, MapManager);
            zUDebug.Log("[BattleGame] 导航系统注册完成");
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
            // 注册基础移动系统
            World.SystemManager.RegisterSystem(new MovementSystem());

            // 注册战斗系统
            World.SystemManager.RegisterSystem(new CombatSystem());
            World.SystemManager.RegisterSystem(new ProjectileSystem());

            // 注册AI系统
            AISystem = new SimpleAISystem();
            World.SystemManager.RegisterSystem(AISystem);
            AISystem.Initialize(NavSystem);

            zUDebug.Log("[BattleGame] 游戏系统注册完成");
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

            // 9. 发布事件通知表现层创建视图
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

