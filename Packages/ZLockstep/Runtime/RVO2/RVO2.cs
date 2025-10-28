using System.Collections.Generic;
using zUnity;

namespace ZLockstep.RVO
{
    /// <summary>
    /// RVO2 (Reciprocal Velocity Obstacles) 基于定点数的实现
    /// 用于多智能体的避障和运动规划
    /// 
    /// 使用示例:
    /// 1. 创建模拟器: var sim = new RVO2Simulator();
    /// 2. 添加智能体: int agentId = sim.AddAgent(position, radius, maxSpeed, ...);
    /// 3. 设置期望速度: sim.SetAgentPrefVelocity(agentId, prefVelocity);
    /// 4. 执行一步模拟: sim.DoStep(deltaTime);
    /// 5. 获取实际速度: zVector2 velocity = sim.GetAgentVelocity(agentId);
    /// 6. 更新位置: position += velocity * deltaTime;
    /// </summary>
    public class RVO2Simulator
    {
        /// <summary>
        /// 所有智能体列表
        /// </summary>
        private List<RVO2Agent> agents = new List<RVO2Agent>();

        /// <summary>
        /// 时间步长
        /// </summary>
        private zfloat timeStep = zfloat.Zero;

        /// <summary>
        /// 默认的时间范围参数
        /// </summary>
        public zfloat defaultTimeHorizon = new zfloat(2);

        /// <summary>
        /// 添加一个智能体到模拟器
        /// </summary>
        /// <param name="position">初始位置</param>
        /// <param name="radius">智能体半径</param>
        /// <param name="maxSpeed">最大速度</param>
        /// <param name="maxNeighbors">最大邻居数量（用于优化性能）</param>
        /// <param name="timeHorizon">避障时间范围</param>
        /// <returns>智能体ID</returns>
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
            agent.radius = radius;
            agent.maxSpeed = maxSpeed;
            agent.maxNeighbors = maxNeighbors;
            agent.timeHorizon = timeHorizon;
            agent.id = agents.Count;

            agents.Add(agent);
            return agent.id;
        }

        /// <summary>
        /// 设置智能体的期望速度
        /// </summary>
        public void SetAgentPrefVelocity(int agentId, zVector2 prefVelocity)
        {
            if (agentId >= 0 && agentId < agents.Count)
            {
                agents[agentId].prefVelocity = prefVelocity;
            }
        }

        /// <summary>
        /// 设置智能体的位置
        /// </summary>
        public void SetAgentPosition(int agentId, zVector2 position)
        {
            if (agentId >= 0 && agentId < agents.Count)
            {
                agents[agentId].position = position;
            }
        }

        /// <summary>
        /// 获取智能体的位置
        /// </summary>
        public zVector2 GetAgentPosition(int agentId)
        {
            if (agentId >= 0 && agentId < agents.Count)
            {
                return agents[agentId].position;
            }
            return zVector2.zero;
        }

        /// <summary>
        /// 获取智能体的速度
        /// </summary>
        public zVector2 GetAgentVelocity(int agentId)
        {
            if (agentId >= 0 && agentId < agents.Count)
            {
                return agents[agentId].velocity;
            }
            return zVector2.zero;
        }

        /// <summary>
        /// 执行一步模拟
        /// </summary>
        /// <param name="deltaTime">时间步长</param>
        public void DoStep(zfloat deltaTime)
        {
            timeStep = deltaTime;

            // 为每个智能体计算新速度
            for (int i = 0; i < agents.Count; i++)
            {
                ComputeNewVelocity(agents[i]);
            }

            // 更新所有智能体的位置
            for (int i = 0; i < agents.Count; i++)
            {
                agents[i].position += agents[i].velocity * deltaTime;
            }
        }

        /// <summary>
        /// 计算智能体的新速度（ORCA算法核心）
        /// </summary>
        private void ComputeNewVelocity(RVO2Agent agent)
        {
            // 构建ORCA线
            List<ORCALine> orcaLines = new List<ORCALine>();

            // 对每个其他智能体创建ORCA线
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
                    }
                }
                else
                {
                    // 发生碰撞，需要立即避开
                    zfloat invTimeStep = zfloat.One / timeStep;
                    zVector2 w = relativeVelocity - relativePosition * invTimeStep;
                    zfloat wLength = w.magnitude;
                    zVector2 unitW = w / wLength;

                    line.direction = new zVector2(unitW.y, -unitW.x);
                    u = unitW * (combinedRadius * invTimeStep - wLength);
                }

                line.point = agent.velocity + u * zfloat.Half;
                orcaLines.Add(line);
            }

            // 使用线性规划找到最优速度
            zVector2 newVelocity = LinearProgram2(orcaLines, agent.maxSpeed, agent.prefVelocity);
            agent.velocity = newVelocity;
        }

        /// <summary>
        /// 二维线性规划求解器
        /// 在ORCA线约束下，找到最接近期望速度的可行速度
        /// </summary>
        private zVector2 LinearProgram2(List<ORCALine> lines, zfloat maxSpeed, zVector2 optVelocity)
        {
            if (optVelocity.magnitude > maxSpeed)
            {
                optVelocity = optVelocity.normalized * maxSpeed;
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
                }
            }

            // 限制在最大速度内
            if (result.magnitude > maxSpeed)
            {
                result = result.normalized * maxSpeed;
            }

            return result;
        }

        /// <summary>
        /// 计算2D行列式 (叉积的z分量)
        /// </summary>
        private zfloat Det(zVector2 a, zVector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>
        /// 将速度投影到ORCA线上
        /// </summary>
        private zVector2 Project(ORCALine line, zVector2 velocity)
        {
            zVector2 delta = velocity - line.point;
            zfloat dotProduct = zVector2.Dot(delta, line.direction);
            return line.point + line.direction * dotProduct;
        }

        /// <summary>
        /// 获取智能体数量
        /// </summary>
        public int GetNumAgents()
        {
            return agents.Count;
        }

        /// <summary>
        /// 清除所有智能体
        /// </summary>
        public void Clear()
        {
            agents.Clear();
        }
    }

    /// <summary>
    /// RVO智能体
    /// </summary>
    public class RVO2Agent
    {
        /// <summary>智能体ID</summary>
        public int id;

        /// <summary>当前位置</summary>
        public zVector2 position;

        /// <summary>当前速度</summary>
        public zVector2 velocity;

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
    }

    /// <summary>
    /// ORCA线 (Optimal Reciprocal Collision Avoidance Line)
    /// 表示速度空间中的半平面约束
    /// </summary>
    public struct ORCALine
    {
        /// <summary>线上的一个点</summary>
        public zVector2 point;

        /// <summary>线的方向（单位向量）</summary>
        public zVector2 direction;
    }
}

