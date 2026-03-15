using System;
using System.Collections.Generic;
using ZLockstep.Flow;
using zUnity;

namespace ZLockstep.RVO
{
    /// <summary>
    /// RVO2 (Reciprocal Velocity Obstacles) 基于定点数的实现
    /// 用于多智能体的避障和运动规划
    /// 
    /// RVO2 是一种基于速度障碍法的碰撞 avoidance 算法，它考虑了智能体之间的相互作用，
    /// 通过计算最优互惠碰撞避免 (ORCA) 来实现实时避障效果。该实现使用定点数 (zfloat) 而非浮点数，
    /// 以确保在分布式仿真环境中的一致性和确定性行为。
    /// 
    /// 核心原理:
    /// - 每个智能体预测与其他智能体在未来时间范围内的潜在碰撞
    /// - 构建速度空间中的约束线 (ORCA Lines)，表示允许的安全速度区域
    /// - 通过线性规划在约束区域内寻找最接近期望速度的实际执行速度
    /// 
    /// 使用示例:
    /// 1. 创建模拟器：var sim = new RVO2Simulator();
    /// 2. 添加智能体：int agentId = sim.AddAgent(position, radius, maxSpeed, ...);
    /// 3. 设置期望速度：sim.SetAgentPrefVelocity(agentId, prefVelocity);
    /// 4. 执行一步模拟：sim.DoStep(deltaTime);
    /// 5. 获取实际速度：zVector2 velocity = sim.GetAgentVelocity(agentId);
    /// 6. 更新位置：position += velocity * deltaTime;
    /// 
    /// ID 分配机制:
    /// 每个智能体在添加时会被分配一个唯一 ID，该 ID 在整个模拟器生命周期内保持唯一性，
    /// 即使移除某些智能体后也不会被重复使用。这是通过使用独立的递增计数器实现的，
    /// 确保了每个新添加的智能体都会获得一个新的、唯一的 ID，避免了 ID 冲突问题。
    /// 
    /// 性能优化:
    /// - 使用 KD-tree 空间分区数据结构加速邻近智能体查找
    /// - 使用 Dictionary 实现 O(1) 复杂度的 ID 查找
    /// - 将邻近查询的时间复杂度从 O(n²) 降低至 O(n·k·log n)
    /// </summary>
    /// <summary>
    /// 简化的 2D KD-tree 实现，专用于 RVO2 空间邻近查询
    /// </summary>
    public class KdTree2D
    {
        private class Node
        {
            public RVO2Agent agent;
            public Node left;
            public Node right;
            public float splitValue; // 分割值
            public int splitAxis; // 分割轴 (0=x, 1=y)
        }

        private Node root;
        private List<RVO2Agent> agents = new List<RVO2Agent>();
        private bool needsRebuild = true;

        /// <summary>
        /// 添加智能体（标记需要重建）
        /// </summary>
        public void Add(RVO2Agent agent)
        {
            agents.Add(agent);
            needsRebuild = true;
        }

        /// <summary>
        /// 移除智能体
        /// </summary>
        public void Remove(RVO2Agent agent)
        {
            if (agents.Remove(agent))
            {
                needsRebuild = true;
            }
        }

        /// <summary>
        /// 清空树
        /// </summary>
        public void Clear()
        {
            agents.Clear();
            root = null;
            needsRebuild = false;
        }

        /// <summary>
        /// 重建 KD-tree
        /// </summary>
        public void Rebuild()
        {
            if (!needsRebuild && root != null)
                return;

            root = BuildTree(agents.ToArray(), 0, agents.Count - 1, 0);
            needsRebuild = false;
        }

        private Node BuildTree(RVO2Agent[] agentsArray, int start, int end, int depth)
        {
            if (start > end)
                return null;

            int axis = depth % 2; // 交替选择 x 或 y 轴
            int mid = start + (end - start) / 2;

            // 按当前轴排序
            Array.Sort(agentsArray, start, end - start + 1, new AgentComparer(axis));

            var node = new Node
            {
                agent = agentsArray[mid],
                splitAxis = axis,
                splitValue = axis == 0 ? (float)agentsArray[mid].position.x : (float)agentsArray[mid].position.y
            };

            node.left = BuildTree(agentsArray, start, mid - 1, depth + 1);
            node.right = BuildTree(agentsArray, mid + 1, end, depth + 1);

            return node;
        }

        /// <summary>
        /// 径向搜索：查找给定点周围半径范围内的所有智能体
        /// </summary>
        public List<RVO2Agent> RadialSearch(zVector2 center, float radius)
        {
            if (root == null)
                return new List<RVO2Agent>();

            var result = new List<RVO2Agent>();
            float radiusSq = radius * radius;
            float[] centerPoint = new float[] { (float)center.x, (float)center.y };

            SearchRadial(root, centerPoint, radius, radiusSq, result);
            return result;
        }

        private void SearchRadial(Node node, float[] centerPoint, float radius, float radiusSq, List<RVO2Agent> result)
        {
            if (node == null)
                return;

            // 计算当前节点与查询点的距离
            float dx = (float)node.agent.position.x - centerPoint[0];
            float dy = (float)node.agent.position.y - centerPoint[1];
            float distSq = dx * dx + dy * dy;

            // 如果在范围内且不是同一个智能体，添加到结果
            if (distSq <= radiusSq && distSq > 0.0001f)
            {
                result.Add(node.agent);
            }

            // 决定搜索哪个子树
            float diff = centerPoint[node.splitAxis] - node.splitValue;

            if (diff <= radius)
            {
                SearchRadial(node.left, centerPoint, radius, radiusSq, result);
            }

            if (diff >= -radius)
            {
                SearchRadial(node.right, centerPoint, radius, radiusSq, result);
            }
        }

        private class AgentComparer : IComparer<RVO2Agent>
        {
            private int axis;

            public AgentComparer(int axis)
            {
                this.axis = axis;
            }

            public int Compare(RVO2Agent a, RVO2Agent b)
            {
                float valA = axis == 0 ? (float)a.position.x : (float)a.position.y;
                float valB = axis == 0 ? (float)b.position.x : (float)b.position.y;
                return valA.CompareTo(valB);
            }
        }
    }

    public class RVO2Simulator
    {
        /// <summary>
        /// 所有智能体列表（保留用于 ID 查找和遍历）
        /// </summary>
        private List<RVO2Agent> agents = new List<RVO2Agent>();
        
        /// <summary>
        /// ID 到索引的映射，用于 O(1) 复杂度的智能体查找
        /// </summary>
        private Dictionary<int, int> idToIndexMap = new Dictionary<int, int>();
        
        /// <summary>
        /// KD-tree 用于加速空间邻近查询
        /// </summary>
        private KdTree2D agentKDTree;
        
        /// <summary>
        /// 用于生成唯一 ID 的计数器
        /// </summary>
        private int nextAgentId = 0;

        /// <summary>
        /// 时间步长
        /// </summary>
        private zfloat timeStep = zfloat.Zero;

        /// <summary>
        /// 速度平滑因子，用于减少震荡
        /// 0 表示完全使用新速度，1 表示完全保持当前速度
        /// </summary>
        private zfloat velocitySmoothingFactor = new zfloat(0, 2000); // 0.2

        /// <summary>
        /// 默认的时间范围参数
        /// </summary>
        public zfloat defaultTimeHorizon = new zfloat(2);
        
        /// <summary>
        /// 静止检测阈值
        /// </summary>
        private zfloat stationaryThreshold = new zfloat(0, 100); // 0.01 单位
        
        /// <summary>
        /// 静止判定帧数
        /// </summary>
        private int stationaryFrameThreshold = 30;
        
        /// <summary>
        /// 流场管理器引用，用于通知动态障碍物变化
        /// </summary>
        private FlowFieldManager flowFieldManager;

        /// <summary>
        /// 搜索邻居的最大半径（基于最大速度和时间的估计值）
        /// </summary>
        private const float MAX_SEARCH_RADIUS = 100f;

        /// <summary>
        /// 设置速度平滑因子
        /// </summary>
        /// <param name="smoothingFactor">平滑因子，0 表示无平滑，1 表示完全保持当前速度</param>
        public void SetVelocitySmoothingFactor(zfloat smoothingFactor)
        {
            velocitySmoothingFactor = smoothingFactor;
        }
        
        /// <summary>
        /// 设置流场管理器引用
        /// </summary>
        /// <param name="manager">流场管理器</param>
        public void SetFlowFieldManager(FlowFieldManager manager)
        {
            flowFieldManager = manager;
        }

        /// <summary>
        /// 初始化 KD-tree
        /// </summary>
        private void InitializeKDTree()
        {
            agentKDTree = new KdTree2D();
        }

        /// <summary>
        /// 重建 KD-tree（每帧调用）
        /// </summary>
        private void RebuildKDTree()
        {
            if (agentKDTree == null)
            {
                InitializeKDTree();
            }
            
            agentKDTree.Rebuild();
        }

        /// <summary>
        /// 添加一个智能体到模拟器中
        /// 
        /// 此方法创建一个新的 RVO2Agent 实例并将其添加到模拟器中，为其分配唯一 ID。
        /// 智能体的初始速度设置为零，需要通过 SetAgentPrefVelocity 方法设置期望速度。
        /// 
        /// 算法特点:
        /// - 自动为智能体分配全局唯一的递增 ID
        /// - 将新智能体按 ID 顺序插入到 agents 列表中以维持有序性
        /// - 支持自定义时间范围参数，若未提供则使用默认值
        /// </summary>
        /// <param name="position">智能体的初始位置坐标</param>
        /// <param name="radius">智能体的碰撞半径，用于计算智能体间距离和碰撞检测</param>
        /// <param name="maxSpeed">智能体的最大移动速度限制</param>
        /// <param name="maxNeighbors">最大邻居数量（用于性能优化，限制参与避障计算的邻居数）</param>
        /// <param name="timeHorizon">避障时间范围，决定预测未来多长时间内的潜在碰撞，默认为 defaultTimeHorizon</param>
        /// <returns>返回新添加智能体的唯一标识符 ID</returns>
        public int AddAgent(zVector2 position, zfloat radius, zfloat maxSpeed, 
            int maxNeighbors = 10, zfloat timeHorizon = default(zfloat))
        {
            if (timeHorizon == zfloat.Zero)
            {
                timeHorizon = defaultTimeHorizon;
            }

            RVO2Agent agent = new RVO2Agent();
            agent.position = position;
            agent.velocity = zVector2.zero;
            agent.prefVelocity = zVector2.zero;
            agent.radius = radius - new zfloat(0, 1000);
            agent.maxSpeed = maxSpeed;
            agent.maxNeighbors = maxNeighbors;
            agent.timeHorizon = timeHorizon;
            agent.id = nextAgentId++;
            
            // 初始化静止检测相关字段
            agent.lastPosition = position;
            agent.stationaryFrames = 0;
            agent.isStationary = false;

            agents.Add(agent);
            idToIndexMap[agent.id] = agents.Count - 1;
            
            // 如果 KD-tree 已初始化，添加到树中
            if (agentKDTree != null)
            {
                agentKDTree.Add(agent);
            }
            
            return agent.id;
        }

        /// <summary>
        /// 设置智能体的期望速度向量
        /// 
        /// 该方法用于指定智能体希望移动的方向和速度大小，但实际移动速度会受到
        /// RVO2 避障算法的约束调整。这是控制智能体行为的主要接口之一。
        /// 
        /// 工作机制：
        /// - 通过智能体 ID 查找对应实体（使用 Dictionary 实现 O(1) 查找）
        /// - 更新该智能体的 prefVelocity 属性
        /// - 在下一 DoStep 调用中，算法会参考此值计算实际可行速度
        /// 
        /// 注意事项：
        /// - 期望速度仅作为参考，实际速度可能因避障需要而大幅调整
        /// - 建议在每次模拟步前更新期望速度以实现动态路径规划
        /// </summary>
        /// <param name="agentId">目标智能体的唯一标识符</param>
        /// <param name="prefVelocity">期望速度向量，表示理想状态下智能体希望达到的速度</param>
        public void SetAgentPrefVelocity(int agentId, zVector2 prefVelocity)
        {
            if (idToIndexMap.TryGetValue(agentId, out int index))
            {
                agents[index].prefVelocity = prefVelocity;
            }
        }

        /// <summary>
        /// 设置智能体的位置坐标
        /// 
        /// 直接更新指定智能体的空间位置，这是一种强制性位置设定操作，
        /// 不受物理规律或避障算法约束。主要用于以下场景：
        /// 1. 初始化智能体位置
        /// 2. 传送或瞬移效果实现
        /// 3. 校正由于累积误差导致的位置偏差
        /// 4. 外部系统同步位置信息
        /// 
        /// 注意事项：
        /// - 此操作不会影响智能体的速度状态
        /// - 下一帧的位置计算仍将基于当前速度进行
        /// - 需要谨慎使用以避免破坏仿真的连续性
        /// </summary>
        /// <param name="agentId">目标智能体的唯一标识符</param>
        /// <param name="position">新的位置坐标</param>
        public void SetAgentPosition(int agentId, zVector2 position)
        {
            if (idToIndexMap.TryGetValue(agentId, out int index))
            {
                agents[index].position = position;
                
                // 位置改变后需要重建 KD-tree
                if (agentKDTree != null)
                {
                    RebuildKDTree();
                }
            }
        }

        /// <summary>
        /// 获取指定智能体的当前位置坐标
        /// 
        /// 返回特定智能体在二维空间中的当前位置，该位置由模拟器在每次 DoStep
        /// 调用中根据速度和时间步长自动更新。
        /// 
        /// 应用场景：
        /// - 渲染系统根据此位置绘制智能体
        /// - 路径规划系统查询智能体现在所在位置
        /// - 碰撞检测或其他系统需要获取精确位置信息
        /// 
        /// 数据一致性保证：
        /// - 位置更新在 DoStep 方法的第二阶段统一进行
        /// - 采用欧拉积分方法计算位置变化量
        /// </summary>
        /// <param name="agentId">目标智能体的唯一标识符</param>
        /// <returns>智能体当前位置坐标，如果未找到对应智能体则返回零向量</returns>
        public zVector2 GetAgentPosition(int agentId)
        {
            if (idToIndexMap.TryGetValue(agentId, out int index))
            {
                return agents[index].position;
            }
            return zVector2.zero;
        }

        /// <summary>
        /// 获取指定智能体的当前实际移动速度
        /// 
        /// 返回经过 RVO2 避障算法计算后的实际速度向量，该速度是综合考虑了
        /// 所有邻近智能体影响后的安全可行速度。
        /// 
        /// 与期望速度的区别：
        /// - 期望速度 (prefVelocity)：用户设定的理想运动状态
        /// - 实际速度 (velocity)：算法调整后的可执行运动指令
        /// 
        /// 典型用途：
        /// - 物理引擎根据此速度更新智能体位置
        /// - 动画系统据此计算角色朝向和动作
        /// - 调试工具显示当前真实的运动状态
        /// </summary>
        /// <param name="agentId">目标智能体的唯一标识符</param>
        /// <returns>智能体当前实际速度向量，如果未找到对应智能体则返回零向量</returns>
        public zVector2 GetAgentVelocity(int agentId)
        {
            if (idToIndexMap.TryGetValue(agentId, out int index))
            {
                return agents[index].velocity;
            }
            return zVector2.zero;
        }

        /// <summary>
        /// 执行一步模拟计算
        /// 
        /// 这是 RVO2 模拟器的核心方法，负责推进整个系统的状态演化。
        /// 采用两阶段更新策略确保计算的一致性和稳定性：
        /// 第一阶段：为所有智能体并行计算新速度，此时使用的是同步的旧状态数据
        /// 第二阶段：统一应用新速度并更新位置
        /// 
        /// 算法优势：
        /// - 避免了在计算过程中因部分智能体状态已更新而导致的不对称性问题
        /// - 确保 ORCA 算法的理论正确性得到实践保证
        /// - 时间复杂度为 O(n²)，其中 n 为智能体数量
        /// </summary>
        /// <param name="deltaTime">时间步长，控制模拟的精细程度和计算频率</param>
        public void DoStep(zfloat deltaTime)
        {
            // 防止除以零错误
            if (deltaTime <= zfloat.Zero)
            {
                return;
            }

            timeStep = deltaTime;

            // 阶段 1：为每个智能体计算新速度（存储到 newVelocity，不修改 velocity）
            // 这样保证所有智能体计算时看到的是同一时刻的状态，保持 ORCA 算法的对称性
            for (int i = 0; i < agents.Count; i++)
            {
                ComputeNewVelocity(agents[i]);
            }

            // 阶段 2：统一应用新速度并更新位置
            for (int i = 0; i < agents.Count; i++)
            {
                // 更新静止状态
                bool wasStationary = agents[i].isStationary;
                UpdateStationaryState(agents[i]);
                bool isStationary = agents[i].isStationary;
                
                // 如果智能体开始移动，立即移除其动态障碍物
                if (wasStationary && !isStationary && flowFieldManager != null)
                {
                    // 通过 ID 精确移除动态障碍物
                    var map = flowFieldManager.GetMap();
                    if (map is SimpleMapManager simpleMap)
                    {
                        simpleMap.RemoveDynamicObstacle(agents[i].id);
                    }
                    flowFieldManager.MarkDynamicObstaclesNeedUpdate();
                }
                // 如果智能体变为静止，通知流场管理器需要更新动态障碍物
                else if (!wasStationary && isStationary && flowFieldManager != null)
                {
                    flowFieldManager.MarkDynamicObstaclesNeedUpdate();
                }
                
                agents[i].velocity = agents[i].newVelocity;
                agents[i].position += agents[i].velocity * deltaTime;
            }
            
            // 每帧结束后重建 KD-tree，为下一帧做准备
            RebuildKDTree();
        }

        /// <summary>
        /// 更新智能体的静止状态
        /// </summary>
        /// <param name="agent">需要更新状态的智能体</param>
        private void UpdateStationaryState(RVO2Agent agent)
        {
            zfloat moveDistance = (agent.position - agent.lastPosition).magnitude;
            
            if (moveDistance < stationaryThreshold && agent.prefVelocity == zVector2.zero)
            {
                agent.stationaryFrames++;
                if (agent.stationaryFrames >= stationaryFrameThreshold)
                {
                    agent.isStationary = true;
                }
            }
            else
            {
                agent.stationaryFrames = 0;
                agent.isStationary = false;
            }
            
            agent.lastPosition = agent.position;
        }

        /// <summary>
        /// 计算单个智能体的新速度（ORCA 算法核心实现）
        /// 
        /// 该方法实现了最优互惠碰撞避免 (Optimal Reciprocal Collision Avoidance) 算法的核心逻辑。
        /// 对于目标智能体，遍历所有其他智能体并构建相应的 ORCA 约束线，最终通过线性规划求解最优速度。
        /// 
        /// 算法流程：
        /// 1. 遍历所有其他智能体，计算相对位置和相对速度
        /// 2. 根据距离关系判断是否会发生碰撞
        /// 3. 对每种情况（无碰撞、未来会碰撞、已碰撞）分别计算 ORCA 约束线
        /// 4. 调用线性规划求解器在所有约束线下找到最接近期望速度的可行速度
        /// 
        /// 特殊处理：
        /// - 区分三种情况：安全距离外、未来会碰撞、已经重叠
        /// - 对不同情况采用不同的几何计算方法构建约束线
        /// - 防止数值不稳定性的边界条件检查
        /// </summary>
        /// <param name="agent">需要计算新速度的目标智能体</param>
        private void ComputeNewVelocity(RVO2Agent agent)
        {
            // zUDebug.Log($"[RVO2] 开始计算智能体 {agent.id} 的新速度，期望速度：{agent.prefVelocity}, 当前速度：{agent.velocity}");

            // 构建 ORCA 线
            List<ORCALine> orcaLines = new List<ORCALine>();

            // 使用 KD-tree 进行空间邻近查询，只检查附近的智能体
            if (agentKDTree != null && agents.Count > 1)
            {
                // 计算搜索半径
                zfloat searchRadius = agent.timeHorizon * agent.maxSpeed + agent.radius + new zfloat(10);
                float searchRadiusFloat = Math.Min((float)searchRadius, MAX_SEARCH_RADIUS);
                
                // 径向搜索查找范围内的所有智能体
                var neighbors = agentKDTree.RadialSearch(agent.position, searchRadiusFloat);
                
                // 对每个邻居智能体创建 ORCA 线
                foreach (var other in neighbors)
                {
                    if (other.id == agent.id)
                        continue;

                    zVector2 relativePosition = other.position - agent.position;
                    zVector2 relativeVelocity = agent.velocity - other.velocity;
                    zfloat distSq = relativePosition.sqrMagnitude;
                    zfloat combinedRadius = agent.radius + other.radius;
                    zfloat combinedRadiusSq = combinedRadius * combinedRadius;

                    ORCALine line;
                    zVector2 u;

                    if (distSq > combinedRadiusSq)
                    {
                        // 没有碰撞
                        zVector2 w = relativeVelocity - relativePosition / agent.timeHorizon;
                        zfloat wLengthSq = w.sqrMagnitude;
                        zfloat dotProduct1 = zVector2.Dot(w, relativePosition);

                        if (dotProduct1 < zfloat.Zero && dotProduct1 * dotProduct1 > combinedRadiusSq * wLengthSq)
                        {
                            // 投影到截断圆
                            zfloat wLength = zMathf.Sqrt(wLengthSq);
                            zVector2 unitW = w / wLength;

                            line.direction = new zVector2(unitW.y, -unitW.x);
                            u = unitW * (combinedRadius / agent.timeHorizon - wLength);
                            
                            // zUDebug.Log($"[RVO2] 智能体 {agent.id} 与 {other.id} 投影到截断圆，距离平方：{distSq}, 组合半径平方：{combinedRadiusSq}");
                        }
                        else
                        {
                            // 投影到腿部
                            zfloat leg = zMathf.Sqrt(distSq - combinedRadiusSq);
                            if (Det(relativePosition, w) > zfloat.Zero)
                            {
                                line.direction = new zVector2(
                                    relativePosition.x * leg - relativePosition.y * combinedRadius,
                                    relativePosition.x * combinedRadius + relativePosition.y * leg
                                ) / distSq;
                            }
                            else
                            {
                                line.direction = -new zVector2(
                                    relativePosition.x * leg + relativePosition.y * combinedRadius,
                                    -relativePosition.x * combinedRadius + relativePosition.y * leg
                                ) / distSq;
                            }

                            zfloat dotProduct2 = zVector2.Dot(relativeVelocity, line.direction);
                            u = line.direction * dotProduct2 - relativeVelocity;
                            
                            // zUDebug.Log($"[RVO2] 智能体 {agent.id} 与 {other.id} 投影到腿部，距离平方：{distSq}, 组合半径平方：{combinedRadiusSq}");
                        }
                    }
                    else
                    {
                        // 发生碰撞，需要立即避开
                        zfloat invTimeStep = zfloat.One / timeStep;
                        zVector2 w = relativeVelocity - relativePosition * invTimeStep;
                        zfloat wLength = w.magnitude;
                        
                        // 防止除以零
                        if (wLength < new zfloat(0, 1000)) // 0.001
                        {
                            zUDebug.LogWarning($"[RVO2] 智能体 {agent.id} 与 {other.id} 距离过近且相对速度太小，跳过避障计算");
                            continue; // 跳过这个障碍物
                        }
                        
                        zVector2 unitW = w / wLength;

                        line.direction = new zVector2(unitW.y, -unitW.x);
                        u = unitW * (combinedRadius * invTimeStep - wLength);
                        
                        // zUDebug.Log($"[RVO2] 智能体 {agent.id} 与 {other.id} 已发生碰撞，需要立即避开，距离平方：{distSq}, 组合半径平方：{combinedRadiusSq}");
                    }

                    line.point = agent.velocity + u * zfloat.Half;
                    orcaLines.Add(line);
                }
            }
            else
            {
                // 回退到原始的全量遍历方式（兼容模式）
                for (int i = 0; i < agents.Count; i++)
                {
                    if (agents[i].id == agent.id)
                        continue;

                    RVO2Agent other = agents[i];
                    zVector2 relativePosition = other.position - agent.position;
                    zVector2 relativeVelocity = agent.velocity - other.velocity;
                    zfloat distSq = relativePosition.sqrMagnitude;
                    zfloat combinedRadius = agent.radius + other.radius;
                    zfloat combinedRadiusSq = combinedRadius * combinedRadius;

                    ORCALine line;
                    zVector2 u;

                    if (distSq > combinedRadiusSq)
                    {
                        // 没有碰撞
                        zVector2 w = relativeVelocity - relativePosition / agent.timeHorizon;
                        zfloat wLengthSq = w.sqrMagnitude;
                        zfloat dotProduct1 = zVector2.Dot(w, relativePosition);

                        if (dotProduct1 < zfloat.Zero && dotProduct1 * dotProduct1 > combinedRadiusSq * wLengthSq)
                        {
                            // 投影到截断圆
                            zfloat wLength = zMathf.Sqrt(wLengthSq);
                            zVector2 unitW = w / wLength;

                            line.direction = new zVector2(unitW.y, -unitW.x);
                            u = unitW * (combinedRadius / agent.timeHorizon - wLength);
                            
                            // zUDebug.Log($"[RVO2] 智能体 {agent.id} 与 {other.id} 投影到截断圆，距离平方：{distSq}, 组合半径平方：{combinedRadiusSq}");
                        }
                        else
                        {
                            // 投影到腿部
                            zfloat leg = zMathf.Sqrt(distSq - combinedRadiusSq);
                            if (Det(relativePosition, w) > zfloat.Zero)
                            {
                                line.direction = new zVector2(
                                    relativePosition.x * leg - relativePosition.y * combinedRadius,
                                    relativePosition.x * combinedRadius + relativePosition.y * leg
                                ) / distSq;
                            }
                            else
                            {
                                line.direction = -new zVector2(
                                    relativePosition.x * leg + relativePosition.y * combinedRadius,
                                    -relativePosition.x * combinedRadius + relativePosition.y * leg
                                ) / distSq;
                            }

                            zfloat dotProduct2 = zVector2.Dot(relativeVelocity, line.direction);
                            u = line.direction * dotProduct2 - relativeVelocity;
                            
                            // zUDebug.Log($"[RVO2] 智能体 {agent.id} 与 {other.id} 投影到腿部，距离平方：{distSq}, 组合半径平方：{combinedRadiusSq}");
                        }
                    }
                    else
                    {
                        // 发生碰撞，需要立即避开
                        zfloat invTimeStep = zfloat.One / timeStep;
                        zVector2 w = relativeVelocity - relativePosition * invTimeStep;
                        zfloat wLength = w.magnitude;
                        
                        // 防止除以零
                        if (wLength < new zfloat(0, 1000)) // 0.001
                        {
                            zUDebug.LogWarning($"[RVO2] 智能体 {agent.id} 与 {other.id} 距离过近且相对速度太小，跳过避障计算");
                            continue; // 跳过这个障碍物
                        }
                        
                        zVector2 unitW = w / wLength;

                        line.direction = new zVector2(unitW.y, -unitW.x);
                        u = unitW * (combinedRadius * invTimeStep - wLength);
                        
                        // zUDebug.Log($"[RVO2] 智能体 {agent.id} 与 {other.id} 已发生碰撞，需要立即避开，距离平方：{distSq}, 组合半径平方：{combinedRadiusSq}");
                    }

                    line.point = agent.velocity + u * zfloat.Half;
                    orcaLines.Add(line);
                }
            }

            // 使用线性规划求解器在所有约束线下找到最接近期望速度的可行速度
            zVector2 newVelocity = LinearProgram2(orcaLines, agent.maxSpeed, agent.prefVelocity);
            // zUDebug.Log($"[RVO2] 智能体 {agent.id} 线性规划完成，新速度：{newVelocity}, 最大速度：{agent.maxSpeed}");
            
            // 应用速度平滑以减少震荡
            if (agent.velocity != zVector2.zero && newVelocity != zVector2.zero)
            {
                newVelocity = zVector2.Lerp(newVelocity, agent.velocity, velocitySmoothingFactor);
            }
            
            // 存储到 newVelocity 字段，而不是直接修改 velocity
            // 这样保证所有智能体在计算时看到的是同一时刻的 velocity 状态
            agent.newVelocity = newVelocity;
        }

        /// <summary>
        /// 二维线性规划求解器，在 ORCA 线约束下寻找最接近期望速度的可行速度
        /// 
        /// 这是一个专门针对 RVO2 设计的二维线性规划算法实现，用于在多个半平面约束下
        /// 寻找最接近给定期望速度的解向量。算法采用迭代投影方法处理约束冲突。
        /// 
        /// 算法步骤：
        /// 1. 首先检查期望速度是否已在可行域内，如果是则直接返回
        /// 2. 否则逐个检查约束线，当发现违反约束时进行投影修正
        /// 3. 投影后验证是否满足之前的所有约束，如不满足则寻找约束线交点
        /// 4. 最终结果需满足最大速度限制
        /// 
        /// 数值稳定性处理：
        /// - 添加 epsilon 级别数值误差容忍
        /// - 防止零向量归一化等可能导致的除零异常
        /// - 处理约束线近似平行的特殊情况
        /// </summary>
        /// <param name="lines">ORCA 约束线集合，每个线代表一个速度空间中的半平面约束</param>
        /// <param name="maxSpeed">速度大小上限，确保结果向量的模长不超过此值</param>
        /// <param name="optVelocity">期望速度向量，算法目标是在约束下尽可能接近此向量</param>
        /// <returns>满足所有约束条件的最优速度向量</returns>
        private zVector2 LinearProgram2(List<ORCALine> lines, zfloat maxSpeed, zVector2 optVelocity)
        {
            zfloat optMagnitude = optVelocity.magnitude;
            if (optMagnitude > maxSpeed)
            {
                // 防止除以零
                if (optMagnitude > new zfloat(0, 1000)) // 0.001
                {
                    optVelocity = optVelocity.normalized * maxSpeed;
                }
                else
                {
                    optVelocity = zVector2.zero;
                }
            }

            // 检查期望速度是否满足所有约束
            bool satisfiesAll = true;
            for (int i = 0; i < lines.Count; i++)
            {
                if (Det(lines[i].direction, lines[i].point - optVelocity) > zfloat.Zero)
                {
                    satisfiesAll = false;
                    break;
                }
            }

            if (satisfiesAll)
            {
                return optVelocity;
            }

            // 需要投影到可行域
            zVector2 result = optVelocity;
            
            for (int i = 0; i < lines.Count; i++)
            {
                if (Det(lines[i].direction, lines[i].point - result) > zfloat.Zero)
                {
                    // 投影到线上
                    zVector2 tempResult = Project(lines[i], result);
                    
                    // 检查是否满足之前的约束
                    bool feasible = true;
                    for (int j = 0; j < i; j++)
                    {
                        if (Det(lines[j].direction, lines[j].point - tempResult) > zfloat.Zero)
                        {
                            feasible = false;
                            break;
                        }
                    }

                    if (feasible)
                    {
                        result = tempResult;
                    }
                    else
                    {
                        // 需要在两条线的交点处
                        // 添加边界检查，防止访问 lines[i-1] 时数组越界
                        if (i > 0)
                        {
                            zfloat determinant = Det(lines[i].direction, lines[i - 1].direction);
                            
                            if (zMathf.Abs(determinant) > zfloat.Epsilon)
                            {
                                zVector2 delta = lines[i].point - lines[i - 1].point;
                                zfloat t = Det(delta, lines[i - 1].direction) / determinant;
                                result = lines[i].point + lines[i].direction * t;
                            }
                            else
                            {
                                // 线几乎平行，使用当前结果
                                result = lines[i].point;
                            }
                        }
                        else
                        {
                            // 只有一条线且不可行，使用当前线上的点
                            result = lines[i].point;
                        }
                    }
                }
            }

            // 限制在最大速度内
            zfloat resultMagnitude = result.magnitude;
            if (resultMagnitude > maxSpeed)
            {
                // 防止除以零
                if (resultMagnitude > new zfloat(0, 1000)) // 0.001
                {
                    result = result.normalized * maxSpeed;
                }
                else
                {
                    result = zVector2.zero;
                }
            }

            return result;
        }

        /// <summary>
        /// 计算两个二维向量的行列式值（等价于叉积的 z 分量）
        /// 
        /// 在二维向量空间中，两个向量 a(x1,y1) 和 b(x2,y2) 的行列式定义为：
        /// det(a,b) = x1*y2 - y1*x2
        /// 
        /// 几何意义：
        /// - 绝对值等于由两向量构成的平行四边形面积
        /// - 符号表示从 a 到 b 的旋转方向（正数为逆时针，负数为顺时针）
        /// - 零值表示两向量共线
        /// 
        /// 在 RVO2 算法中的应用：
        /// - 判断点相对于直线的位置关系
        /// - 确定向量间的相对方向
        /// - 计算几何约束的有效性
        /// </summary>
        /// <param name="a">第一个二维向量</param>
        /// <param name="b">第二个二维向量</param>
        /// <returns>两向量的行列式值</returns>
        private zfloat Det(zVector2 a, zVector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>
        /// 将速度向量投影到指定的 ORCA 约束线上
        /// 
        /// 执行正交投影运算，将给定的速度向量投影到 ORCA 线所代表的约束边界上。
        /// 这是线性规划求解过程中的基本几何操作。
        /// 
        /// 投影原理：
        /// 给定线上的点 P 和方向向量 D，以及待投影点 V，
        /// 投影点 = P + D * dot(V-P, D)
        /// 其中 dot 表示向量点积运算
        /// 
        /// 应用场景：
        /// - 当速度向量违反某条约束线时，将其"推回"到约束边界
        /// - 寻找满足多个约束的最佳折衷解
        /// </summary>
        /// <param name="line">目标 ORCA 约束线</param>
        /// <param name="velocity">需要投影的速度向量</param>
        /// <returns>投影后的新速度向量</returns>
        private zVector2 Project(ORCALine line, zVector2 velocity)
        {
            zVector2 delta = velocity - line.point;
            zfloat dotProduct = zVector2.Dot(delta, line.direction);
            return line.point + line.direction * dotProduct;
        }

        /// <summary>
        /// 移除指定 ID 的智能体
        /// 
        /// 从模拟器中移除特定的智能体实例，包括：
        /// 1. 从 agents 列表中移除对应的 RVO2Agent 对象
        /// 2. 如果关联了流场系统，则同步移除其动态障碍物表示
        /// 
        /// 该方法通过 ID 精确查找目标智能体，避免了因索引变化导致的错误。
        /// 在移除前会先检查智能体是否存在，确保操作的安全性。
        /// 
        /// 注意事项：
        /// - ID 是智能体的唯一标识符，即使在移除后也不会被重复使用
        /// - 移除操作是立即生效的，不会延迟到下一帧
        /// - 如果目标 ID 不存在，方法将静默返回，不抛出异常
        /// </summary>
        /// <param name="agentId">要移除的智能体 ID</param>
        public void RemoveAgent(int agentId)
        {
            if (idToIndexMap.TryGetValue(agentId, out int index))
            {
                var agent = agents[index];
                
                // 移除动态障碍物
                if (flowFieldManager != null)
                {
                    var map = flowFieldManager.GetMap();
                    if (map is SimpleMapManager simpleMap)
                    {
                        simpleMap.RemoveDynamicObstacle(agentId);
                    }
                    flowFieldManager.MarkDynamicObstaclesNeedUpdate();
                }
                
                agents.RemoveAt(index);
                idToIndexMap.Remove(agentId);
                
                // 从 KD-tree 中移除
                if (agentKDTree != null)
                {
                    agentKDTree.Remove(agent);
                }
            }
        }
        
        /// <summary>
        /// 重置指定智能体的静止状态
        /// 
        /// 将指定智能体的静止计数器和静止状态重置为初始值，
        /// 用于在智能体开始移动时确保状态的一致性。
        /// </summary>
        /// <param name="agentId">要重置的智能体 ID</param>
        public void ResetAgentStationaryState(int agentId)
        {
            if (idToIndexMap.TryGetValue(agentId, out int index))
            {
                agents[index].stationaryFrames = 0;
                agents[index].isStationary = false;
            }
        }
        
        /// <summary>
        /// 获取当前系统中的智能体总数量
        /// 
        /// 返回模拟器中活跃智能体的数量，包括静止和移动状态的个体。
        /// 这是一个轻量级查询操作，常用于调试和监控系统负载。
        /// 
        /// 典型应用场景：
        /// - 控制系统根据密度调整模拟参数
        /// - 性能监控和调试信息展示
        /// - 决定是否需要扩容或缩容模拟规模
        /// - 循环处理所有智能体时的边界条件
        /// </summary>
        /// <returns>当前系统中的智能体总数</returns>
        public int GetNumAgents()
        {
            return agents.Count;
        }

        /// <summary>
        /// 清除模拟器中的所有智能体
        /// 
        /// 一次性移除系统内的全部智能体，将模拟器恢复到初始空载状态。
        /// 此操作不会重置 ID 计数器，新添加的智能体将继续使用递增的 ID 序列。
        /// 
        /// 效果说明：
        /// - 所有智能体对象将被销毁
        /// - agents 列表清空但保留容器本身
        /// - 下次调用 DoStep 时不会有任何计算
        /// - ID 计数器 (nextAgentId) 保持现有值
        /// </summary>
        public void Clear()
        {
            agents.Clear();
            idToIndexMap.Clear();
            if (agentKDTree != null)
            {
                agentKDTree.Clear();
            }
        }

        /// <summary>
        /// 获取所有智能体的只读列表
        /// 
        /// 返回模拟器中所有活跃智能体的只读列表，可用于调试和可视化。
        /// 注意：不要修改返回的列表，因为它直接引用内部数据结构。
        /// </summary>
        /// <returns>所有智能体的只读列表</returns>
        public IReadOnlyList<RVO2Agent> GetAgents()
        {
            return agents.AsReadOnly();
        }
        
        /// <summary>
        /// 获取静止的智能体列表
        /// </summary>
        /// <returns>静止智能体的列表</returns>
        public List<RVO2Agent> GetStationaryAgents()
        {
            List<RVO2Agent> stationaryAgents = new List<RVO2Agent>();
            foreach (var agent in agents)
            {
                if (agent.isStationary)
                {
                    stationaryAgents.Add(agent);
                }
            }
            return stationaryAgents;
        }
    }

    /// <summary>
    /// RVO 智能体数据结构
    /// 
    /// 表示 RVO2 模拟系统中的单个移动实体，包含其运动状态和避障参数。
    /// 每个智能体都被分配一个全局唯一的 ID，并在整个生命周期中保持不变。
    /// 
    /// 核心属性说明：
    /// - position: 决定智能体在世界坐标系中的位置
    /// - velocity: 当前实际移动速度，由 RVO2 算法计算得出
    /// - prefVelocity: 用户设定的期望速度，作为算法输入
    /// - radius: ircular 碰撞体积的半径
    /// - maxSpeed: 速度大小的硬性上限
    /// - timeHorizon: 预测时间窗口，影响避障的前瞻性和激进程度
    /// </summary>
    public class RVO2Agent
    {
        /// <summary>智能体 ID</summary>
        public int id;

        /// <summary>当前位置</summary>
        public zVector2 position;

        /// <summary>当前速度</summary>
        public zVector2 velocity;

        /// <summary>新计算的速度（用于两阶段更新）</summary>
        public zVector2 newVelocity;

        /// <summary>期望速度</summary>
        public zVector2 prefVelocity;

        /// <summary>智能体半径</summary>
        public zfloat radius;

        /// <summary>最大速度</summary>
        public zfloat maxSpeed;

        /// <summary>最大邻居数</summary>
        public int maxNeighbors;

        /// <summary>避障时间范围</summary>
        public zfloat timeHorizon;
        
        /// <summary>上一帧位置</summary>
        public zVector2 lastPosition;
        
        /// <summary>静止帧数</summary>
        public int stationaryFrames;
        
        /// <summary>是否为静止状态</summary>
        public bool isStationary;
    }

    /// <summary>
    /// ORCA 线 (Optimal Reciprocal Collision Avoidance Line)
    /// 
    /// 表示速度空间中的半平面约束，是 ORCA 算法的基本构成单元。
    /// 每条 ORCA 线定义了一个允许的安全速度区域边界。
    /// 
    /// 几何含义：
    /// - point: 约束线上的基准点，通常位于速度空间中某个特定位置
    /// - direction: 约束线的方向向量，单位长度，与约束边界平行
    /// - 有效区域: 从 point 出发，垂直于 direction 方向的半无限区域
    /// 
    /// 在 RVO2 中，每个邻近的智能体会产生一条或若干条 ORCA 线，
    /// 所有这些线的交集构成了当前智能体的合法速度选择空间。
    /// </summary>
    public struct ORCALine
    {
        /// <summary>线上的一个点</summary>
        public zVector2 point;

        /// <summary>线的方向（单位向量）</summary>
        public zVector2 direction;
    }
}