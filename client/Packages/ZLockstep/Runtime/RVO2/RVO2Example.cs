using System.Collections.Generic;
using zUnity;

namespace ZLockstep.RVO
{
    /// <summary>
    /// RVO2使用示例
    /// </summary>
    public class RVO2Example
    {
        private RVO2Simulator simulator;
        private List<AgentData> agentsData;

        public class AgentData
        {
            public int id;
            public zVector2 targetPosition;
            public zfloat arrivalRadius; // 到达目标的距离阈值
        }

        /// <summary>
        /// 初始化示例场景：圆形布局的智能体互相穿过
        /// </summary>
        public void InitializeCircleExample()
        {
            simulator = new RVO2Simulator();
            agentsData = new List<AgentData>();

            // 参数设置
            int numAgents = 8;
            zfloat circleRadius = new zfloat(10);
            zfloat agentRadius = new zfloat(0, 5000); // 0.5
            zfloat maxSpeed = new zfloat(2);
            zfloat timeHorizon = new zfloat(2);

            // 创建圆形布局的智能体，目标是对面的位置
            for (int i = 0; i < numAgents; i++)
            {
                zfloat angle = zMathf.PI * zfloat.Two * i / numAgents;
                
                // 起始位置：圆周上
                zVector2 startPos = new zVector2(
                    zMathf.Cos(angle) * circleRadius,
                    zMathf.Sin(angle) * circleRadius
                );

                // 目标位置：对面
                zfloat targetAngle = angle + zMathf.PI;
                zVector2 targetPos = new zVector2(
                    zMathf.Cos(targetAngle) * circleRadius,
                    zMathf.Sin(targetAngle) * circleRadius
                );

                // 添加到模拟器
                int agentId = simulator.AddAgent(
                    startPos, 
                    agentRadius, 
                    maxSpeed,
                    10, // maxNeighbors
                    timeHorizon
                );

                // 保存数据
                AgentData data = new AgentData();
                data.id = agentId;
                data.targetPosition = targetPos;
                data.arrivalRadius = new zfloat(0, 2000); // 0.2
                agentsData.Add(data);
            }
        }

        /// <summary>
        /// 初始化走廊场景：两队智能体相向而行
        /// </summary>
        public void InitializeCorridorExample()
        {
            simulator = new RVO2Simulator();
            agentsData = new List<AgentData>();

            int agentsPerSide = 5;
            zfloat agentRadius = new zfloat(0, 4000); // 0.4
            zfloat maxSpeed = new zfloat(1, 5000); // 1.5
            zfloat spacing = new zfloat(2);

            // 左侧队伍（向右移动）
            for (int i = 0; i < agentsPerSide; i++)
            {
                zVector2 startPos = new zVector2(
                    (zfloat)0,
                    (zfloat)i * spacing - (zfloat)(agentsPerSide / 2) * spacing
                );
                zVector2 targetPos = new zVector2((zfloat)20, startPos.y);

                int agentId = simulator.AddAgent(startPos, agentRadius, maxSpeed);
                
                AgentData data = new AgentData();
                data.id = agentId;
                data.targetPosition = targetPos;
                data.arrivalRadius = new zfloat(0, 5000); // 0.5
                agentsData.Add(data);
            }

            // 右侧队伍（向左移动）
            for (int i = 0; i < agentsPerSide; i++)
            {
                zVector2 startPos = new zVector2(
                    (zfloat)20,
                    (zfloat)i * spacing - (zfloat)(agentsPerSide / 2) * spacing
                );
                zVector2 targetPos = new zVector2((zfloat)0, startPos.y);

                int agentId = simulator.AddAgent(startPos, agentRadius, maxSpeed);
                
                AgentData data = new AgentData();
                data.id = agentId;
                data.targetPosition = targetPos;
                data.arrivalRadius = new zfloat(0, 5000); // 0.5
                agentsData.Add(data);
            }
        }

        /// <summary>
        /// 更新模拟（每帧调用）
        /// </summary>
        /// <param name="deltaTime">时间步长</param>
        public void Update(zfloat deltaTime)
        {
            if (simulator == null || agentsData == null)
                return;

            // 为每个智能体计算并设置期望速度
            for (int i = 0; i < agentsData.Count; i++)
            {
                AgentData data = agentsData[i];
                zVector2 currentPos = simulator.GetAgentPosition(data.id);
                zVector2 toTarget = data.targetPosition - currentPos;
                zfloat distance = toTarget.magnitude;

                if (distance > data.arrivalRadius)
                {
                    // 还未到达目标，计算期望速度
                    zVector2 direction = toTarget / distance;
                    
                    // 如果接近目标，减速
                    zfloat slowDownRadius = new zfloat(2);
                    zfloat speed;
                    if (distance < slowDownRadius)
                    {
                        // 线性减速
                        speed = distance / slowDownRadius * new zfloat(2);
                    }
                    else
                    {
                        speed = new zfloat(2); // 最大速度
                    }

                    simulator.SetAgentPrefVelocity(data.id, direction * speed);
                }
                else
                {
                    // 已到达目标，停止
                    simulator.SetAgentPrefVelocity(data.id, zVector2.zero);
                }
            }

            // 执行一步模拟
            simulator.DoStep(deltaTime);
        }

        /// <summary>
        /// 获取所有智能体的位置（用于渲染）
        /// </summary>
        public List<zVector2> GetAllAgentPositions()
        {
            List<zVector2> positions = new List<zVector2>();
            if (simulator != null && agentsData != null)
            {
                for (int i = 0; i < agentsData.Count; i++)
                {
                    positions.Add(simulator.GetAgentPosition(agentsData[i].id));
                }
            }
            return positions;
        }

        /// <summary>
        /// 获取所有智能体的速度（用于调试渲染）
        /// </summary>
        public List<zVector2> GetAllAgentVelocities()
        {
            List<zVector2> velocities = new List<zVector2>();
            if (simulator != null && agentsData != null)
            {
                for (int i = 0; i < agentsData.Count; i++)
                {
                    velocities.Add(simulator.GetAgentVelocity(agentsData[i].id));
                }
            }
            return velocities;
        }

        /// <summary>
        /// 检查是否所有智能体都到达目标
        /// </summary>
        public bool AllAgentsReachedGoal()
        {
            if (simulator == null || agentsData == null)
                return false;

            for (int i = 0; i < agentsData.Count; i++)
            {
                AgentData data = agentsData[i];
                zVector2 currentPos = simulator.GetAgentPosition(data.id);
                zfloat distance = (data.targetPosition - currentPos).magnitude;

                if (distance > data.arrivalRadius)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 动态添加新的智能体
        /// </summary>
        public int AddAgent(zVector2 startPos, zVector2 targetPos)
        {
            if (simulator == null)
            {
                simulator = new RVO2Simulator();
                agentsData = new List<AgentData>();
            }

            zfloat radius = new zfloat(0, 5000); // 0.5
            zfloat maxSpeed = new zfloat(2);

            int agentId = simulator.AddAgent(startPos, radius, maxSpeed);

            AgentData data = new AgentData();
            data.id = agentId;
            data.targetPosition = targetPos;
            data.arrivalRadius = new zfloat(0, 2000); // 0.2
            agentsData.Add(data);

            return agentId;
        }

        /// <summary>
        /// 为指定智能体设置新目标
        /// </summary>
        public void SetAgentTarget(int agentId, zVector2 newTarget)
        {
            for (int i = 0; i < agentsData.Count; i++)
            {
                if (agentsData[i].id == agentId)
                {
                    agentsData[i].targetPosition = newTarget;
                    break;
                }
            }
        }

        /// <summary>
        /// 清除所有智能体
        /// </summary>
        public void Clear()
        {
            if (simulator != null)
            {
                simulator.Clear();
            }
            if (agentsData != null)
            {
                agentsData.Clear();
            }
        }
    }
}

