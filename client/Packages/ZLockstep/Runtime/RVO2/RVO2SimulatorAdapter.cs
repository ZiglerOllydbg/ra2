using System.Collections.Generic;
using ZLockstep.Flow;
using zUnity;

namespace ZLockstep.RVO
{
    /// <summary>
    /// RVO2Simulator适配器，将旧的RVO2Simulator接口适配到NewRVO2的RVO.Simulator
    /// 保持与旧接口的兼容性，同时使用新的NewRVO2实现
    /// </summary>
    public class RVO2SimulatorAdapter : RVO2Simulator
    {
        private global::RVO.Simulator simulator;
        // 使用SortedDictionary保证遍历顺序的确定性（锁步同步系统要求）
        private SortedDictionary<int, int> agentIdMap; // 旧ID -> 新ID映射
        private Dictionary<int, int> reverseAgentIdMap; // 新ID -> 旧ID映射（仅用于查找，不需要有序）
        private int nextAgentId = 0;
        private Dictionary<int, AgentState> agentStates; // 存储额外的状态信息（静止检测等）

        private struct AgentState
        {
            public zVector2 lastPosition;
            public int stationaryFrames;
            public bool isStationary;
        }

        /// <summary>
        /// 默认的时间范围参数
        /// </summary>
        public new zfloat defaultTimeHorizon = new zfloat(2);

        /// <summary>
        /// 静止检测阈值
        /// </summary>
        private zfloat stationaryThreshold = new zfloat(0, 100); // 0.01单位

        /// <summary>
        /// 静止判定帧数
        /// </summary>
        private int stationaryFrameThreshold = 30;

        /// <summary>
        /// 流场管理器引用
        /// </summary>
        private FlowFieldManager flowFieldManager;

        public RVO2SimulatorAdapter()
        {
            simulator = new global::RVO.Simulator();
            agentIdMap = new SortedDictionary<int, int>(); // 使用SortedDictionary保证确定性
            reverseAgentIdMap = new Dictionary<int, int>();
            agentStates = new Dictionary<int, AgentState>();

            // 初始化NewRVO2模拟器
            simulator.Clear();
            
            // 设置默认时间步长（会在DoStep时更新）
            simulator.setTimeStep(new zfloat(0, 1000)); // 0.1
        }

        /// <summary>
        /// 设置流场管理器引用
        /// </summary>
        public new void SetFlowFieldManager(FlowFieldManager manager)
        {
            flowFieldManager = manager;
        }

        /// <summary>
        /// 添加一个智能体到模拟器中
        /// </summary>
        public new int AddAgent(zVector2 position, zfloat radius, zfloat maxSpeed,
            int maxNeighbors = 10, zfloat timeHorizon = default(zfloat))
        {
            if (timeHorizon == zfloat.Zero)
            {
                timeHorizon = defaultTimeHorizon;
            }

            // 使用NewRVO2的addAgent方法
            // neighborDist默认使用maxSpeed * timeHorizon * 2（经验值）
            zfloat neighborDist = maxSpeed * timeHorizon * new zfloat(2);
            // timeHorizonObst使用与timeHorizon相同的值
            zfloat timeHorizonObst = timeHorizon;
            global::RVO.Vector2 rvoPosition = new global::RVO.Vector2(position);
            global::RVO.Vector2 rvoVelocity = new global::RVO.Vector2(zVector2.zero);

            int newAgentId = simulator.addAgent(rvoPosition, neighborDist, maxNeighbors, 
                timeHorizon, timeHorizonObst, radius, maxSpeed, rvoVelocity);

            // 分配旧的ID并建立映射
            int oldAgentId = nextAgentId++;
            agentIdMap[oldAgentId] = newAgentId;
            reverseAgentIdMap[newAgentId] = oldAgentId;

            // 初始化状态
            agentStates[oldAgentId] = new AgentState
            {
                lastPosition = position,
                stationaryFrames = 0,
                isStationary = false
            };

            return oldAgentId;
        }

        /// <summary>
        /// 设置智能体的期望速度向量
        /// </summary>
        public new void SetAgentPrefVelocity(int agentId, zVector2 prefVelocity)
        {
            if (agentIdMap.TryGetValue(agentId, out int newAgentId))
            {
                global::RVO.Vector2 rvoPrefVelocity = new global::RVO.Vector2(prefVelocity);
                simulator.setAgentPrefVelocity(newAgentId, rvoPrefVelocity);
            }
        }

        /// <summary>
        /// 设置智能体的位置坐标
        /// </summary>
        public new void SetAgentPosition(int agentId, zVector2 position)
        {
            if (agentIdMap.TryGetValue(agentId, out int newAgentId))
            {
                global::RVO.Vector2 rvoPosition = new global::RVO.Vector2(position);
                simulator.setAgentPosition(newAgentId, rvoPosition);
            }
        }

        /// <summary>
        /// 获取指定智能体的当前位置坐标
        /// </summary>
        public new zVector2 GetAgentPosition(int agentId)
        {
            if (agentIdMap.TryGetValue(agentId, out int newAgentId))
            {
                global::RVO.Vector2 rvoPosition = simulator.getAgentPosition(newAgentId);
                return rvoPosition.ToZVector2();
            }
            return zVector2.zero;
        }

        /// <summary>
        /// 获取指定智能体的当前实际移动速度
        /// </summary>
        public new zVector2 GetAgentVelocity(int agentId)
        {
            if (agentIdMap.TryGetValue(agentId, out int newAgentId))
            {
                global::RVO.Vector2 rvoVelocity = simulator.getAgentVelocity(newAgentId);
                return rvoVelocity.ToZVector2();
            }
            return zVector2.zero;
        }

        /// <summary>
        /// 执行一步模拟计算
        /// </summary>
        public new void DoStep(zfloat deltaTime)
        {
            // 防止除以零错误
            if (deltaTime <= zfloat.Zero)
            {
                return;
            }

            // 设置时间步长
            simulator.setTimeStep(deltaTime);

            // 执行NewRVO2的模拟步
            simulator.doStep();

            // 更新静止状态
            foreach (var kvp in agentIdMap)
            {
                int oldAgentId = kvp.Key;
                int newAgentId = kvp.Value;

                // 更新静止状态
                bool wasStationary = agentStates[oldAgentId].isStationary;
                UpdateStationaryState(oldAgentId, newAgentId);
                bool isStationary = agentStates[oldAgentId].isStationary;

                // 如果智能体开始移动，立即移除其动态障碍物
                if (wasStationary && !isStationary && flowFieldManager != null)
                {
                    var map = flowFieldManager.GetMap();
                    if (map is SimpleMapManager simpleMap)
                    {
                        simpleMap.RemoveDynamicObstacle(oldAgentId);
                    }
                    flowFieldManager.MarkDynamicObstaclesNeedUpdate();
                }
                // 如果智能体变为静止，通知流场管理器需要更新动态障碍物
                else if (!wasStationary && isStationary && flowFieldManager != null)
                {
                    flowFieldManager.MarkDynamicObstaclesNeedUpdate();
                }
            }
        }

        /// <summary>
        /// 更新智能体的静止状态
        /// </summary>
        private void UpdateStationaryState(int oldAgentId, int newAgentId)
        {
            zVector2 currentPos = GetAgentPosition(oldAgentId);
            AgentState state = agentStates[oldAgentId];
            zVector2 prefVelocity = simulator.getAgentPrefVelocity(newAgentId).ToZVector2();

            zfloat moveDistance = (currentPos - state.lastPosition).magnitude;

            if (moveDistance < stationaryThreshold && prefVelocity == zVector2.zero)
            {
                state.stationaryFrames++;
                if (state.stationaryFrames >= stationaryFrameThreshold)
                {
                    state.isStationary = true;
                }
            }
            else
            {
                state.stationaryFrames = 0;
                state.isStationary = false;
            }

            state.lastPosition = currentPos;
            agentStates[oldAgentId] = state;
        }

        /// <summary>
        /// 移除指定ID的智能体
        /// 注意：NewRVO2的Simulator没有RemoveAgent方法
        /// 这里通过重建模拟器状态来实现移除功能
        /// </summary>
        public new void RemoveAgent(int agentId)
        {
            if (!agentIdMap.TryGetValue(agentId, out int newAgentId))
            {
                return;
            }

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

            // 重建模拟器：保存所有其他agent的状态，清除并重新添加
            List<AgentData> remainingAgents = new List<AgentData>();
            foreach (var kvp in agentIdMap)
            {
                if (kvp.Key == agentId) continue; // 跳过要删除的

                int oldId = kvp.Key;
                int currentNewId = kvp.Value;
                remainingAgents.Add(new AgentData
                {
                    OldId = oldId,
                    Position = GetAgentPosition(oldId),
                    Velocity = GetAgentVelocity(oldId),
                    PrefVelocity = simulator.getAgentPrefVelocity(currentNewId).ToZVector2(),
                    Radius = simulator.getAgentRadius(currentNewId),
                    MaxSpeed = simulator.getAgentMaxSpeed(currentNewId),
                    MaxNeighbors = simulator.getAgentMaxNeighbors(currentNewId),
                    TimeHorizon = simulator.getAgentTimeHorizon(currentNewId),
                    TimeHorizonObst = simulator.getAgentTimeHorizonObst(currentNewId),
                    NeighborDist = simulator.getAgentNeighborDist(currentNewId),
                    State = agentStates[oldId]
                });
            }

            // 清除并重建
            simulator.Clear();
            agentIdMap.Clear();
            reverseAgentIdMap.Clear();
            agentStates.Clear();
            nextAgentId = 0;

            // 重新添加所有剩余的agent
            foreach (var agentData in remainingAgents)
            {
                global::RVO.Vector2 rvoPosition = new global::RVO.Vector2(agentData.Position);
                global::RVO.Vector2 rvoVelocity = new global::RVO.Vector2(agentData.Velocity);
                global::RVO.Vector2 rvoPrefVelocity = new global::RVO.Vector2(agentData.PrefVelocity);

                int newId = simulator.addAgent(rvoPosition, agentData.NeighborDist, agentData.MaxNeighbors,
                    agentData.TimeHorizon, agentData.TimeHorizonObst, agentData.Radius, 
                    agentData.MaxSpeed, rvoVelocity);
                
                simulator.setAgentPrefVelocity(newId, rvoPrefVelocity);

                agentIdMap[agentData.OldId] = newId;
                reverseAgentIdMap[newId] = agentData.OldId;
                agentStates[agentData.OldId] = agentData.State;

                if (agentData.OldId >= nextAgentId)
                {
                    nextAgentId = agentData.OldId + 1;
                }
            }
        }

        private struct AgentData
        {
            public int OldId;
            public zVector2 Position;
            public zVector2 Velocity;
            public zVector2 PrefVelocity;
            public zfloat Radius;
            public zfloat MaxSpeed;
            public int MaxNeighbors;
            public zfloat TimeHorizon;
            public zfloat TimeHorizonObst;
            public zfloat NeighborDist;
            public AgentState State;
        }

        /// <summary>
        /// 重置指定智能体的静止状态
        /// </summary>
        public new void ResetAgentStationaryState(int agentId)
        {
            if (agentStates.TryGetValue(agentId, out AgentState state))
            {
                state.stationaryFrames = 0;
                state.isStationary = false;
                agentStates[agentId] = state;
            }
        }

        /// <summary>
        /// 获取当前系统中的智能体总数量
        /// </summary>
        public new int GetNumAgents()
        {
            return agentIdMap.Count;
        }

        /// <summary>
        /// 清除模拟器中的所有智能体
        /// </summary>
        public new void Clear()
        {
            simulator.Clear();
            agentIdMap.Clear();
            reverseAgentIdMap.Clear();
            agentStates.Clear();
            nextAgentId = 0;
        }

        /// <summary>
        /// 获取所有智能体的只读列表（适配旧接口）
        /// </summary>
        public new IReadOnlyList<RVO2Agent> GetAgents()
        {
            List<RVO2Agent> agents = new List<RVO2Agent>();
            foreach (var kvp in agentIdMap)
            {
                int oldAgentId = kvp.Key;
                int newAgentId = kvp.Value;

                RVO2Agent agent = new RVO2Agent
                {
                    id = oldAgentId,
                    position = GetAgentPosition(oldAgentId),
                    velocity = GetAgentVelocity(oldAgentId),
                    prefVelocity = simulator.getAgentPrefVelocity(newAgentId).ToZVector2(),
                    radius = simulator.getAgentRadius(newAgentId),
                    maxSpeed = simulator.getAgentMaxSpeed(newAgentId),
                    maxNeighbors = simulator.getAgentMaxNeighbors(newAgentId),
                    timeHorizon = simulator.getAgentTimeHorizon(newAgentId),
                    isStationary = agentStates[oldAgentId].isStationary,
                    stationaryFrames = agentStates[oldAgentId].stationaryFrames,
                    lastPosition = agentStates[oldAgentId].lastPosition
                };
                agents.Add(agent);
            }
            return agents.AsReadOnly();
        }

        /// <summary>
        /// 获取静止的智能体列表
        /// </summary>
        public new List<RVO2Agent> GetStationaryAgents()
        {
            List<RVO2Agent> stationaryAgents = new List<RVO2Agent>();
            foreach (var agent in GetAgents())
            {
                if (agent.isStationary)
                {
                    stationaryAgents.Add(agent);
                }
            }
            return stationaryAgents;
        }
    }
}
