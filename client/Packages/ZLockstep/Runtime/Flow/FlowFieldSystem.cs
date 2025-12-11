using System.Collections.Generic;
using zUnity;
using ZLockstep.RVO;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;

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
        private RVO2Simulator rvoSimulator;
        private IFlowFieldMap map;

        /// <summary>
        /// 是否启用RVO避障算法
        /// 如果为false，将直接使用流场方向移动，不使用RVO进行避障
        /// </summary>
        private bool useRvo = true;

        /// <summary>
        /// 设置是否启用RVO避障算法
        /// </summary>
        /// <param name="enabled">是否启用RVO</param>
        public void SetUseRvo(bool enabled)
        {
            useRvo = enabled;
        }

        /// <summary>
        /// 获取是否启用RVO避障算法
        /// </summary>
        public bool GetUseRvo()
        {
            return useRvo;
        }

        private struct PooledAgent
        {
            public int AgentId;
            public zfloat Radius;
            public zfloat MaxSpeed;
        }

        // Debug: 最近一次散点集合
        private readonly List<zVector2> debugScatterPoints = new List<zVector2>();
        public IReadOnlyList<zVector2> DebugScatterPoints => debugScatterPoints;
        private zVector2 debugScatterCenter = zVector2.zero;
        private zfloat debugScatterRadius = zfloat.Zero;
        public zVector2 DebugScatterCenter => debugScatterCenter;
        public zfloat DebugScatterRadius => debugScatterRadius;

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
            rvoSimulator = rvoSim;
            map = gameMap;

            // 只有在启用RVO时才设置RVO相关引用
            if (useRvo && rvoSimulator != null)
            {
                // 设置RVO模拟器引用到流场管理器
                flowFieldManager.SetRvoSimulator(rvoSimulator);

                // 设置流场管理器引用到RVO模拟器
                rvoSimulator.SetFlowFieldManager(flowFieldManager);
            }
        }

        protected override void OnInitialize()
        {
            // 系统初始化
        }

        /// <summary>
        /// 为实体添加流场导航能力
        /// 主要功能：
        /// 1. 获取实体当前位置
        /// 2. 创建导航组件
        /// 3. 创建或复用RVO避障代理
        /// 4. 将导航组件与实体关联
        /// </summary>
        /// <param name="entity">需要添加导航能力的实体</param>
        /// <param name="radius">实体的半径（用于碰撞检测）</param>
        /// <param name="maxSpeed">实体的最大移动速度</param>
        public void AddNavigator(Entity entity, zfloat radius, zfloat maxSpeed)
        {
            // 获取实体位置
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 pos2D = new zVector2(transform.Position.x, transform.Position.z);

            // 添加导航组件
            var navigator = FlowFieldNavigatorComponent.Create(radius, maxSpeed);

            // 只有在启用RVO时才创建RVO智能体
            if (useRvo && rvoSimulator != null)
            {
                // 在RVO中创建智能体
                // 优化的RVO参数，减少卡死在角落的情况
                int rvoAgentId = rvoSimulator.AddAgent(
                        pos2D,
                        radius,
                        maxSpeed,
                        maxNeighbors: 10,
                        timeHorizon: new zfloat(1, 5000)  // 降低到1.5，减少过早反应
                    );
                navigator.RvoAgentId = rvoAgentId;
            }
            else
            {
                navigator.RvoAgentId = -1;
            }

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
        public void SetMoveTarget(Entity entity, zVector2 targetPos)
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

            // 在请求新流场之前，移除该实体作为动态障碍物（仅在启用RVO时）
            if (useRvo && navigator.RvoAgentId >= 0 && rvoSimulator != null)
            {
                var map = flowFieldManager.GetMap();
                if (map is SimpleMapManager simpleMap)
                {
                    simpleMap.RemoveDynamicObstacle(navigator.RvoAgentId);
                }
                // 重置智能体的静止状态
                rvoSimulator.ResetAgentStationaryState(navigator.RvoAgentId);
                flowFieldManager.MarkDynamicObstaclesNeedUpdate();
            }

            // 请求新流场
            navigator.CurrentFlowFieldId = flowFieldManager.RequestFlowField(alignedTargetPos);
            navigator.HasReachedTarget = false;
            // 重置卡住状态
            navigator.StuckFrames = 0;
            navigator.LastPosition = new zVector2(transform.Position.x, transform.Position.z);
            navigator.NearSlowFrames = 0;

            // 更新导航组件
            ComponentManager.AddComponent(entity, navigator);

            // 添加或更新目标组件
            var target = MoveTargetComponent.Create(alignedTargetPos);
            ComponentManager.AddComponent(entity, target);
        }

        /// <summary>
        /// 批量设置多个实体的目标（常见于RTS选中操作）
        /// </summary>
        /// <param name="entities">需要设置目标的实体列表</param>
        /// <param name="targetPos">目标位置</param>
        public void SetMultipleTargets(List<Entity> entities, zVector2 targetPos)
        {
            foreach (var entity in entities)
            {
                SetMoveTarget(entity, targetPos);
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
        public void SetScatterTargets(List<Entity> entities, zVector2 groupCenter)
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

            // 在请求新流场之前，移除所有实体作为动态障碍物（仅在启用RVO时）
            if (useRvo && rvoSimulator != null)
            {
                foreach (var e in entities)
                {
                    var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(e);
                    if (navigator.RvoAgentId >= 0)
                    {
                        var map = flowFieldManager.GetMap();
                        if (map is SimpleMapManager simpleMap)
                        {
                            simpleMap.RemoveDynamicObstacle(navigator.RvoAgentId);
                        }
                        // 重置智能体的静止状态
                        rvoSimulator.ResetAgentStationaryState(navigator.RvoAgentId);
                    }
                }
                flowFieldManager.MarkDynamicObstaclesNeedUpdate();
            }

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

        /// <summary>
        /// 生成圆形分布的候选点
        /// 主要功能：
        /// 1. 围绕中心点以同心圆方式生成候选点集
        /// 2. 根据指定的环数和每环点数生成点
        /// 3. 按距离中心点的远近对生成的点进行排序
        /// </summary>
        /// <param name="center">中心点位置</param>
        /// <param name="spacing">点之间的间隔距离</param>
        /// <param name="rings">生成的环数</param>
        /// <param name="pointsPerRing">每环的点数</param>
        /// <param name="maxCount">生成点的最大数量</param>
        /// <returns>按距离排序的候选点列表</returns>
        private List<zVector2> GenerateCircularLattice(zVector2 center, zfloat spacing, int rings, int pointsPerRing, int maxCount)
        {
            List<zVector2> pts = new List<zVector2>();

            // 添加中心点
            // 将中心点对齐到网格中心
            map.WorldToGrid(center, out int centerGridX, out int centerGridY);
            zVector2 alignedCenter = map.GridToWorld(centerGridX, centerGridY);
            pts.Add(alignedCenter);
            if (maxCount <= 1)
                return pts;

            for (int ring = 1; ring <= rings; ring++)
            {
                zfloat radius = spacing * (zfloat)ring;
                int pointsInThisRing = pointsPerRing * ring;

                for (int i = 0; i < pointsInThisRing; i++)
                {
                    zfloat angle = (zfloat)i * zMathf.PI * 2 / (zfloat)pointsInThisRing;
                    zVector2 p = new zVector2(
                        center.x + radius * zMathf.Cos(angle),
                        center.y + radius * zMathf.Sin(angle)
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
                zfloat d1 = (p1 - alignedCenter).sqrMagnitude;
                zfloat d2 = (p2 - alignedCenter).sqrMagnitude;
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

            // 停止RVO智能体（仅在启用RVO时）
            if (useRvo && navigator.RvoAgentId >= 0 && rvoSimulator != null)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
            }
        }

        /// <summary>
        /// 系统更新（每帧调用）
        /// 主要功能：
        /// 1. 更新流场管理器
        /// 2. 为每个有导航组件的实体设置期望速度
        /// 3. 执行RVO避障计算
        /// 4. 同步位置回Transform组件
        /// </summary>
        public override void Update()
        {
            if (flowFieldManager == null)
                return;

            // 如果启用RVO但RVO模拟器为空，则返回
            if (useRvo && rvoSimulator == null)
                return;

            // 1. 更新流场管理器（处理脏流场）
            flowFieldManager.Tick();

            // 2. 为每个有导航组件的实体设置期望速度
            var navigatorEntities = ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
            foreach (var entityId in navigatorEntities)
            {
                Entity entity = new Entity(entityId);
                UpdateEntityNavigation(entity);
            }

            // 3. RVO统一计算避障（仅在启用RVO时）
            if (useRvo && rvoSimulator != null)
            {
                rvoSimulator.DoStep(DeltaTime);
            }

            // 4. 同步位置回Transform组件
            foreach (var entityId in navigatorEntities)
            {
                Entity entity = new Entity(entityId);
                SyncPosition(entity);
            }
        }

        /// <summary>
        /// 更新单个实体的导航
        /// 主要功能：
        /// 1. 检查实体是否存活或有移动能力
        /// 2. 获取流场方向并计算期望速度
        /// 3. 处理卡住检测和随机扰动
        /// 4. 实现到达目标检测
        /// 5. 设置RVO期望速度
        /// </summary>
        /// <summary>
        /// 更新单个实体的导航
        /// </summary>
        private void UpdateEntityNavigation(Entity entity)
        {
            // 1. 基础检查
            if (ComponentManager.HasComponent<DeathComponent>(entity))
            {
                ClearMoveTarget(entity);
                return;
            }

            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            if (!navigator.IsEnabled) return;

            // 2. 目标检查
            if (!ComponentManager.HasComponent<MoveTargetComponent>(entity))
            {
                if (useRvo && navigator.RvoAgentId >= 0 && rvoSimulator != null)
                {
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                }
                else if (!useRvo && ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));
                }
                return;
            }

            if (navigator.CurrentFlowFieldId < 0) return;

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            var target = ComponentManager.GetComponent<MoveTargetComponent>(entity);
            zVector2 currentPos = new zVector2(transform.Position.x, transform.Position.z);

            // ===== 卡住检测逻辑 (保持不变) =====
            zfloat moveDistance = (currentPos - navigator.LastPosition).magnitude;
            zfloat minMoveThreshold = new zfloat(0, 100);
            if (moveDistance < minMoveThreshold) navigator.StuckFrames++;
            else navigator.StuckFrames = 0;
            navigator.LastPosition = currentPos;

            // 3. 获取基础流场方向
            zVector2 flowDirection = flowFieldManager.SampleDirection(navigator.CurrentFlowFieldId, currentPos);

            // 处理目标格子内的移动
            zVector2 toTarget = target.TargetPosition - currentPos;
            zfloat distSq = toTarget.sqrMagnitude;
            zfloat distanceToTarget = zMathf.Sqrt(distSq);

            if (flowDirection == zVector2.zero)
            {
                zfloat arrivalRadiusSq = navigator.ArrivalRadius * navigator.ArrivalRadius;
                if (distSq <= arrivalRadiusSq)
                {
                    navigator.HasReachedTarget = true;
                    ComponentManager.AddComponent(entity, navigator);
                    ClearMoveTarget(entity, true);
                    return;
                }
                flowDirection = toTarget.normalized;
            }

            // ===== 个体化收敛逻辑 (保持不变) =====
            zfloat personalizationRadius = navigator.Radius * new zfloat(4);
            zfloat grid3 = map.GetGridSize() * new zfloat(3);
            if (personalizationRadius < grid3) personalizationRadius = grid3;

            if (distanceToTarget <= personalizationRadius)
            {
                if (distSq > zfloat.Epsilon) flowDirection = toTarget.normalized;
            }

            // ===== 速度计算 (保持不变) =====
            zfloat speed = navigator.MaxSpeed;
            if (distanceToTarget < navigator.SlowDownRadius)
            {
                zfloat denom = navigator.SlowDownRadius - navigator.ArrivalRadius;
                if (denom <= zfloat.Zero) denom = navigator.SlowDownRadius;
                zfloat t = (distanceToTarget - navigator.ArrivalRadius) / denom;
                if (t < zfloat.Zero) t = zfloat.Zero;
                if (t > zfloat.One) t = zfloat.One;
                speed = navigator.MaxSpeed * t;
            }

            // ===== 到达判定与去抖动 (保持不变) =====
            zfloat arriveDeadband = map.GetGridSize() * new zfloat(0, 2000);
            if (distanceToTarget <= navigator.ArrivalRadius + arriveDeadband)
            {
                navigator.HasReachedTarget = true;
                ComponentManager.AddComponent(entity, navigator);
                ClearMoveTarget(entity, true);
                return;
            }

            // 低速判定逻辑
            if (distanceToTarget <= navigator.SlowDownRadius)
            {
                zfloat lowSpeedThreshold = new zfloat(0, 500);
                bool isLowSpeed = false;
                if (ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    var vel = ComponentManager.GetComponent<VelocityComponent>(entity);
                    if (new zVector2(vel.Value.x, vel.Value.z).magnitude <= lowSpeedThreshold)
                        isLowSpeed = true;
                }

                if (isLowSpeed) navigator.NearSlowFrames++;
                else navigator.NearSlowFrames = 0;

                if (navigator.NearSlowFrames >= 6)
                {
                    navigator.HasReachedTarget = true;
                    ComponentManager.AddComponent(entity, navigator);
                    ClearMoveTarget(entity, true);
                    return;
                }
            }

            // =========================================================================================
            // [核心修复] 原地旋转判定与组件设置
            // =========================================================================================
            bool needStopToTurn = false;
            zVector2 desiredDir = zVector2.zero;

            // 只有当期望方向有效时才进行判定
            if (flowDirection.sqrMagnitude > zfloat.Epsilon)
            {
                desiredDir = flowDirection.normalized;
                zVector2 currentForward = GetEntityForward2D(transform);

                // 计算点积 (1.0 = 同向, 0 = 垂直, -1 = 反向)
                zfloat dot = zVector2.Dot(currentForward, desiredDir);

                // 获取之前的状态
                bool isAlreadyRotating = false;
                RotationStateComponent rotationState;
                bool hasRotationComp = ComponentManager.HasComponent<RotationStateComponent>(entity);

                if (hasRotationComp)
                {
                    rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
                    isAlreadyRotating = rotationState.IsInPlaceRotating;
                }
                else
                {
                    // 如果还没有组件，初始化一个
                    rotationState = RotationStateComponent.Create(desiredDir);
                }

                // --- 双阈值滞后逻辑 (Hysteresis) ---
                // 进入阈值：夹角大于约 45 度时开始旋转 (Cos 45 ≈ 0.707)
                zfloat enterTurnThreshold = new zfloat(0, 7071);
                // 退出阈值：夹角小于约 10 度时才结束旋转 (Cos 10 ≈ 0.9848)
                zfloat exitTurnThreshold = new zfloat(0, 9848);

                if (isAlreadyRotating)
                {
                    // 如果已经在旋转中，必须转得比较正了才停止 (防止在这个临界点抖动)
                    if (dot < exitTurnThreshold)
                    {
                        needStopToTurn = true;
                    }
                }
                else
                {
                    // 如果正在移动中，只有偏差比较大才开始原地旋转
                    if (dot < enterTurnThreshold)
                    {
                        needStopToTurn = true;
                    }
                }

                // --- 更新 RotationStateComponent ---
                // 这就是你问的“后续设置代码”部分，必须显式更新组件
                rotationState.DesiredDirection = desiredDir;
                rotationState.IsInPlaceRotating = needStopToTurn;
                ComponentManager.AddComponent(entity, rotationState);
            }

            // [逻辑分支] 如果需要原地旋转，强制停止移动
            if (needStopToTurn)
            {
                // RVO 停止
                if (useRvo && navigator.RvoAgentId >= 0 && rvoSimulator != null)
                {
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                }
                // 非 RVO 停止
                else if (!useRvo && ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));
                }

                // 更新导航组件状态并退出，不再执行后续的位置预测
                ComponentManager.AddComponent(entity, navigator);
                return;
            }

            // =========================================================================================
            // 以下是正常的移动逻辑 (当不需要原地旋转时执行)
            // =========================================================================================

            zVector2 desiredVelocity = flowDirection * speed;

            // 预测与滑墙处理
            zVector2 predictedPos = currentPos + desiredVelocity * DeltaTime;
            map.WorldToGrid(predictedPos, out int predGridX, out int predGridY);

            if (!map.IsWalkable(predGridX, predGridY))
            {
                desiredVelocity = GetSlideVelocity(currentPos, desiredVelocity, map);
            }

            // 应用速度到 RVO 或 组件
            if (useRvo && navigator.RvoAgentId >= 0 && rvoSimulator != null)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, desiredVelocity);
            }
            else if (!useRvo && ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                var velocity = new VelocityComponent(new zVector3(desiredVelocity.x, zfloat.Zero, desiredVelocity.y));
                ComponentManager.AddComponent(entity, velocity);
            }

            ComponentManager.AddComponent(entity, navigator);
        }

        /// <summary>
        /// 从Transform组件获取当前的2D前向向量
        /// </summary>
        private zVector2 GetEntityForward2D(TransformComponent transform)
        {
            // 假设 TransformComponent 中有 Rotation (zQuaternion)
            // 如果你的架构不同，请调整此处
            zQuaternion rotation = transform.Rotation;

            // 计算三维 Forward
            zVector3 forward3D = rotation * zVector3.forward;

            // 投影到 2D 平面并归一化
            zVector2 forward2D = new zVector2(forward3D.x, forward3D.z);

            if (forward2D.sqrMagnitude > zfloat.Epsilon)
            {
                return forward2D.normalized;
            }

            // 默认方向 (防止异常)
            return new zVector2(zfloat.Zero, zfloat.One);
        }

        /// <summary>
        /// 计算滑动速度（沿墙滑行）
        /// 当预测到碰撞时，将速度投影到墙壁的切线方向
        /// </summary>
        /// <param name="currentPos">当前位置</param>
        /// <param name="desiredVelocity">期望速度</param>
        /// <param name="map">地图接口</param>
        /// <returns>沿墙滑行的速度向量</returns>
        private zVector2 GetSlideVelocity(zVector2 currentPos, zVector2 desiredVelocity, IFlowFieldMap map)
        {
            // 检测碰撞法线方向
            zVector2 wallNormal = DetectWallNormal(currentPos, desiredVelocity, map);

            if (wallNormal == zVector2.zero)
            {
                // 没有检测到明确的墙壁方向，保持原速度
                return desiredVelocity;
            }

            // 将速度投影到墙壁切线（垂直于法线）
            // 切线 = 旋转法线90度
            zVector2 wallTangent = new zVector2(-wallNormal.y, wallNormal.x);

            // 投影：保留沿切线的分量
            zfloat projection = zVector2.Dot(desiredVelocity, wallTangent);
            zVector2 slideVelocity = wallTangent * projection;

            // 保持原速度的大小（只改方向）
            zfloat originalSpeed = desiredVelocity.magnitude;
            if (slideVelocity.magnitude > zfloat.Epsilon)
            {
                return slideVelocity.normalized * originalSpeed;
            }

            // 如果投影为零，尝试反方向
            return -desiredVelocity * new zfloat(0, 5000); // 50%速度后退
        }

        /// <summary>
        /// 检测墙壁法线方向
        /// 通过检查周围格子，找出障碍物的方向
        /// </summary>
        /// <param name="currentPos">当前位置</param>
        /// <param name="moveDir">移动方向</param>
        /// <param name="map">地图接口</param>
        /// <returns>墙壁法线方向向量</returns>
        private zVector2 DetectWallNormal(zVector2 currentPos, zVector2 moveDir, IFlowFieldMap map)
        {
            zVector2 checkPos = currentPos + moveDir.normalized * map.GetGridSize();
            map.WorldToGrid(checkPos, out int gx, out int gy);

            if (!map.IsWalkable(gx, gy))
            {
                // 最简单的法线：从当前位置指向碰撞点的反方向
                zVector2 toObstacle = map.GridToWorld(gx, gy) - currentPos;
                return -toObstacle.normalized;
            }

            // 检查8个方向，找到障碍物的平均方向
            int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] DY = { 1, 1, 0, -1, -1, -1, 0, 1 };

            zVector2 avgNormal = zVector2.zero;
            int obstacleCount = 0;

            map.WorldToGrid(currentPos, out int cx, out int cy);
            for (int i = 0; i < 8; i++)
            {
                int nx = cx + DX[i];
                int ny = cy + DY[i];

                if (!map.IsWalkable(nx, ny))
                {
                    // 从当前格子指向障碍物格子的方向
                    zVector2 toObstacle = new zVector2((zfloat)DX[i], (zfloat)DY[i]);
                    avgNormal -= toObstacle; // 法线是反方向
                    obstacleCount++;
                }
            }

            if (obstacleCount > 0)
            {
                return avgNormal.normalized;
            }

            return zVector2.zero;
        }

        /// <summary>
        /// 非RVO模式下的位置同步
        /// 直接从VelocityComponent计算位置更新
        /// </summary>
        private void SyncPositionWithoutRvo(Entity entity, FlowFieldNavigatorComponent navigator)
        {
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 currentPos = new zVector2(transform.Position.x, transform.Position.z);

            // 如果已到达目标，停止移动
            if (navigator.HasReachedTarget)
            {
                if (ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    var velocity = new VelocityComponent(new zVector3(zfloat.Zero, zfloat.Zero, zfloat.Zero));
                    ComponentManager.AddComponent(entity, velocity);
                }
                return;
            }

            // 检查是否正在原地转向
            if (ComponentManager.HasComponent<RotationStateComponent>(entity))
            {
                var rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
                if (rotationState.IsInPlaceRotating)
                {
                    if (ComponentManager.HasComponent<VelocityComponent>(entity))
                    {
                        var velocity = new VelocityComponent(new zVector3(zfloat.Zero, zfloat.Zero, zfloat.Zero));
                        ComponentManager.AddComponent(entity, velocity);
                    }
                    return;
                }
            }

            // 从VelocityComponent获取速度并更新位置
            zVector2 vel2D = zVector2.zero;
            if (ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                var velocityComp = ComponentManager.GetComponent<VelocityComponent>(entity);
                vel2D = new zVector2(velocityComp.Value.x, velocityComp.Value.z);

                // 计算新位置
                zVector2 newPos = currentPos + vel2D * DeltaTime;

                // 碰撞检测：检查新位置是否在障碍物里
                if (map != null)
                {
                    map.WorldToGrid(newPos, out int gridX, out int gridY);

                    // 如果新位置不可行走，回退到旧位置并停止移动
                    if (!map.IsWalkable(gridX, gridY))
                    {
                        newPos = currentPos;
                        var velocity = new VelocityComponent(new zVector3(zfloat.Zero, zfloat.Zero, zfloat.Zero));
                        ComponentManager.AddComponent(entity, velocity);
                        vel2D = zVector2.zero;
                    }
                    else
                    {
                        // 更新Transform位置
                        transform.Position.x = newPos.x;
                        transform.Position.z = newPos.y;
                        ComponentManager.AddComponent(entity, transform);
                    }
                }
                else
                {
                    // 更新Transform位置
                    transform.Position.x = newPos.x;
                    transform.Position.z = newPos.y;
                    ComponentManager.AddComponent(entity, transform);
                }
            }

            // ===== 基于实际速度方向更新DesiredDirection（非RVO模式） =====
            // 在非RVO模式下，速度方向应该与流场方向一致，但为了保持一致性也更新
            if (vel2D.sqrMagnitude > zfloat.Epsilon)
            {
                zVector2 actualDirection = vel2D.normalized;

                if (ComponentManager.HasComponent<RotationStateComponent>(entity))
                {
                    var rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
                    rotationState.DesiredDirection = actualDirection;
                    ComponentManager.AddComponent(entity, rotationState);
                }
                else
                {
                    var rotationState = RotationStateComponent.Create(actualDirection);
                    ComponentManager.AddComponent(entity, rotationState);
                }
            }
        }

        /// <summary>
        /// 同步RVO位置到Transform组件
        /// 
        /// 核心功能：将RVO模拟器中计算出的智能体位置和速度同步到游戏实体的相关组件中
        /// 
        /// 实现逻辑：
        /// 1. 获取RVO模拟器中的智能体位置和实体当前的Transform位置
        /// 2. 如果实体已到达目标，则冻结代理（速度为零，位置对齐Transform）
        /// 3. 如果实体正在原地转向，则冻结位置更新但允许旋转
        /// 4. 检测新位置是否在障碍物中，如果是则回退到旧位置并停止移动
        /// 5. 更新Transform组件的位置为RVO模拟器中的新位置
        /// 6. 同步智能体速度到VelocityComponent组件（如果存在）
        /// </summary>
        private void SyncPosition(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            // 如果未启用RVO，直接从速度计算位置
            if (!useRvo)
            {
                SyncPositionWithoutRvo(entity, navigator);
                return;
            }

            // RVO模式下，需要RVO代理ID有效
            if (navigator.RvoAgentId < 0 || rvoSimulator == null)
                return;

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 oldPos = new zVector2(transform.Position.x, transform.Position.z);
            zVector2 newPos = rvoSimulator.GetAgentPosition(navigator.RvoAgentId);

            // ===== 检查是否已到达目标 =====
            // 如果已经标记为到达，确保速度为零
            if (navigator.HasReachedTarget)
            {
                // 冻结代理：速度为零，位置对齐 Transform
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                zVector2 freezePos = new zVector2(transform.Position.x, transform.Position.z);
                // 对齐网格
                flowFieldManager.GetMap().WorldToGrid(freezePos, out int gx, out int gy);
                zVector2 fixedPos = new zVector2(gx, gy);
                rvoSimulator.SetAgentPosition(navigator.RvoAgentId, fixedPos);
                // freezePos transform.Position
                // zUDebug.Log($"navigator.RvoAgentId: {navigator.RvoAgentId}, freezePos: {freezePos}, transform.Position: {fixedPos}");

                // 同步零速度到 VelocityComponent（若存在）
                if (ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    var velocity = new VelocityComponent(new zVector3(zfloat.Zero, zfloat.Zero, zfloat.Zero));
                    ComponentManager.AddComponent(entity, velocity);
                }
                return;
            }

            // ===== 检查是否正在原地转向 =====
            // 原地转向时冻结位置更新，只允许旋转
            if (ComponentManager.HasComponent<RotationStateComponent>(entity))
            {
                var rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
                if (rotationState.IsInPlaceRotating)
                {
                    // 冻结位置：重置RVO代理位置到当前Transform位置
                    rvoSimulator.SetAgentPosition(navigator.RvoAgentId, oldPos);

                    // 同步零速度到 VelocityComponent（若存在）
                    if (ComponentManager.HasComponent<VelocityComponent>(entity))
                    {
                        var velocity = new VelocityComponent(new zVector3(zfloat.Zero, zfloat.Zero, zfloat.Zero));
                        ComponentManager.AddComponent(entity, velocity);
                    }
                    return;
                }
            }

            // ===== 碰撞检测：检查新位置是否在障碍物里 =====
            if (map != null)
            {
                map.WorldToGrid(newPos, out int gridX, out int gridY);

                // 如果新位置不可行走，回退到旧位置
                if (!map.IsWalkable(gridX, gridY))
                {
                    // 回退到旧位置
                    newPos = oldPos;
                    rvoSimulator.SetAgentPosition(navigator.RvoAgentId, oldPos);

                    // 停止速度，避免继续尝试进入障碍物
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                }
            }

            transform.Position.x = newPos.x;
            transform.Position.z = newPos.y;
            ComponentManager.AddComponent(entity, transform);

            // 同步速度到VelocityComponent（如果有）
            zVector2 vel2D = zVector2.zero;
            if (ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                vel2D = rvoSimulator.GetAgentVelocity(navigator.RvoAgentId);
                var velocity = new VelocityComponent(new zVector3(vel2D.x, zfloat.Zero, vel2D.y));
                ComponentManager.AddComponent(entity, velocity);
            }
            else
            {
                vel2D = rvoSimulator.GetAgentVelocity(navigator.RvoAgentId);
            }

            // =========================================================================================
            // [优化] 解决抖动问题：速度死区 + 方向平滑
            // =========================================================================================

            // 1. 速度死区：只有速度大于一定阈值时才更新朝向
            // 0.1 * 0.1 = 0.01 (建议根据你的世界单位比例调整，这里假设1单位=1米)
            zfloat rotationSpeedThresholdSq = new zfloat(0, 100); // 0.01f

            if (vel2D.sqrMagnitude > rotationSpeedThresholdSq)
            {
                zVector2 targetDir = vel2D.normalized;

                // 获取当前的 RotationState
                RotationStateComponent rotationState;
                if (ComponentManager.HasComponent<RotationStateComponent>(entity))
                {
                    rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);

                    // 2. 方向平滑：不要直接赋值，而是与上一次的 DesiredDirection 进行插值
                    // smoothingFactor 越小越平滑，但响应越慢。建议 0.1 ~ 0.3
                    zfloat smoothingFactor = new zfloat(0, 2000); // 0.2f

                    // 只有当上一次方向有效时才插值
                    if (rotationState.DesiredDirection.sqrMagnitude > zfloat.Epsilon)
                    {
                        // 使用 Lerp 平滑过渡，然后归一化
                        // 注意：zVector2.Lerp 需要你确认 zUnity 库中是否有，没有则手动实现： a + (b-a)*t
                        zVector2 smoothedDir = zVector2.Lerp(rotationState.DesiredDirection, targetDir, smoothingFactor);
                        targetDir = smoothedDir.normalized;
                    }
                }
                else
                {
                    rotationState = RotationStateComponent.Create(targetDir);
                }

                // 更新组件
                rotationState.DesiredDirection = targetDir;
                ComponentManager.AddComponent(entity, rotationState);
            }
            // 如果速度很小，保持当前的 DesiredDirection 不变，防止低速下的随机旋转
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

            // 将 RVO 代理归还池并冻结（仅在启用RVO时）
            if (useRvo && navigator.RvoAgentId >= 0 && rvoSimulator != null)
            {
                rvoSimulator.RemoveAgent(navigator.RvoAgentId);
                navigator.RvoAgentId = -1;
            }

            // 移除组件
            ComponentManager.RemoveComponent<FlowFieldNavigatorComponent>(entity);
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);
        }

    }
}