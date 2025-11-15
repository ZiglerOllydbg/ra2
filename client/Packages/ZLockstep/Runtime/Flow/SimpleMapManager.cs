using zUnity;
using System.Collections.Generic;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 简单的地图管理器实现
    /// 用于演示如何实现IFlowFieldMap接口
    /// </summary>
    public class SimpleMapManager : IFlowFieldMap
    {
        private int width;
        private int height;
        private zfloat gridSize;
        
        private bool[] walkableGrid;
        private Dictionary<int, DynamicObstacleInfo> dynamicObstacles; // agentId -> obstacle info
        private HashSet<long> occupiedCells; // cells occupied by dynamic obstacles

        /// <summary>
        /// 动态障碍物信息
        /// </summary>
        private class DynamicObstacleInfo
        {
            public zVector2 position;
            public zfloat radius;
            public HashSet<long> occupiedCells;
            
            public DynamicObstacleInfo(zVector2 pos, zfloat r)
            {
                position = pos;
                radius = r;
                occupiedCells = new HashSet<long>();
            }
        }

        /// <summary>
        /// 初始化地图
        /// </summary>
        /// <param name="width">地图宽度（格子数）</param>
        /// <param name="height">地图高度（格子数）</param>
        /// <param name="gridSize">格子大小</param>
        public void Initialize(int width, int height, zfloat gridSize)
        {
            this.width = width;
            this.height = height;
            this.gridSize = gridSize;
            
            // 初始化格子数据
            walkableGrid = new bool[width * height];
            for (int i = 0; i < walkableGrid.Length; i++)
            {
                walkableGrid[i] = true;
            }
            
            // 初始化动态障碍物集合
            dynamicObstacles = new Dictionary<int, DynamicObstacleInfo>();
            occupiedCells = new HashSet<long>();
        }

        /// <summary>
        /// 设置格子的可行走性
        /// </summary>
        public void SetWalkable(int x, int y, bool walkable)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                walkableGrid[y * width + x] = walkable;
            }
        }

        /// <summary>
        /// 设置矩形区域的可行走性（用于建筑）
        /// </summary>
        public void SetWalkableRect(int minX, int minY, int maxX, int maxY, bool walkable)
        {
            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    SetWalkable(x, y, walkable);
                }
            }
        }

        /// <summary>
        /// 设置圆形区域的可行走性（用于圆形障碍物）
        /// </summary>
        public void SetWalkableCircle(int centerX, int centerY, int radius, bool walkable)
        {
            int radiusSq = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        SetWalkable(x, y, walkable);
                    }
                }
            }
        }

        /// <summary>
        /// 添加动态障碍物
        /// </summary>
        public void AddDynamicObstacle(int agentId, zVector2 position, zfloat radius)
        {
            // 移除已存在的同ID障碍物
            RemoveDynamicObstacle(agentId);
            
            // 创建新的动态障碍物信息
            var obstacleInfo = new DynamicObstacleInfo(position, radius);
            
            // 计算影响的格子范围
            WorldToGrid(position, out int centerX, out int centerY);
            int radiusInCells = (int)(radius / gridSize) + 1;
            
            for (int y = centerY - radiusInCells; y <= centerY + radiusInCells; y++)
            {
                for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
                {
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        zVector2 cellCenter = GridToWorld(x, y);
                        zfloat distance = (cellCenter - position).magnitude;
                        
                        // 如果格子中心到智能体中心的距离小于半径，则认为该格子被占据
                        if (distance <= radius)
                        {
                            long key = ((long)x) | (((long)y) << 32);
                            obstacleInfo.occupiedCells.Add(key);
                            occupiedCells.Add(key);
                        }
                    }
                }
            }
            
            // 添加到动态障碍物字典
            dynamicObstacles[agentId] = obstacleInfo;
        }

        /// <summary>
        /// 移除动态障碍物
        /// </summary>
        public void RemoveDynamicObstacle(int agentId)
        {
            if (dynamicObstacles.TryGetValue(agentId, out DynamicObstacleInfo obstacleInfo))
            {
                // 从占用格子集合中移除
                foreach (long cellKey in obstacleInfo.occupiedCells)
                {
                    occupiedCells.Remove(cellKey);
                }
                
                // 从动态障碍物字典中移除
                dynamicObstacles.Remove(agentId);
            }
        }

        /// <summary>
        /// 清除所有动态障碍物
        /// </summary>
        public void ClearDynamicObstacles()
        {
            dynamicObstacles.Clear();
            occupiedCells.Clear();
        }

        /// <summary>
        /// 获取格子是否可行走
        /// </summary>
        public bool IsLogicWalkable(int x, int y)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                return walkableGrid[y * width + x];
            }
            return false;
        }

        // ============ IFlowFieldMap 接口实现 ============

        public int GetWidth()
        {
            return width;
        }

        public int GetHeight()
        {
            return height;
        }

        public zfloat GetGridSize()
        {
            return gridSize;
        }

        public bool IsWalkable(int gridX, int gridY)
        {
            if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height)
            {
                // 检查是否为动态障碍物
                long key = ((long)gridX) | (((long)gridY) << 32);
                if (occupiedCells.Contains(key))
                {
                    return false;
                }
                
                return walkableGrid[gridY * width + gridX];
            }
            return false;
        }

        public zfloat GetTerrainCost(int gridX, int gridY)
        {
            // 简单实现：所有地形代价相同
            // 可以扩展为支持不同地形类型
            return zfloat.One;
        }

        public void WorldToGrid(zVector2 worldPos, out int gridX, out int gridY)
        {
            gridX = (int)(worldPos.x / gridSize);
            gridY = (int)(worldPos.y / gridSize);
            
            // 限制在地图范围内
            if (gridX < 0) gridX = 0;
            if (gridY < 0) gridY = 0;
            if (gridX >= width) gridX = width - 1;
            if (gridY >= height) gridY = height - 1;
        }

        public zVector2 GridToWorld(int gridX, int gridY)
        {
            // 返回格子中心的世界坐标
            return new zVector2(
                (zfloat)gridX * gridSize + gridSize * zfloat.Half,
                (zfloat)gridY * gridSize + gridSize * zfloat.Half
            );
        }

        /// <summary>
        /// 清空地图（重置为全部可行走）
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < walkableGrid.Length; i++)
            {
                walkableGrid[i] = true;
            }
            dynamicObstacles.Clear();
            occupiedCells.Clear();
        }
    }
}