using System.Collections.Generic;
using System.Linq;
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
    /// </summary>
    public class FlowFieldNavigationSystem : BaseSystem
    {
        private FlowFieldManager flowFieldManager;
        private RVO2Simulator rvoSimulator;
        private IFlowFieldMap map;
        
        private struct PooledAgent
        {
            public int AgentId;
            public zfloat Radius;
            public zfloat MaxSpeed;
        }

        private List<PooledAgent> rvoFreeList = new List<PooledAgent>();

        // Debug: 最近一次散点集合
        private readonly List<zVector2> debugScatterPoints = new List<zVector2>();
        public System.Collections.Generic.IReadOnlyList<zVector2> DebugScatterPoints => debugScatterPoints;
        private zVector2 debugScatterCenter = zVector2.zero;
        private zfloat debugScatterRadius = zfloat.Zero;
        public zVector2 DebugScatterCenter => debugScatterCenter;
        public zfloat DebugScatterRadius => debugScatterRadius;

        /// <summary>
        /// 初始化流场导航系统
        /// 在系统注册到World后调用
        /// </summary>
        public void InitializeNavigation(FlowFieldManager ffMgr, RVO2Simulator rvoSim, IFlowFieldMap gameMap)
        {
            flowFieldManager = ffMgr;
            rvoSimulator = rvoSim;
            map = gameMap;
        }

        protected override void OnInitialize()
        {
            // 系统初始化
        }

        /// <summary>
        /// 为实体添加流场导航能力
        /// </summary>
        public void AddNavigator(Entity entity, zfloat radius, zfloat maxSpeed)
        {
            // 获取实体位置
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 pos2D = new zVector2(transform.Position.x, transform.Position.z);

            // 添加导航组件
            var navigator = FlowFieldNavigatorComponent.Create(radius, maxSpeed);
            
            // 在RVO中创建智能体
            // 优化的RVO参数，减少卡死在角落的情况
            int rvoAgentId = -1;
            // 先尝试从池中复用同参数代理
            int poolIndex = rvoFreeList.FindIndex(p => p.Radius == radius && p.MaxSpeed == maxSpeed);
            if (poolIndex >= 0)
            {
                var pooled = rvoFreeList[poolIndex];
                rvoFreeList.RemoveAt(poolIndex);
                rvoAgentId = pooled.AgentId;
                rvoSimulator.SetAgentPosition(rvoAgentId, pos2D);
                rvoSimulator.SetAgentPrefVelocity(rvoAgentId, zVector2.zero);
            }
            else
            {
                rvoAgentId = rvoSimulator.AddAgent(
                    pos2D,
                    radius,
                    maxSpeed,
                    maxNeighbors: 10,
                    timeHorizon: new zfloat(1, 5000)  // 降低到1.5，减少过早反应
                );
            }
            navigator.RvoAgentId = rvoAgentId;

            ComponentManager.AddComponent(entity, navigator);
        }

        /// <summary>
        /// 设置实体的移动目标
        /// </summary>
        public void SetMoveTarget(Entity entity, zVector2 targetPos)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);

            // 释放旧流场
            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
            }

            // 请求新流场
            navigator.CurrentFlowFieldId = flowFieldManager.RequestFlowField(targetPos);
            navigator.HasReachedTarget = false;
            // 重置卡住状态
            navigator.StuckFrames = 0;
            navigator.LastPosition = new zVector2(transform.Position.x, transform.Position.z);
            navigator.NearSlowFrames = 0;

            // 更新导航组件
            ComponentManager.AddComponent(entity, navigator);

            // 添加或更新目标组件
            var target = MoveTargetComponent.Create(targetPos);
            ComponentManager.AddComponent(entity, target);
        }

        /// <summary>
        /// 批量设置多个实体的目标（常见于RTS选中操作）
        /// </summary>
        public void SetMultipleTargets(List<Entity> entities, zVector2 targetPos)
        {
            foreach (var entity in entities)
            {
                SetMoveTarget(entity, targetPos);
            }
        }

        /// <summary>
        /// 为多个单位设置散点目标，复用同一个多源流场
        /// </summary>
        public void SetScatterTargets(List<Entity> entities, zVector2 groupCenter)
        {
            if (entities == null || entities.Count == 0)
                return;

            // 统计最大半径，确定最小间距
            zfloat maxRadius = zfloat.Zero;
            foreach (var e in entities)
            {
                var nav = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(e);
                if (nav.Radius > maxRadius) maxRadius = nav.Radius;
            }
            // 基础间距：约 4x 半径，并至少为 2 个网格尺寸，保证明显分散
            zfloat spacing = maxRadius * new zfloat(4);
            zfloat minSpacing = map.GetGridSize() * new zfloat(2);
            if (spacing < minSpacing) spacing = minSpacing;

            // 生成候选散点（蜂窝格）
            // 计算所需环数：1 + 3r(r+1) 覆盖点数 >= N
            int ringsNeeded = 0;
            while (1 + 3 * ringsNeeded * (ringsNeeded + 1) < entities.Count) ringsNeeded++;
            int rings = System.Math.Max(6, ringsNeeded + 1);
            var candidates = GenerateHexLattice(groupCenter, spacing, rings, entities.Count * 6);

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
                    rings += 4;
                    candidates = GenerateHexLattice(groupCenter, spacing, rings, entities.Count * 8);
                    attempts++;
                }
            }
            if (scatterPoints.Count == 0)
                return;

            // 请求共享多源流场
            int fieldId = flowFieldManager.RequestFlowFieldMultiWorld(scatterPoints);
            if (fieldId < 0)
                return;

            // 贪心分配散点
            var assigned = GreedyAssign(entities, scatterPoints);

            // 记录 Debug 散点
            debugScatterPoints.Clear();
            debugScatterPoints.AddRange(scatterPoints);
            debugScatterCenter = groupCenter;
            // 半径取最大距离
            zfloat maxR = zfloat.Zero;
            foreach (var p in scatterPoints)
            {
                zfloat d = (p - groupCenter).magnitude;
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
                zfloat minArrival = navigator.Radius * new zfloat(0, 15000); // 1.5x radius
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

        private List<zVector2> GenerateHexLattice(zVector2 center, zfloat spacing, int rings, int maxCount)
        {
            List<zVector2> pts = new List<zVector2>();
            zfloat half = new zfloat(0, 5000);
            zfloat sqrt3over2 = new zfloat(0, 8660);
            zVector2 a = new zVector2(spacing, zfloat.Zero);
            zVector2 b = new zVector2(spacing * half, spacing * sqrt3over2);

            for (int r = 0; r <= rings; r++)
            {
                for (int i = -r; i <= r; i++)
                {
                    for (int j = -r; j <= r; j++)
                    {
                        zVector2 p = center + a * (zfloat)i + b * (zfloat)j;
                        pts.Add(p);
                        if (pts.Count >= maxCount) goto END;
                    }
                }
            }
        END:
            // 按距中心排序（zfloat 不实现 IComparable，使用自定义比较器）
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
        /// 清除实体的移动目标
        /// </summary>
        public void ClearMoveTarget(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
                navigator.CurrentFlowFieldId = -1;
                navigator.HasReachedTarget = false;

                ComponentManager.AddComponent(entity, navigator);
            }

            // 移除目标组件
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);

            // 停止RVO智能体
            if (navigator.RvoAgentId >= 0)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
            }
        }

        /// <summary>
        /// 系统更新（每帧调用）
        /// </summary>
        public override void Update()
        {
            if (flowFieldManager == null || rvoSimulator == null)
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

            // 3. RVO统一计算避障
            rvoSimulator.DoStep(DeltaTime);

            // 4. 同步位置回Transform组件
            foreach (var entityId in navigatorEntities)
            {
                Entity entity = new Entity(entityId);
                SyncPosition(entity);
            }
        }

        /// <summary>
        /// 更新单个实体的导航
        /// </summary>
        private void UpdateEntityNavigation(Entity entity)
        {
            // 检查单位是否存活，死亡单位不能移动（只检查DeathComponent）
            if (ComponentManager.HasComponent<DeathComponent>(entity))
            {
                ClearMoveTarget(entity);
                return;
            }

            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            if (!navigator.IsEnabled)
                return;

            // 没有目标
            if (!ComponentManager.HasComponent<MoveTargetComponent>(entity))
            {
                if (navigator.RvoAgentId >= 0)
                {
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                }
                return;
            }

            if (navigator.CurrentFlowFieldId < 0)
                return;

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            var target = ComponentManager.GetComponent<MoveTargetComponent>(entity);

            zVector2 currentPos = new zVector2(transform.Position.x, transform.Position.z);

            // 到达判定统一为世界距离（移除基于代价的判定）

            // ===== 卡住检测 =====
            zfloat moveDistance = (currentPos - navigator.LastPosition).magnitude;
            zfloat minMoveThreshold = new zfloat(0, 100); // 0.01单位

            if (moveDistance < minMoveThreshold)
            {
                navigator.StuckFrames++;
            }
            else
            {
                navigator.StuckFrames = 0;
            }
            navigator.LastPosition = currentPos;

            // 从流场获取方向
            zVector2 flowDirection = flowFieldManager.SampleDirection(navigator.CurrentFlowFieldId, currentPos);

            // 当流场方向为零时（通常是在目标格子内），直接朝目标点移动
            if (flowDirection == zVector2.zero)
            {
                zVector2 toTarget = target.TargetPosition - currentPos;
                zfloat distSq = toTarget.sqrMagnitude;
                
                // 如果距离很近（小于到达半径的平方），停止移动
                zfloat arrivalRadiusSq = navigator.ArrivalRadius * navigator.ArrivalRadius;
                if (distSq <= arrivalRadiusSq)
                {
                    navigator.HasReachedTarget = true;
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                    ComponentManager.AddComponent(entity, navigator);
                    ClearMoveTarget(entity);
                    return;
                }
                
                // 否则，直接朝目标移动（用于目标格子内的精确移动）
                flowDirection = toTarget.normalized;
            }

            // 预先计算与目标距离（供扰动/减速/到达使用）
            zfloat distanceToTarget = (target.TargetPosition - currentPos).magnitude;

            // ===== 卡住处理：添加随机扰动（仅远离目标时生效） =====
            if (navigator.StuckFrames > 20 && distanceToTarget > navigator.SlowDownRadius) // 远离目标才扰动
            {
                // 添加垂直于当前方向的随机扰动
                zfloat randomValue = new zfloat((entity.Id * 137 + World.Tick * 17) % 1000 - 500, 1000); // -0.5 到 0.5
                zVector2 perpendicular = new zVector2(-flowDirection.y, flowDirection.x);
                flowDirection = (flowDirection + perpendicular * randomValue * new zfloat(0, 5000)).normalized; // 混入50%扰动
                
                // 每60帧重置一次计数，避免持续添加扰动
                if (navigator.StuckFrames > 60)
                {
                    navigator.StuckFrames = 0;
                }
            }

            // ===== 近端个体化收敛：靠近个人散点时忽略流场，直达个人目标 =====
            // 目的：多源流场指向“最近散点”，可能与个人分配散点不同，近端阶段改为直达个人目标
            zfloat personalizationRadius = navigator.Radius * new zfloat(4);
            zfloat grid3 = map.GetGridSize() * new zfloat(3);
            if (personalizationRadius < grid3) personalizationRadius = grid3;
            if (distanceToTarget <= personalizationRadius)
            {
                zVector2 toPersonal = (target.TargetPosition - currentPos);
                if (toPersonal.sqrMagnitude > zfloat.Epsilon)
                {
                    flowDirection = toPersonal.normalized;
                }
            }

            // 计算速度（靠近目标时平滑减速，至到达半径处速度为0，避免抖动）
            zfloat speed = navigator.MaxSpeed;
            if (distanceToTarget < navigator.SlowDownRadius)
            {
                zfloat denom = navigator.SlowDownRadius - navigator.ArrivalRadius;
                if (denom <= zfloat.Zero) denom = navigator.SlowDownRadius; // 兜底
                zfloat t = (distanceToTarget - navigator.ArrivalRadius) / denom; // t∈(-∞,1]
                if (t < zfloat.Zero) t = zfloat.Zero;
                if (t > zfloat.One) t = zfloat.One;
                speed = navigator.MaxSpeed * t;
            }

            // ===== 再次检查是否到达（更精确的判定） =====
            // 加入轻微回滞：到达半径 + 网格尺寸20%的死区，减少临界抖动
            zfloat arriveDeadband = map.GetGridSize() * new zfloat(0, 2000);
            if (distanceToTarget <= navigator.ArrivalRadius + arriveDeadband)
            {
                // 已经到达目标，停止移动
                navigator.HasReachedTarget = true;
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                ComponentManager.AddComponent(entity, navigator);
                ClearMoveTarget(entity);
                return;
            }

            // ===== 近端稳定判定：低速连续N帧即判到达（进一步去抖） =====
            if (distanceToTarget <= navigator.SlowDownRadius)
            {
                zfloat lowSpeedThreshold = new zfloat(0, 500); // 0.05 m/s
                bool isLowSpeed = false;
                if (ComponentManager.HasComponent<VelocityComponent>(entity))
                {
                    var velComp = ComponentManager.GetComponent<VelocityComponent>(entity);
                    zVector2 lastVel2D = new zVector2(velComp.Value.x, velComp.Value.z);
                    isLowSpeed = lastVel2D.magnitude <= lowSpeedThreshold;
                }
                if (isLowSpeed)
                {
                    navigator.NearSlowFrames++;
                    if (navigator.NearSlowFrames >= 6)
                    {
                        navigator.HasReachedTarget = true;
                        rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                        ComponentManager.AddComponent(entity, navigator);
                        ClearMoveTarget(entity);
                        return;
                    }
                }
                else
                {
                    navigator.NearSlowFrames = 0;
                }
            }

            // ===== 设置期望旋转方向到RotationStateComponent =====
            if (ComponentManager.HasComponent<RotationStateComponent>(entity))
            {
                var rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
                if (flowDirection.magnitude > zfloat.Epsilon)
                {
                    rotationState.DesiredDirection = flowDirection.normalized;
                    ComponentManager.AddComponent(entity, rotationState);
                }
            }

            // ===== 检查是否正在原地转向 =====
            bool isInPlaceRotating = false;
            if (ComponentManager.HasComponent<RotationStateComponent>(entity))
            {
                var rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
                isInPlaceRotating = rotationState.IsInPlaceRotating;
            }

            // 如果正在原地转向，停止移动
            if (isInPlaceRotating)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                ComponentManager.AddComponent(entity, navigator);
                return;
            }

            // ===== 提前预测碰撞 =====
            zVector2 desiredVelocity = flowDirection * speed;
            
            // 预测下一帧位置
            zVector2 predictedPos = currentPos + desiredVelocity * DeltaTime;
            map.WorldToGrid(predictedPos, out int predGridX, out int predGridY);
            
            // 如果预测位置是障碍物，调整方向
            if (!map.IsWalkable(predGridX, predGridY))
            {
                // 尝试滑动：投影到切线方向
                desiredVelocity = GetSlideVelocity(currentPos, desiredVelocity, map);
            }
            
            // 设置期望速度（RVO会处理避障）
            rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, desiredVelocity);

            // 保存更新后的导航组件
            ComponentManager.AddComponent(entity, navigator);
        }

        /// <summary>
        /// 计算滑动速度（沿墙滑行）
        /// 当预测到碰撞时，将速度投影到墙壁的切线方向
        /// </summary>
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
        /// 同步RVO位置到Transform组件
        /// </summary>
        private void SyncPosition(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            if (navigator.RvoAgentId < 0)
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
                rvoSimulator.SetAgentPosition(navigator.RvoAgentId, freezePos);

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
            if (ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                zVector2 vel2D = rvoSimulator.GetAgentVelocity(navigator.RvoAgentId);
                var velocity = new VelocityComponent(new zVector3(vel2D.x, zfloat.Zero, vel2D.y));
                ComponentManager.AddComponent(entity, velocity);
            }
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
            }

            // 将 RVO 代理归还池并冻结
            if (navigator.RvoAgentId >= 0)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                rvoFreeList.Add(new PooledAgent
                {
                    AgentId = navigator.RvoAgentId,
                    Radius = navigator.Radius,
                    MaxSpeed = navigator.MaxSpeed
                });
                 rvoSimulator.RemoveAgent(navigator.RvoAgentId);
            }

            // 移除组件
            ComponentManager.RemoveComponent<FlowFieldNavigatorComponent>(entity);
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);
        }
    }
}
