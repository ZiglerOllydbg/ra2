using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Flow;
using zUnity;

namespace ZLockstep.Simulation.ECS.Utils
{
    /// <summary>
    /// 建筑放置工具类
    /// 提供与建筑放置相关的计算和验证方法
    /// </summary>
    public static class BuildingPlacementUtils
    {
        /// <summary>
        /// 根据建筑类型获取建筑的宽度和高度
        /// </summary>
        /// <param name="buildingType">建筑类型</param>
        /// <param name="width">输出宽度</param>
        /// <param name="height">输出高度</param>
        public static void GetBuildingDimensions(BuildingType buildingType, out int width, out int height)
        {
            switch (buildingType)
            {
                case BuildingType.Base: // 基地
                    width = 10;
                    height = 10;
                    break;
                case BuildingType.Mine: // 矿
                    width = 5;
                    height = 5;
                    break;
                case BuildingType.Smelter: // 冶金厂
                    width = 4;
                    height = 4;
                    break;
                case BuildingType.PowerPlant: // 电厂
                    width = 6;
                    height = 4;
                    break;
                case BuildingType.Factory: // 坦克工厂
                    width = 12;
                    height = 8;
                    break;
                default:
                    width = 4;
                    height = 4;
                    break;
            }
        }

        /// <summary>
        /// 检查建筑放置位置是否有效（无阻挡且在地图范围内）
        /// </summary>
        /// <param name="buildingType">建筑类型</param>
        /// <param name="position">建筑位置（世界坐标）</param>
        /// <param name="mapManager">地图管理器</param>
        /// <returns>是否可以放置建筑</returns>
        public static bool CheckBuildingPlacement(BuildingType buildingType, zVector3 position, IFlowFieldMap mapManager)
        {
            if (mapManager == null)
            {
                UnityEngine.Debug.LogWarning("[BuildingPlacementUtils] 无法获取地图管理器");
                return false;
            }

            // 获取建筑尺寸
            GetBuildingDimensions(buildingType, out int width, out int height);
            
            // 计算建筑占据的矩形区域（以建筑中心为基准）
            int halfWidth = width / 2;
            int halfHeight = height / 2;
            
            // 将世界坐标转换为格子坐标
            mapManager.WorldToGrid(new zVector2(position.x, position.z), out int gridX, out int gridY);
            
            // 计算建筑占据的格子范围
            int minX = gridX - halfWidth;
            int minY = gridY - halfHeight;
            int maxX = gridX + halfWidth;
            int maxY = gridY + halfHeight;
            
            // 检查建筑是否在地图边界内
            if (minX < 0 || minY < 0 || maxX >= mapManager.GetWidth() || maxY >= mapManager.GetHeight())
            {
                UnityEngine.Debug.Log($"[BuildingPlacementUtils] 建筑位置超出地图边界。位置:({gridX},{gridY}), 尺寸:{width}x{height}, 地图尺寸:{mapManager.GetWidth()}x{mapManager.GetHeight()}");
                return false;
            }
            
            // 检查建筑占据的所有格子是否都可以行走（无阻挡）
            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    if (!mapManager.IsWalkable(x, y))
                    {
                        UnityEngine.Debug.Log($"[BuildingPlacementUtils] 建筑位置存在阻挡。阻挡格子:({x},{y})");
                        return false;
                    }
                }
            }
            
            return true;
        }
    }
}