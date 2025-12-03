namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 建筑组件
    /// 用于静态建筑物（基地、防御塔等）
    /// </summary>
    public struct BuildingComponent : IComponent
    {
        /// <summary>
        /// 建筑类型
        /// 0 = 基地
        /// 1 = 防御塔
        /// 2 = 资源建筑等
        /// </summary>
        public int BuildingType;

        /// <summary>
        /// 占据的地图起始位置（格子坐标）
        /// </summary>
        public int GridX;
        public int GridY;

        /// <summary>
        /// 占据的格子数量（宽度和高度）
        /// </summary>
        public int Width;
        public int Height;

        /// <summary>
        /// 创建建筑组件
        /// </summary>
        public static BuildingComponent Create(int buildingType, int gridX, int gridY, int width, int height)
        {
            return new BuildingComponent
            {
                BuildingType = buildingType,
                GridX = gridX,
                GridY = gridY,
                Width = width,
                Height = height
            };
        }
    }
}

