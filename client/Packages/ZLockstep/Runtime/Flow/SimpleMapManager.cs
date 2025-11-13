using zUnity;

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
        }
    }
}