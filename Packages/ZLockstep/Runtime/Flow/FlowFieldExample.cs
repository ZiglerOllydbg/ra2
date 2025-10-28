using System.Collections.Generic;
using zUnity;
using ZLockstep.RVO;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场系统完整使用示例
    /// 展示如何在现有ECS架构中集成流场系统
    /// </summary>
    public class FlowFieldExample
    {
        private zWorld world;
        private SimpleMapManager mapManager;
        private FlowFieldManager flowFieldManager;
        private RVO2Simulator rvoSimulator;
        private FlowFieldNavigationSystem navSystem;

        /// <summary>
        /// 初始化完整的流场系统
        /// </summary>
        public void Initialize(zWorld gameWorld)
        {
            world = gameWorld;

            // 1. 创建256x256的逻辑地图（每格0.5单位）
            mapManager = new SimpleMapManager();
            mapManager.Initialize(256, 256, new zfloat(0, 5000)); // 0.5

            // 2. 设置一些障碍物
            SetupObstacles();

            // 3. 创建流场管理器（使用128x128的流场，每格1.0单位）
            flowFieldManager = new FlowFieldManager();
            flowFieldManager.Initialize(mapManager, maxFields: 20);
            flowFieldManager.SetMaxUpdatesPerFrame(2);

            // 4. 创建RVO模拟器
            rvoSimulator = new RVO2Simulator();

            // 5. 创建导航系统并注册到World
            navSystem = new FlowFieldNavigationSystem();
            world.SystemManager.RegisterSystem(navSystem);
            navSystem.InitializeNavigation(flowFieldManager, rvoSimulator, mapManager);
        }

        /// <summary>
        /// 设置地图障碍物
        /// </summary>
        private void SetupObstacles()
        {
            // 地图边界
            for (int i = 0; i < 256; i++)
            {
                mapManager.SetWalkable(0, i, false);
                mapManager.SetWalkable(255, i, false);
                mapManager.SetWalkable(i, 0, false);
                mapManager.SetWalkable(i, 255, false);
            }

            // 添加一些建筑
            mapManager.SetWalkableRect(50, 50, 60, 60, false);
            mapManager.SetWalkableRect(100, 80, 115, 88, false);
            mapManager.SetWalkableCircle(150, 150, 10, false);
        }

        /// <summary>
        /// 创建具有导航能力的单位
        /// </summary>
        public Entity CreateNavigableUnit(zVector2 startPos, zfloat radius, zfloat maxSpeed)
        {
            // 1. 创建实体
            Entity entity = world.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            var transform = TransformComponent.Default;
            transform.Position = new zVector3(startPos.x, zfloat.Zero, startPos.y);
            world.ComponentManager.AddComponent(entity, transform);

            // 3. 添加Velocity组件（可选）
            world.ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));

            // 4. 添加导航能力
            navSystem.AddNavigator(entity, radius, maxSpeed);

            return entity;
        }

        /// <summary>
        /// 示例1：创建一队单位移动到目标
        /// </summary>
        public void Example1_SquadMovement()
        {
            zUDebug.Log("=== 示例1：编队移动 ===");

            // 创建10个单位
            List<Entity> squad = new List<Entity>();
            for (int i = 0; i < 10; i++)
            {
                zVector2 startPos = new zVector2(
                    new zfloat(10 + i * 2),
                    new zfloat(10)
                );
                var unit = CreateNavigableUnit(startPos, new zfloat(0, 3000), new zfloat(2));
                squad.Add(unit);
            }

            // 设置相同目标（会共享同一个流场）
            // 地图范围：256格*0.5=128单位，所以目标应该在(0,0)到(128,128)范围内
            zVector2 targetPos = new zVector2(new zfloat(110), new zfloat(110));
            navSystem.SetMultipleTargets(squad, targetPos);

            zUDebug.Log($"创建了 {squad.Count} 个单位，目标: {targetPos}");
            zUDebug.Log($"活跃流场数量: {flowFieldManager.GetActiveFieldCount()}"); // 应该是1
        }

        /// <summary>
        /// 示例2：多个目标
        /// </summary>
        public void Example2_MultipleTargets()
        {
            zUDebug.Log("=== 示例2：多目标导航 ===");

            // 地图范围：256格*0.5=128单位，所以目标应该在(0,0)到(128,128)范围内
            zVector2[] targets = new zVector2[]
            {
                new zVector2(new zfloat(110), new zfloat(110)),
                new zVector2(new zfloat(110), new zfloat(30)),
                new zVector2(new zfloat(30), new zfloat(110)),
                new zVector2(new zfloat(70), new zfloat(70))
            };

            // 创建20个单位，分成4组
            for (int group = 0; group < 4; group++)
            {
                List<Entity> groupUnits = new List<Entity>();
                
                for (int i = 0; i < 5; i++)
                {
                    zVector2 startPos = new zVector2(
                        new zfloat(20 + group * 10),
                        new zfloat(20 + i * 2)
                    );
                    var unit = CreateNavigableUnit(startPos, new zfloat(0, 3000), new zfloat(2));
                    groupUnits.Add(unit);
                }

                navSystem.SetMultipleTargets(groupUnits, targets[group]);
            }

            zUDebug.Log($"创建了 4 组共 20 个单位");
            zUDebug.Log($"活跃流场数量: {flowFieldManager.GetActiveFieldCount()}"); // 应该是4
        }

        /// <summary>
        /// 示例3：建造建筑（动态障碍物）
        /// </summary>
        public void Example3_DynamicObstacles()
        {
            zUDebug.Log("=== 示例3：动态障碍物 ===");

            // 创建单位并设置目标
            var unit = CreateNavigableUnit(
                new zVector2(new zfloat(30), new zfloat(30)),
                new zfloat(0, 3000),
                new zfloat(2)
            );
            // 地图范围：256格*0.5=128单位
            navSystem.SetMoveTarget(unit, new zVector2(new zfloat(100), new zfloat(100)));

            zUDebug.Log("单位开始移动...");

            // 模拟建造建筑
            System.Threading.Thread.Sleep(100);
            BuildBuilding(70, 70, 15, 10);

            zUDebug.Log("建筑建造完成，流场已更新");
        }

        /// <summary>
        /// 建造建筑
        /// </summary>
        public void BuildBuilding(int x, int y, int width, int height)
        {
            // 1. 更新地图
            mapManager.SetWalkableRect(x, y, x + width, y + height, false);

            // 2. 标记受影响的流场为脏
            flowFieldManager.MarkRegionDirty(x, y, x + width, y + height);

            zUDebug.Log($"建筑建造在 ({x}, {y}), 尺寸 {width}x{height}");
        }

        /// <summary>
        /// 摧毁建筑
        /// </summary>
        public void DestroyBuilding(int x, int y, int width, int height)
        {
            // 1. 更新地图
            mapManager.SetWalkableRect(x, y, x + width, y + height, true);

            // 2. 标记受影响的流场为脏
            flowFieldManager.MarkRegionDirty(x, y, x + width, y + height);

            zUDebug.Log($"建筑摧毁在 ({x}, {y}), 尺寸 {width}x{height}");
        }

        /// <summary>
        /// 获取所有有导航组件的单位位置
        /// </summary>
        public List<zVector2> GetAllUnitPositions()
        {
            List<zVector2> positions = new List<zVector2>();
            
            var entities = world.ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
            foreach (var entityId in entities)
            {
                Entity entity = new Entity(entityId);
                var transform = world.ComponentManager.GetComponent<TransformComponent>(entity);
                positions.Add(new zVector2(transform.Position.x, transform.Position.z));
            }
            
            return positions;
        }

        /// <summary>
        /// 完整的游戏循环示例
        /// </summary>
        public void RunCompleteExample()
        {
            zUDebug.Log("=== 流场系统完整示例 ===\n");

            // 创建单位
            Example1_SquadMovement();

            // 模拟游戏循环
            int frameCount = 0;
            int maxFrames = 300; // 模拟10秒

            while (frameCount < maxFrames)
            {
                // 更新游戏世界（会自动调用所有System的Update）
                world.Update();
                frameCount++;

                // 每秒输出一次状态
                if (frameCount % 30 == 0)
                {
                    int arrivedCount = 0;
                    var entities = world.ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
                    foreach (var entityId in entities)
                    {
                        Entity entity = new Entity(entityId);
                        var navigator = world.ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
                        if (navigator.HasReachedTarget)
                        {
                            arrivedCount++;
                        }
                    }

                    int totalCount = 0;
                    foreach (var _ in entities) totalCount++;

                    zUDebug.Log($"帧 {frameCount}: {arrivedCount}/{totalCount} 单位到达目标");
                    zUDebug.Log($"  活跃流场: {flowFieldManager.GetActiveFieldCount()}");
                }

                // 在第5秒建造建筑
                if (frameCount == 150)
                {
                    BuildBuilding(100, 100, 20, 15);
                }

                // 检查是否所有单位都到达
                bool allArrived = true;
                var allEntities = world.ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
                foreach (var entityId in allEntities)
                {
                    Entity entity = new Entity(entityId);
                    var navigator = world.ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
                    if (!navigator.HasReachedTarget)
                    {
                        allArrived = false;
                        break;
                    }
                }

                if (allArrived)
                {
                    zUDebug.Log($"\n所有单位在第 {frameCount} 帧到达目标！");
                    break;
                }
            }

            zUDebug.Log("\n=== 示例结束 ===");
        }
    }
}
