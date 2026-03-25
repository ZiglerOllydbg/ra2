using System.Collections.Generic;
using System.Linq;
using zUnity;
using ZLockstep.RVO;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.View;
using System;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场导航系统
    /// 处理所有使用流场寻路的实体
    /// 继承自BaseSystem，集成到现有ECS框架
    /// 
    /// 该系统结合了流场寻路(Flow Field Pathfinding)和RVO避障算法(Reciprocal Velocity Obstacles)
    /// 实现高效的群体寻路和避障功能，特别适用于RTS游戏中大量单位的移动控制
    /// 主要功能包括：
    /// 1. 流场生成与管理
    /// 2. RVO避障代理管理
    /// 3. 单位移动控制与同步
    /// 4. 散点目标分配（适用于编队分散）
    /// 5. 卡住检测与处理
    /// 6. 到达目标检测
    /// </summary>
    public class FlowFieldNavigationSystem : BaseSystem
    {
        private FlowFieldManager flowFieldManager;
        private IFlowFieldMap map;
        
        /// <summary>
        /// 到达距离阈值：当单位与目标距离小于此值时，认为已到达目标
        /// 可在 Unity Inspector 中配置，针对不同单位类型调整
        /// </summary>
        private float arrivalDistanceThreshold = 1f;
    

        // Debug: 最近一次散点集合
        private readonly List<zVector2> debugScatterPoints = new List<zVector2>();
        public IReadOnlyList<zVector2> DebugScatterPoints => debugScatterPoints;
        private zVector2 debugScatterCenter = zVector2.zero;
        private zfloat debugScatterRadius = zfloat.Zero;
        public zVector2 DebugScatterCenter => debugScatterCenter;
        public zfloat DebugScatterRadius => debugScatterRadius;

        protected override void OnInitialize()
        {
            // 系统初始化
            Simulator.Instance.Clear();
            Simulator.Instance.setTimeStep(0.05f);
            Simulator.Instance.setAgentDefaults(15.0f, 10, 5.0f, 5.0f, 1.0f, 6.0f, new Vector2(0.0f, 0.0f));
        }

        /// <summary>
        /// 初始化流场导航系统
        /// 在系统注册到World后调用
        /// </summary>
        /// <param name="ffMgr">流场管理器</param>
        /// <param name="rvoSim">RVO避障模拟器</param>
        /// <param name="gameMap">游戏地图接口</param>
        public void InitializeNavigation(FlowFieldManager ffMgr, RVO2Simulator rvoSim, IFlowFieldMap gameMap)
        {
            flowFieldManager = ffMgr;
            map = gameMap;
            // UpdateObstacles();
        }

        /// <summary>
        /// 更新场景障碍物
        /// </summary>
        public void UpdateObstacles()
        {
            Simulator.Instance.ClearObstacles();

            // 场景边界
            int width = map.GetWidth();
            int height = map.GetHeight();
            float gridSize = (float)map.GetGridSize();

            // 创建场景边界障碍物（逆时针顺序）
            // 边界向外扩展半个格子，确保单位不会走到地图边缘
            float halfGrid = gridSize * 0.5f;
            float minX = -halfGrid;
            float minY = -halfGrid;
            float maxX = width * gridSize - halfGrid;
            float maxY = height * gridSize - halfGrid;

            // 环境边界，使用顺时针顺序
            List<Vector2> boundaryVertices = new List<Vector2>
            {
                new(minX, minY),  // 左下
                new(minX, maxY),   // 左上
                new(maxX, maxY),  // 右上
                new(maxX, minY),  // 右下
            };

            Simulator.Instance.addObstacle(boundaryVertices);

            // 获取所有建筑，添加建筑障碍物
            var buildingEntities = ComponentManager.GetAllEntityIdsWith<BuildingComponent>();
            foreach (var entityId in buildingEntities)
            {
                Entity entity = new Entity(entityId);
                var building = ComponentManager.GetComponent<BuildingComponent>(entity);
                
                // 计算建筑物的世界坐标边界
                float minWorldX = building.GridX * gridSize;
                float minWorldY = building.GridY * gridSize;
                float maxWorldX = (building.GridX + building.Width) * gridSize;
                float maxWorldY = (building.GridY + building.Height) * gridSize;

                // 创建建筑物障碍物（逆时针顺序）
                List<Vector2> buildingVertices = new List<Vector2>
                {
                    new(minWorldX, minWorldY),  // 左下
                    new(maxWorldX, minWorldY),  // 右下
                    new(maxWorldX, maxWorldY),  // 右上
                    new(minWorldX, maxWorldY)   // 左上
                };

                Simulator.Instance.addObstacle(buildingVertices);
            }

            // add in awake
            Simulator.Instance.processObstacles();
        }

        /// <summary>
        /// 添加导航能力
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="radius">半径</param>
        /// <param name="maxSpeed">最大速度</param>
        public void AddNavigator(Entity entity, zfloat radius, zfloat maxSpeed)
        {
            // 获取实体位置
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 pos2D = new(transform.Position.x, transform.Position.z);

            // 添加导航组件
            var navigator = FlowFieldNavigatorComponent.Create(radius, maxSpeed);
            
            int sid = Simulator.Instance.addAgent(new Vector2(pos2D.ToVector2().x, pos2D.ToVector2().y));
            Simulator.Instance.setAgentRadius(sid, radius.ToFloat() / 2f);
            Simulator.Instance.setAgentMaxSpeed(sid, maxSpeed.ToFloat());
            navigator.RvoAgentId = sid;

            ComponentManager.AddComponent(entity, navigator);
        }

        /// <summary>
        /// 设置实体的移动目标
        /// 主要功能：
        /// 1. 释放实体当前的流场资源
        /// 2. 请求新的流场数据
        /// 3. 更新导航状态（重置卡住检测等）
        /// 4. 设置目标位置组件
        /// </summary>
        /// <param name="entity">需要设置目标的实体</param>
        /// <param name="targetPos">目标位置</param>
        public void SetMoveTarget(Entity entity, zVector2 targetPos, bool userInput = false)
        {
            // 将目标位置对齐到网格中心
            map.WorldToGrid(targetPos, out int targetGridX, out int targetGridY);
            zVector2 alignedTargetPos = map.GridToWorld(targetGridX, targetGridY);

            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);

            // 释放旧流场
            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
            }

            // 用户输入，使用流畅寻路
            if (userInput)
            {
                navigator.CurrentFlowFieldId = flowFieldManager.RequestFlowField(alignedTargetPos);
            }
            navigator.HasReachedTarget = false;
            // 重置卡住状态
            navigator.StuckFrames = 0;
            navigator.LastPosition = new zVector2(transform.Position.x, transform.Position.z);
            navigator.NearSlowFrames = 0;

            // 更新导航组件
            ComponentManager.AddComponent(entity, navigator);

            // 添加或更新目标组件
            var target = MoveTargetComponent.Create(alignedTargetPos);
            target.UserInput = userInput;
            ComponentManager.AddComponent(entity, target);

            debugScatterPoints.Clear();
            debugScatterCenter = alignedTargetPos;
        }

        /// <summary>
        /// 批量设置多个实体的目标（常见于RTS选中操作）
        /// </summary>
        /// <param name="entities">需要设置目标的实体列表</param>
        /// <param name="targetPos">目标位置</param>
        public void SetMultipleTargets(List<Entity> entities, zVector2 targetPos, bool userInput = false)
        {
            foreach (var entity in entities)
            {
                SetMoveTarget(entity, targetPos, userInput);
            }
        }

        /// <summary>
        /// 为多个单位设置散点目标，复用同一个多源流场
        /// 主要功能：
        /// 1. 计算编队中单位的最大半径，确定分散间距
        /// 2. 生成方形候选散点分布（默认）
        /// 3. 将候选点投影到可行走区域并去重
        /// 4. 请求共享的多源流场（一个流场服务多个目标点）
        /// 5. 使用贪心算法为每个单位分配最近的散点
        /// 6. 为每个单位设置个人目标点和共享流场ID
        /// </summary>
        /// <param name="entities">需要设置目标的实体列表</param>
        /// <param name="groupCenter">编队中心位置</param>
        public void SetScatterTargets(List<Entity> entities, zVector2 groupCenter, bool userInput = false)
        {
            if (entities == null || entities.Count == 0)
                return;

            // 将编队中心位置对齐到网格中心
            map.WorldToGrid(groupCenter, out int centerGridX, out int centerGridY);
            zVector2 alignedGroupCenter = map.GridToWorld(centerGridX, centerGridY);

            // 统计最大半径，确定最小间距
            zfloat maxRadius = zfloat.Zero;
            foreach (var e in entities)
            {
                var nav = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(e);
                if (nav.Radius > maxRadius) maxRadius = nav.Radius;
            }
            // 基础间距：约 4x 半径，并至少为 2 个网格尺寸，保证明显分散
            zfloat spacing = maxRadius * new zfloat(2);
            zfloat minSpacing = map.GetGridSize() * new zfloat(2);
            if (spacing < minSpacing) spacing = minSpacing;

            // 生成候选散点（方形格，默认）
            // 计算所需的行列数：rows*cols >= N
            int pointsPerSide = System.Math.Max(6, (int)zMathf.Sqrt((zfloat)entities.Count) + 1);
            var candidates = GenerateSquareLattice(alignedGroupCenter, spacing, pointsPerSide, pointsPerSide, entities.Count * 6);

            // 投影到最近可走格并去重
            List<zVector2> scatterPoints = new List<zVector2>();
            HashSet<long> seen = new HashSet<long>();
            int attempts = 0;
            while (scatterPoints.Count < entities.Count && attempts < 3)
            {
                foreach (var p in candidates)
                {
                    zVector2 q = ProjectToWalkable(p, 12 + attempts * 6);
                    map.WorldToGrid(q, out int gx, out int gy);
                    long k = ((long)gx) | (((long)gy) << 32);
                    if (seen.Add(k))
                    {
                        scatterPoints.Add(q);
                        if (scatterPoints.Count >= entities.Count) break;
                    }
                }
                if (scatterPoints.Count < entities.Count)
                {
                    pointsPerSide += 2;
                    candidates = GenerateSquareLattice(alignedGroupCenter, spacing, pointsPerSide, pointsPerSide, entities.Count * 8);
                    attempts++;
                }
            }
            if (scatterPoints.Count == 0)
                return;

            // 请求共享多源流场
            int fieldId = flowFieldManager.RequestFlowFieldMultiWorld(scatterPoints);
            if (fieldId < 0)
                return;

            // 使用保持相对位置的分配算法
            var assigned = PositionPreservingAssign(entities, scatterPoints, alignedGroupCenter);

            // 记录 Debug 散点
            debugScatterPoints.Clear();
            debugScatterPoints.AddRange(scatterPoints);
            debugScatterCenter = alignedGroupCenter;
            // 半径取最大距离
            zfloat maxR = zfloat.Zero;
            foreach (var p in scatterPoints)
            {
                zfloat d = (p - alignedGroupCenter).magnitude;
                if (d > maxR) maxR = d;
            }
            debugScatterRadius = maxR;

            foreach (var e in entities)
            {
                var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(e);
                // 释放旧流场
                if (navigator.CurrentFlowFieldId >= 0)
                {
                    flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
                }
                navigator.CurrentFlowFieldId = fieldId;
                navigator.HasReachedTarget = false;
                navigator.StuckFrames = 0;
                // 放宽到达半径，减少相互挤动
                zfloat minArrival = navigator.Radius * new zfloat(1, 5000); // 1.5x radius
                if (navigator.ArrivalRadius < minArrival)
                {
                    navigator.ArrivalRadius = minArrival;
                }
                navigator.NearSlowFrames = 0;

                // 更新组件
                ComponentManager.AddComponent(e, navigator);

                // 设置个人目标
                var target = MoveTargetComponent.Create(assigned[e]);
                target.UserInput = userInput;
                ComponentManager.AddComponent(e, target);
            }
        }

       
        /// <summary>
        /// 生成方形分布的候选点
        /// 主要功能：
        /// 1. 在正方形区域内生成均匀分布的候选点集
        /// 2. 根据指定的行列数和最大数量限制生成点
        /// 3. 按距离中心点的远近对生成的点进行排序
        /// </summary>
        /// <param name="center">中心点位置</param>
        /// <param name="spacing">点之间的间隔距离</param>
        /// <param name="rows">生成的行数</param>
        /// <param name="cols">生成的列数</param>
        /// <param name="maxCount">生成点的最大数量</param>
        /// <returns>按距离排序的候选点列表</returns>
        private List<zVector2> GenerateSquareLattice(zVector2 center, zfloat spacing, int rows, int cols, int maxCount)
        {
            List<zVector2> pts = new List<zVector2>();
            
            // 计算起始点，使中心对齐
            zfloat startX = center.x - (cols - 1) * spacing * new zfloat(0, 5000);
            zfloat startY = center.y - (rows - 1) * spacing * new zfloat(0, 5000);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    zVector2 p = new zVector2(
                        startX + col * spacing,
                        startY + row * spacing
                    );
                    // 将点对齐到网格中心
                    map.WorldToGrid(p, out int gridX, out int gridY);
                    zVector2 alignedPoint = map.GridToWorld(gridX, gridY);
                    pts.Add(alignedPoint);
                    
                    if (pts.Count >= maxCount)
                        goto END;
                }
            }
            
        END:
            // 按距中心排序
            pts.Sort((p1, p2) =>
            {
                zfloat d1 = (p1 - center).sqrMagnitude;
                zfloat d2 = (p2 - center).sqrMagnitude;
                if (d1 < d2) return -1;
                if (d1 > d2) return 1;
                return 0;
            });
            return pts;
        }

        private zVector2 ProjectToWalkable(zVector2 pos, int maxRing)
        {
            map.WorldToGrid(pos, out int gx, out int gy);
            if (map.IsWalkable(gx, gy))
                return map.GridToWorld(gx, gy);

            for (int r = 1; r <= maxRing; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int y = gy + dy;
                    int x1 = gx - r;
                    int x2 = gx + r;
                    if (map.IsWalkable(x1, y)) return map.GridToWorld(x1, y);
                    if (map.IsWalkable(x2, y)) return map.GridToWorld(x2, y);
                }
                for (int dx = -r + 1; dx <= r - 1; dx++)
                {
                    int x = gx + dx;
                    int y1 = gy - r;
                    int y2 = gy + r;
                    if (map.IsWalkable(x, y1)) return map.GridToWorld(x, y1);
                    if (map.IsWalkable(x, y2)) return map.GridToWorld(x, y2);
                }
            }
            return pos;
        }

        /// <summary>
        /// 贪心分配算法：为每个实体分配最近的散点
        /// 主要功能：
        /// 1. 遍历所有实体，为每个实体寻找最近的可用散点
        /// 2. 使用贪心策略，优先为每个实体分配距离最近的点
        /// 3. 处理点数不足的情况，复用最近的点
        /// </summary>
        /// <param name="entities">需要分配目标点的实体列表</param>
        /// <param name="points">可用的目标点列表</param>
        /// <returns>实体与目标点的映射关系</returns>
        private Dictionary<Entity, zVector2> GreedyAssign(List<Entity> entities, List<zVector2> points)
        {
            Dictionary<Entity, zVector2> result = new Dictionary<Entity, zVector2>();
            bool[] used = new bool[points.Count];
            foreach (var e in entities)
            {
                var t = ComponentManager.GetComponent<TransformComponent>(e);
                zVector2 p = new zVector2(t.Position.x, t.Position.z);
                int best = -1;
                zfloat bestDist = zfloat.Infinity;
                for (int i = 0; i < points.Count; i++)
                {
                    if (used[i]) continue;
                    zfloat d = (points[i] - p).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
                if (best >= 0)
                {
                    used[best] = true;
                    result[e] = points[best];
                }
            }
            // 兜底：若点不足，复用最近的
            for (int i = 0; i < entities.Count; i++)
            {
                if (!result.ContainsKey(entities[i]))
                {
                    result[entities[i]] = points[0];
                }
            }
            return result;
        }

        /// <summary>
        /// 保持相对位置的分配算法：根据实体间的相对位置关系，分配散点以保持编队形状
        /// 主要功能：
        /// 1. 计算实体的相对位置关系（相对于编队中心）
        /// 2. 计算散点的相对位置关系（相对于散点中心）
        /// 3. 按照实体与散点的相对位置匹配程度进行分配
        /// </summary>
        /// <param name="entities">需要分配目标点的实体列表</param>
        /// <param name="points">可用的目标点列表</param>
        /// <param name="groupCenter">编队中心位置</param>
        /// <returns>实体与目标点的映射关系</returns>
        private Dictionary<Entity, zVector2> PositionPreservingAssign(List<Entity> entities, List<zVector2> points, zVector2 groupCenter)
        {
            Dictionary<Entity, zVector2> result = new Dictionary<Entity, zVector2>();
            
            // 获取实体相对于编队中心的位置
            List<zVector2> entityOffsets = new List<zVector2>();
            foreach (var e in entities)
            {
                var t = ComponentManager.GetComponent<TransformComponent>(e);
                zVector2 p = new zVector2(t.Position.x, t.Position.z);
                entityOffsets.Add(p - groupCenter);
            }
            
            // 标记已使用的散点
            bool[] used = new bool[points.Count];
            
            // 为每个实体分配散点
            for (int i = 0; i < entities.Count; i++)
            {
                zVector2 entityOffset = entityOffsets[i];
                int bestPoint = -1;
                zfloat bestScore = zfloat.Infinity;
                
                // 寻找最适合的散点
                for (int j = 0; j < points.Count; j++)
                {
                    if (used[j]) continue;
                    
                    zVector2 pointOffset = points[j] - groupCenter;
                    // 计算相对位置差异（考虑距离和角度）
                    zfloat distanceDiff = zMathf.Abs(entityOffset.magnitude - pointOffset.magnitude);
                    zfloat angleDiff = zMathf.Abs(zMathf.Atan2(entityOffset.y, entityOffset.x) - zMathf.Atan2(pointOffset.y, pointOffset.x));
                    
                    // 角度差需要处理周期性
                    if (angleDiff > zMathf.PI)
                        angleDiff = zMathf.PI * 2 - angleDiff;
                    
                    // 综合评分（距离差异权重较大）
                    zfloat score = distanceDiff * new zfloat(2) + angleDiff;
                    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPoint = j;
                    }
                }
                
                // 分配最佳散点
                if (bestPoint >= 0)
                {
                    used[bestPoint] = true;
                    result[entities[i]] = points[bestPoint];
                }
            }
            
            // 兜底：若点不足，为未分配的实体分配最近的可用点
            for (int i = 0; i < entities.Count; i++)
            {
                if (!result.ContainsKey(entities[i]))
                {
                    zVector2 entityPos = new zVector2(
                        ComponentManager.GetComponent<TransformComponent>(entities[i]).Position.x,
                        ComponentManager.GetComponent<TransformComponent>(entities[i]).Position.z
                    );
                    
                    int nearestPoint = -1;
                    zfloat nearestDistance = zfloat.Infinity;
                    
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (used[j]) continue;
                        
                        zfloat distance = (points[j] - entityPos).sqrMagnitude;
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestPoint = j;
                        }
                    }
                    
                    if (nearestPoint >= 0)
                    {
                        used[nearestPoint] = true;
                        result[entities[i]] = points[nearestPoint];
                    }
                    else
                    {
                        // 如果所有点都被使用，分配第一个点
                        result[entities[i]] = points[0];
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// 清除实体的移动目标
        /// 主要功能：
        /// 1. 释放实体当前占用的流场资源
        /// 2. 重置导航状态（流场ID和到达标志）
        /// 3. 移除目标组件
        /// 4. 停止RVO智能体的移动
        /// </summary>
        public void ClearMoveTarget(Entity entity, bool reachTarget = false)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            
            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
                navigator.CurrentFlowFieldId = -1;
                navigator.HasReachedTarget = reachTarget;

                ComponentManager.AddComponent(entity, navigator);
            }

            // 移除目标组件
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);

            // 停止RVO智能体
            if (navigator.RvoAgentId >= 0)
            {
                Simulator.Instance.setAgentPrefVelocity(navigator.RvoAgentId, new Vector2(0, 0));
            }

            zUDebug.Log($"Clear move target for entity {entity}");
        }

        /// <summary>
        /// 系统更新（每帧调用）
        /// 主要功能：
        /// 1. 更新流场管理器
        /// 2. 为每个有导航组件的实体设置期望速度
        /// 3. 执行RVO避障计算
        /// 4. 同步位置回 Transform 组件
        /// </summary>
        public override void Update()
        {
            if (flowFieldManager.NeedUpdateObstacles)
            {
                flowFieldManager.NeedUpdateObstacles = false;
                UpdateObstacles();
            }

            UpdateRVO();

            // 监控 agent 行为
            foreach (var agentNo in Simulator.Instance.GetAgentNoList())
            {
                // 获取实际邻居数量
                int actualNeighbors = Simulator.Instance.getAgentNumAgentNeighbors(agentNo);
                
                // 如果经常达到上限，增加 maxNeighbors
                if (actualNeighbors >= 10)
                {
                    zUDebug.LogWarning($"Agent {agentNo} 达到邻居上限！");
                    // 考虑增加到 15 或 20
                }
            }
        }

        private Random m_random = new Random();

        private void UpdateRVO()
        {
            if (flowFieldManager == null)
                return;

            // 1. 更新流场管理器（处理脏流场）
            flowFieldManager.Tick();

            // 设置期望速度
            var navigatorEntities = ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
            foreach (var entityId in navigatorEntities)
            {
                CalculateAndSetEntityVelocity(entityId);
            }

            Simulator.Instance.doStep();

            // 4. 同步位置回 Transform 组件
            foreach (var entityId in navigatorEntities)
            {
                SyncEntityPositionAndRotation(entityId);
            }

        }

        /// <summary>
        /// 计算并设置实体的期望移动速度
        /// 主要功能：
        /// 1. 检查导航器是否启用以及是否有移动目标
        /// 2. 从流场采样获取移动方向
        /// 3. 根据是否在目标格子内采用不同的速度计算策略：
        ///    - 在目标格子内：直接朝向目标点移动
        ///    - 在路径上：沿流场方向移动，并添加微小随机扰动防止卡住
        /// 4. 通过 RVO 系统设置实体的期望速度
        /// </summary>
        /// <param name="entityId">实体 ID</param>
        /// <returns>如果成功设置速度返回 true，否则返回 false（未启用或无目标）</returns>
        private void CalculateAndSetEntityVelocity(int entityId)
        {
            Entity entity = new Entity(entityId);

            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            // 导航器未启用，无法进行移动计算
            if (!navigator.IsEnabled)
                return;

            // 没有移动目标时，清零速度并退出
            if (!ComponentManager.HasComponent<MoveTargetComponent>(entity))
            {
                // 停止RVO智能体
                if (navigator.RvoAgentId >= 0)
                {
                    Simulator.Instance.setAgentPrefVelocity(navigator.RvoAgentId, new Vector2(0, 0));
                }
                return;
            }

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 currentPos = new zVector2(transform.Position.x, transform.Position.z);
            var target = ComponentManager.GetComponent<MoveTargetComponent>(entity);

            // 判断 currentPos 与 target.TargetPosition 之间的距离，如果小于某个阈值，设置速度为 0
            zVector2 targetPos = target.TargetPosition;
            zfloat distance = zVector2.Distance(currentPos, targetPos);
            
            if (distance < zfloat.FromFloat(arrivalDistanceThreshold))
            {
                ClearMoveTarget(entity);
                zUDebug.Log($"[RVO] {entityId} 已到达目标, 距离小于阈值，distance: {distance}");
                return;
            }

            // 从流场获取当前方向的移动指引
            zVector2 flowDirection = flowFieldManager.SampleDirection(navigator.CurrentFlowFieldId, currentPos);

            // // ⭐流场方向是否改变，这是智能体瞬移的原因，移动不顺畅的原因
            // if (flowDirection == target.LastFlowDirection)
            // {
            //     return;
            // }

            // 记录流场方向变化
            // target.LastFlowDirection = flowDirection;
            // ComponentManager.AddComponent(entity, target);

            // 当流场方向为零时，通常是在目标格子内或者没有使用流畅寻路，直接朝目标点移动
            if (flowDirection == zVector2.zero)
            {
                // 直接朝目标移动
                Vector2 moveTarget = new(target.TargetPosition.x.ToFloat(), target.TargetPosition.y.ToFloat());
                Vector2 goalVector = moveTarget - Simulator.Instance.getAgentPosition(navigator.RvoAgentId);
                if (RVOMath.absSq(goalVector) > 1.0f)
                {
                    goalVector = RVOMath.normalize(goalVector);
                }

                goalVector *= navigator.MaxSpeed.ToFloat();

                if (target.LastPrefVelocity == goalVector)
                {
                    return;
                }

                target.LastPrefVelocity = goalVector;
                ComponentManager.AddComponent(entity, target);

                // 设置期望速度，用于目标格子内的精确移动
                Simulator.Instance.setAgentPrefVelocity(navigator.RvoAgentId, goalVector);

                zUDebug.Log($"[RVO] {entityId} 移动中, flowDirection: {flowDirection}, goalVector: {goalVector}");
            }
            else
            {
                // 沿流场方向移动
                Vector2 goalVector = new Vector2(flowDirection.x.ToFloat(), flowDirection.y.ToFloat());
                goalVector = RVOMath.normalize(goalVector);
                // 增加速度倍率，加快移动
                goalVector *= navigator.MaxSpeed.ToFloat();

                if (target.LastPrefVelocity == goalVector)
                {
                    return;
                }
                target.LastPrefVelocity = goalVector;
                ComponentManager.AddComponent(entity, target);

                Simulator.Instance.setAgentPrefVelocity(navigator.RvoAgentId, goalVector);

                zUDebug.Log($"[RVO] {entityId} 移动中, 流场速度改变，flowDirection: {flowDirection}, goalVector: {goalVector}");

                // 添加微小随机扰动，防止多个单位重叠或卡住
                // float angle = (float)m_random.NextDouble() * 2.0f * (float)Math.PI;
                // float dist = (float)m_random.NextDouble() * 0.0001f;

                // Vector2 setFinalV2 = Simulator.Instance.getAgentPrefVelocity(navigator.RvoAgentId) +
                //                                             dist *
                //                                             new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                
                // Simulator.Instance.setAgentPrefVelocity(navigator.RvoAgentId, setFinalV2);

                // zUDebug.Log($"[RVO] {entityId} 移动中, flowDirection: {flowDirection}, goalVector: {goalVector}, setFinalV2: {setFinalV2}");
            }
        }

        /// <summary>
        /// 同步实体的位置和旋转信息
        /// 从 RVO 模拟器获取实体最新的位置和速度方向，并更新到 TransformComponent
        /// </summary>
        /// <param name="entityId">实体 ID</param>
        private void SyncEntityPositionAndRotation(int entityId)
        {
            Entity entity = new Entity(entityId);

            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            if (navigator.RvoAgentId < 0)
                return;

            // 从 RVO 模拟器获取位置
            Vector2 pos = Simulator.Instance.getAgentPosition(navigator.RvoAgentId);            
            zfloat x = zfloat.CreateFloat((long)(pos.x() * 10000));
            zfloat y = zfloat.CreateFloat((long)(pos.y() * 10000));
            zVector2 newV2Pos = new(x, y);

            // zUDebug.Log($"[RVO] SyncEntityPositionAndRotation {entityId} pos: {newV2Pos}");
            
            // 从 RVO 模拟器获取速度方向作为朝向
            Vector2 vel = Simulator.Instance.getAgentVelocity(navigator.RvoAgentId);
            if (vel.IsZero)
            {
                return;
            }

            Vector2 normalVel = RVOMath.normalize(vel);
            zVector3 forward = new(zfloat.FromFloat(normalVel.x()), zfloat.Zero, zfloat.FromFloat(normalVel.y()));

            // 同步位置和旋转到 TransformComponent
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            transform.Position.x = newV2Pos.x;
            transform.Position.z = newV2Pos.y;
            transform.Rotation = zQuaternion.LookRotation(forward);

            ComponentManager.AddComponent(entity, transform);
            // zUDebug.Log($"[RVO] SyncEntityPositionAndRotation: entityId={entity.Id}, newPos:{newV2Pos}, forward:{forward}, vel:{vel}");
        }

        /// <summary>
        /// 移除实体的导航能力
        /// </summary>
        public void RemoveNavigator(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            // 释放流场
            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
                navigator.CurrentFlowFieldId = -1;
                navigator.HasReachedTarget = false;
            }

            // 将 RVO 代理归还池并冻结
            if (navigator.RvoAgentId >= 0)
            {
                // rvoSimulator.RemoveAgent(navigator.RvoAgentId);
                Simulator.Instance.delAgent(navigator.RvoAgentId);
                navigator.RvoAgentId = -1;
            }

            // 移除组件
            ComponentManager.RemoveComponent<FlowFieldNavigatorComponent>(entity);
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);
        }

    }
}