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

            // 3. 根据代价场生成方向场
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

                    // 找到代价最小的邻居
                    zfloat minCost = field.costs[index];
                    int bestDx = 0;
                    int bestDy = 0;

                    for (int i = 0; i < 8; i++)
                    {
                        int nx = x + DX[i];
                        int ny = y + DY[i];

                        if (!field.IsValid(nx, ny))
                            continue;

                        int neighborIndex = field.GetIndex(nx, ny);
                        if (field.costs[neighborIndex] < minCost)
                        {
                            minCost = field.costs[neighborIndex];
                            bestDx = DX[i];
                            bestDy = DY[i];
                        }
                    }

                    // 设置方向（归一化）
                    if (bestDx != 0 || bestDy != 0)
                    {
                        zVector2 dir = new zVector2((zfloat)bestDx, (zfloat)bestDy);
                        field.directions[index] = dir.normalized;
                    }
                    else
                    {
                        field.directions[index] = zVector2.zero;
                    }
                }
            }
        }
    }
}

