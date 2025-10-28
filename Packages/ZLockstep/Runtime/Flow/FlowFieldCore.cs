using System.Collections.Generic;
using zUnity;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场数据
    /// </summary>
    public class FlowField
    {
        public int fieldId;
        public int targetGridX;
        public int targetGridY;
        public int width;
        public int height;
        
        /// <summary>
        /// 每个格子的方向向量（已归一化）
        /// </summary>
        public zVector2[] directions;
        
        /// <summary>
        /// 每个格子到目标的代价
        /// </summary>
        public zfloat[] costs;
        
        /// <summary>
        /// 引用计数
        /// </summary>
        public int referenceCount;
        
        /// <summary>
        /// 是否需要更新（地图变化时标记为脏）
        /// </summary>
        public bool isDirty;
        
        /// <summary>
        /// 最后更新帧
        /// </summary>
        public int lastUpdateFrame;

        public FlowField(int id, int w, int h)
        {
            fieldId = id;
            width = w;
            height = h;
            directions = new zVector2[w * h];
            costs = new zfloat[w * h];
            referenceCount = 0;
            isDirty = false;
            lastUpdateFrame = 0;
        }

        public int GetIndex(int x, int y)
        {
            return y * width + x;
        }

        public bool IsValid(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
    }

    /// <summary>
    /// 流场计算器
    /// 使用Dijkstra算法计算代价场，然后生成方向场
    /// </summary>
    public class FlowFieldCalculator
    {
        private struct GridNode
        {
            public int x;
            public int y;
            public zfloat cost;

            public GridNode(int x, int y, zfloat cost)
            {
                this.x = x;
                this.y = y;
                this.cost = cost;
            }
        }

        private class PriorityQueue
        {
            private List<GridNode> nodes = new List<GridNode>();

            public int Count => nodes.Count;

            public void Enqueue(GridNode node)
            {
                nodes.Add(node);
                int i = nodes.Count - 1;
                
                // 向上调整
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (nodes[parent].cost <= nodes[i].cost)
                        break;
                    
                    GridNode temp = nodes[parent];
                    nodes[parent] = nodes[i];
                    nodes[i] = temp;
                    i = parent;
                }
            }

            public GridNode Dequeue()
            {
                GridNode result = nodes[0];
                int last = nodes.Count - 1;
                nodes[0] = nodes[last];
                nodes.RemoveAt(last);

                if (nodes.Count > 0)
                {
                    // 向下调整
                    int i = 0;
                    while (true)
                    {
                        int left = i * 2 + 1;
                        int right = i * 2 + 2;
                        int smallest = i;

                        if (left < nodes.Count && nodes[left].cost < nodes[smallest].cost)
                            smallest = left;
                        if (right < nodes.Count && nodes[right].cost < nodes[smallest].cost)
                            smallest = right;

                        if (smallest == i)
                            break;

                        GridNode temp = nodes[i];
                        nodes[i] = nodes[smallest];
                        nodes[smallest] = temp;
                        i = smallest;
                    }
                }

                return result;
            }
        }

        private static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] DY = { 1, 0, -1, 0, 1, -1, 1, -1 };
        private static readonly zfloat SQRT2 = new zfloat(1, 4142); // 对角线代价

        /// <summary>
        /// 计算流场
        /// </summary>
        public static void Calculate(FlowField field, IFlowFieldMap map, int targetX, int targetY)
        {
            field.targetGridX = targetX;
            field.targetGridY = targetY;

            // 1. 初始化代价为无穷大
            for (int i = 0; i < field.costs.Length; i++)
            {
                field.costs[i] = zfloat.Infinity;
            }

            // 2. 使用Dijkstra计算代价场
            CalculateCostField(field, map, targetX, targetY);

            // 3. 添加墙壁惩罚（让单位远离障碍物）
            AddWallPenalty(field, map);

            // 4. 根据代价场生成方向场（使用加权平均，更平滑）
            GenerateDirectionField(field, map);

            field.isDirty = false;
        }

        /// <summary>
        /// 使用Dijkstra算法计算代价场
        /// </summary>
        private static void CalculateCostField(FlowField field, IFlowFieldMap map, int targetX, int targetY)
        {
            if (!field.IsValid(targetX, targetY))
                return;

            PriorityQueue openSet = new PriorityQueue();
            bool[] closedSet = new bool[field.width * field.height];

            // 目标点代价为0
            int targetIndex = field.GetIndex(targetX, targetY);
            field.costs[targetIndex] = zfloat.Zero;
            openSet.Enqueue(new GridNode(targetX, targetY, zfloat.Zero));

            // Dijkstra算法
            while (openSet.Count > 0)
            {
                GridNode current = openSet.Dequeue();
                int currentIndex = field.GetIndex(current.x, current.y);

                if (closedSet[currentIndex])
                    continue;

                closedSet[currentIndex] = true;

                // 检查8个邻居
                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x + DX[i];
                    int ny = current.y + DY[i];

                    if (!field.IsValid(nx, ny))
                        continue;

                    if (!map.IsWalkable(nx, ny))
                        continue;

                    int neighborIndex = field.GetIndex(nx, ny);
                    if (closedSet[neighborIndex])
                        continue;

                    // 计算新代价
                    zfloat moveCost = (i < 4) ? zfloat.One : SQRT2; // 直线或对角线
                    zfloat terrainCost = map.GetTerrainCost(nx, ny);
                    zfloat newCost = current.cost + moveCost * terrainCost;

                    // 更新代价
                    if (newCost < field.costs[neighborIndex])
                    {
                        field.costs[neighborIndex] = newCost;
                        openSet.Enqueue(new GridNode(nx, ny, newCost));
                    }
                }
            }
        }

        /// <summary>
        /// 添加墙壁惩罚到代价场
        /// 靠近障碍物的格子增加额外代价，让单位自然远离墙壁
        /// </summary>
        private static void AddWallPenalty(FlowField field, IFlowFieldMap map)
        {
            zfloat wallPenalty = new zfloat(0, 5000); // 每个邻近障碍物增加0.5代价

            for (int y = 0; y < field.height; y++)
            {
                for (int x = 0; x < field.width; x++)
                {
                    if (!map.IsWalkable(x, y))
                        continue;

                    int index = field.GetIndex(x, y);
                    
                    // 如果已经是无限代价（不可达），跳过
                    if (field.costs[index] == zfloat.Infinity)
                        continue;

                    // 检查8个邻居，计算障碍物数量
                    int obstacleCount = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (!field.IsValid(nx, ny) || !map.IsWalkable(nx, ny))
                            {
                                obstacleCount++;
                            }
                        }
                    }

                    // 添加惩罚：障碍物越多，惩罚越高
                    if (obstacleCount > 0)
                    {
                        field.costs[index] += wallPenalty * (zfloat)obstacleCount;
                    }
                }
            }
        }

        /// <summary>
        /// 根据代价场生成方向场
        /// </summary>
        private static void GenerateDirectionField(FlowField field, IFlowFieldMap map)
        {
            for (int y = 0; y < field.height; y++)
            {
                for (int x = 0; x < field.width; x++)
                {
                    int index = field.GetIndex(x, y);

                    // 如果不可达，方向为零
                    if (field.costs[index] == zfloat.Infinity)
                    {
                        field.directions[index] = zVector2.zero;
                        continue;
                    }

                    // 如果已经是目标，方向为零
                    if (x == field.targetGridX && y == field.targetGridY)
                    {
                        field.directions[index] = zVector2.zero;
                        continue;
                    }

                    // ===== 改进：加权平均方向，而不是单纯选最小 =====
                    zfloat currentCost = field.costs[index];
                    zVector2 avgDirection = zVector2.zero;
                    zfloat totalWeight = zfloat.Zero;

                    for (int i = 0; i < 8; i++)
                    {
                        int nx = x + DX[i];
                        int ny = y + DY[i];

                        if (!field.IsValid(nx, ny))
                            continue;

                        int neighborIndex = field.GetIndex(nx, ny);
                        zfloat neighborCost = field.costs[neighborIndex];
                        
                        // 只考虑代价更低的邻居
                        if (neighborCost < currentCost)
                        {
                            // 代价差越大，权重越高
                            zfloat costDiff = currentCost - neighborCost;
                            zfloat weight = costDiff * costDiff; // 平方强调差异
                            
                            zVector2 dirToNeighbor = new zVector2((zfloat)DX[i], (zfloat)DY[i]);
                            avgDirection += dirToNeighbor * weight;
                            totalWeight += weight;
                        }
                    }

                    // 设置方向（归一化）
                    if (totalWeight > zfloat.Epsilon)
                    {
                        field.directions[index] = (avgDirection / totalWeight).normalized;
                    }
                    else
                    {
                        // 没有更低代价的邻居（局部最小值或目标点）
                        field.directions[index] = zVector2.zero;
                    }
                }
            }
        }
    }
}

