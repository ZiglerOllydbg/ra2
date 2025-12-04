using ZLockstep.Simulation.ECS.Components;
using zUnity;

namespace ZLockstep.Simulation.ECS.Utils
{
    /// <summary>
    /// 建筑边界计算工具类
    /// 提供与建筑边界相关的计算方法
    /// </summary>
    public static class BuildingBoundaryUtils
    {
        /// <summary>
        /// 计算追击者/攻击者到建筑边界的交点
        /// </summary>
        /// <param name="chaserPosition">追击者/攻击者位置</param>
        /// <param name="building">建筑组件</param>
        /// <param name="world">游戏世界引用</param>
        /// <returns>建筑边界上的交点</returns>
        public static zVector2 CalculateBuildingBoundaryPoint(zVector3 chaserPosition, BuildingComponent building, zWorld world)
        {
            // 获取地图管理器
            var mapManager = world.GameInstance.GetMapManager();
            if (mapManager == null)
                return new zVector2(chaserPosition.x, chaserPosition.z); // 出错时返回当前位置
            
            // 获取建筑在世界坐标系中的边界框
            zfloat gridSize = mapManager.GetGridSize();
            zVector2 buildingCenter = mapManager.GridToWorld(building.GridX, building.GridY);
            
            // 计算建筑边界
            zfloat halfWidth = (building.Width * gridSize) / 2;
            zfloat halfHeight = (building.Height * gridSize) / 2;
            
            zVector2 minBound = new zVector2(buildingCenter.x - halfWidth, buildingCenter.y - halfHeight);
            zVector2 maxBound = new zVector2(buildingCenter.x + halfWidth, buildingCenter.y + halfHeight);
            
            // 构建从追击者指向建筑中心的射线
            zVector2 rayOrigin = new zVector2(chaserPosition.x, chaserPosition.z);
            zVector2 toBuilding = buildingCenter - rayOrigin;
            zVector2 rayDirection = toBuilding.normalized;
            
            // 如果方向向量为零向量，则直接返回建筑中心
            if (rayDirection.sqrMagnitude == zfloat.Zero)
                return buildingCenter;
            
            // 计算射线与AABB的交点
            zfloat distance;
            if (RayIntersectsAABB(rayOrigin, rayDirection, minBound, maxBound, out distance))
            {
                // 返回第一个交点
                return rayOrigin + rayDirection * distance;
            }
            
            // 如果没有交点（理论上不应该发生），返回建筑中心
            return buildingCenter;
        }
        
        /// <summary>
        /// 检查建筑是否在指定范围内
        /// </summary>
        /// <param name="chaserPosition">追击者/攻击者位置</param>
        /// <param name="building">建筑组件</param>
        /// <param name="range">检测范围</param>
        /// <param name="world">游戏世界引用</param>
        /// <returns>建筑是否在范围内</returns>
        public static bool IsBuildingInRange(zVector3 chaserPosition, BuildingComponent building, zfloat range, zWorld world)
        {
            // 获取地图管理器
            var mapManager = world.GameInstance.GetMapManager();
            if (mapManager == null)
                return false;
            
            // 获取建筑在世界坐标系中的边界框
            zfloat gridSize = mapManager.GetGridSize();
            zVector2 buildingCenter = mapManager.GridToWorld(building.GridX, building.GridY);
            
            // 计算建筑边界
            zfloat halfWidth = (building.Width * gridSize) / 2;
            zfloat halfHeight = (building.Height * gridSize) / 2;
            
            zVector2 minBound = new zVector2(buildingCenter.x - halfWidth, buildingCenter.y - halfHeight);
            zVector2 maxBound = new zVector2(buildingCenter.x + halfWidth, buildingCenter.y + halfHeight);
            
            // 计算追击者位置到建筑边界框的最短距离
            zfloat distX = zfloat.Zero;
            zfloat distY = zfloat.Zero;
            
            // X轴方向的距离
            if (chaserPosition.x < minBound.x)
                distX = minBound.x - chaserPosition.x;
            else if (chaserPosition.x > maxBound.x)
                distX = chaserPosition.x - maxBound.x;
                
            // Y轴方向的距离 (注意: 在世界坐标系中Z对应Y)
            if (chaserPosition.z < minBound.y)
                distY = minBound.y - chaserPosition.z;
            else if (chaserPosition.z > maxBound.y)
                distY = chaserPosition.z - maxBound.y;
                
            // 计算到边界框的欧几里得距离
            zfloat distanceSqr = distX * distX + distY * distY;
            zfloat rangeSqr = range * range;
            
            return distanceSqr <= rangeSqr;
        }
        
        /// <summary>
        /// 计算射线与轴对齐包围盒(AABB)的交点
        /// </summary>
        private static bool RayIntersectsAABB(zVector2 rayOrigin, zVector2 rayDirection, zVector2 min, zVector2 max, out zfloat distance)
        {
            distance = zfloat.Infinity;
            
            // 射线方向不能为零
            if (rayDirection.sqrMagnitude == zfloat.Zero)
                return false;
            
            zfloat tMin = zfloat.Zero;
            zfloat tMax = zfloat.Infinity;
            
            // 分别处理X和Y轴
            for (int i = 0; i < 2; i++)
            {
                if (i == 0) // X轴
                {
                    // 使用zfloat的近似零值检查
                    if (rayDirection.x > -zfloat.Epsilon && rayDirection.x < zfloat.Epsilon) // 射线平行于Y平面
                    {
                        if (rayOrigin.x < min.x || rayOrigin.x > max.x)
                            return false; // 射线在盒子外部
                    }
                    else
                    {
                        zfloat t1 = (min.x - rayOrigin.x) / rayDirection.x;
                        zfloat t2 = (max.x - rayOrigin.x) / rayDirection.x;
                        
                        zfloat tNear = (t1 < t2) ? t1 : t2;
                        zfloat tFar = (t1 < t2) ? t2 : t1;
                        
                        tMin = (tMin > tNear) ? tMin : tNear;
                        tMax = (tMax < tFar) ? tMax : tFar;
                        
                        if (tMin > tMax || tMax < zfloat.Zero)
                            return false;
                    }
                }
                else // Y轴
                {
                    // 使用zfloat的近似零值检查
                    if (rayDirection.y > -zfloat.Epsilon && rayDirection.y < zfloat.Epsilon) // 射线平行于X平面
                    {
                        if (rayOrigin.y < min.y || rayOrigin.y > max.y)
                            return false; // 射线在盒子外部
                    }
                    else
                    {
                        zfloat t1 = (min.y - rayOrigin.y) / rayDirection.y;
                        zfloat t2 = (max.y - rayOrigin.y) / rayDirection.y;
                        
                        zfloat tNear = (t1 < t2) ? t1 : t2;
                        zfloat tFar = (t1 < t2) ? t2 : t1;
                        
                        tMin = (tMin > tNear) ? tMin : tNear;
                        tMax = (tMax < tFar) ? tMax : tFar;
                        
                        if (tMin > tMax || tMax < zfloat.Zero)
                            return false;
                    }
                }
            }
            
            distance = tMin;
            return true;
        }
    }
}