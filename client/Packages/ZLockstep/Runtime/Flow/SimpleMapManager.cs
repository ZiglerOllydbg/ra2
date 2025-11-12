using zUnity;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 简单的地图管理器实现
    /// 用于演示如何实现IFlowFieldMap接口
    /// </summary>
    public class SimpleMapManager : IFlowFieldMap
    {
        private int logicWidth;
        private int logicHeight;
        private zfloat logicGridSize;
        
        private int flowWidth;
        private int flowHeight;
        private zfloat flowGridSize;
        
        private bool[] walkableGrid;

        /// <summary>
        /// 初始化地图
        /// </summary>
        /// <param name="width">逻辑地图宽度（精细格子）</param>
        /// <param name="height">逻辑地图高度（精细格子）</param>
        /// <param name="gridSize">逻辑格子大小（如0.5）</param>
        public void Initialize(int width, int height, zfloat gridSize)
        {
            logicWidth = width;
            logicHeight = height;
            logicGridSize = gridSize;
            
            // 流场地图尺寸减半（使用更大的格子）
            // flowWidth = width / 2;
            // flowHeight = height / 2;
            // flowGridSize = gridSize * zfloat.Two;
            flowWidth = width;
            flowHeight = height;
            flowGridSize = gridSize;
            
            // 初始化格子数据
            walkableGrid = new bool[width * height];
            for (int i = 0; i < walkableGrid.Length; i++)
            {
                walkableGrid[i] = true;
            }
        }

        /// <summary>
        /// 设置逻辑格子的可行走性
        /// </summary>
        public void SetWalkable(int x, int y, bool walkable)
        {
            if (x >= 0 && x < logicWidth && y >= 0 && y < logicHeight)
            {
                walkableGrid[y * logicWidth + x] = walkable;
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
        /// 获取逻辑格子是否可行走
        /// </summary>
        public bool IsLogicWalkable(int x, int y)
        {
            if (x >= 0 && x < logicWidth && y >= 0 && y < logicHeight)
            {
                return walkableGrid[y * logicWidth + x];
            }
            return false;
        }

        // ============ IFlowFieldMap 接口实现 ============

        public int GetWidth()
        {
            return flowWidth;
        }

        public int GetHeight()
        {
            return flowHeight;
        }

        public zfloat GetGridSize()
        {
            return flowGridSize;
        }

        public bool IsWalkable(int gridX, int gridY)
        {
            // 流场格子对应2x2的逻辑格子
            // 只要有一个逻辑格子可走，流场格子就可走
            int logicX = gridX;
            int logicY = gridY;
            
            for (int dy = 0; dy < 1; dy++)
            {
                for (int dx = 0; dx < 1; dx++)
                {
                    int lx = logicX + dx;
                    int ly = logicY + dy;
                    if (lx < logicWidth && ly < logicHeight)
                    {
                        if (walkableGrid[ly * logicWidth + lx])
                        {
                            return true;
                        }
                    }
                }
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
            gridX = (int)(worldPos.x / flowGridSize);
            gridY = (int)(worldPos.y / flowGridSize);
            
            // 限制在地图范围内
            if (gridX < 0) gridX = 0;
            if (gridY < 0) gridY = 0;
            if (gridX >= flowWidth) gridX = flowWidth - 1;
            if (gridY >= flowHeight) gridY = flowHeight - 1;
        }

        public zVector2 GridToWorld(int gridX, int gridY)
        {
            // 返回格子中心的世界坐标
            return new zVector2(
                (zfloat)gridX * flowGridSize + flowGridSize * zfloat.Half,
                (zfloat)gridY * flowGridSize + flowGridSize * zfloat.Half
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

