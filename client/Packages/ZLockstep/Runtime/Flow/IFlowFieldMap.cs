using zUnity;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场地图接口
    /// 提供地图数据给流场系统使用
    /// </summary>
    public interface IFlowFieldMap
    {
        /// <summary>
        /// 获取流场地图宽度（格子数）
        /// </summary>
        int GetWidth();

        /// <summary>
        /// 获取流场地图高度（格子数）
        /// </summary>
        int GetHeight();

        /// <summary>
        /// 获取每个格子的世界尺寸
        /// </summary>
        zfloat GetGridSize();

        /// <summary>
        /// 判断指定格子是否可行走
        /// </summary>
        bool IsWalkable(int gridX, int gridY);

        /// <summary>
        /// 获取地形代价（可选）
        /// 返回值：1.0 = 正常地形，2.0 = 慢速地形（如泥地），0 = 不可通行
        /// </summary>
        zfloat GetTerrainCost(int gridX, int gridY);

        /// <summary>
        /// 世界坐标转换为格子坐标
        /// </summary>
        void WorldToGrid(zVector2 worldPos, out int gridX, out int gridY);

        /// <summary>
        /// 格子坐标转换为世界坐标（格子中心）
        /// </summary>
        zVector2 GridToWorld(int gridX, int gridY);
    }
}

