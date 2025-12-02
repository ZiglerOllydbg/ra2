using ZLockstep.Simulation.ECS.Components;
using zUnity;

namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 经济系统工具类
    /// 提供经济相关的通用方法
    /// </summary>
    public static class EconomyUtils
    {
        /// <summary>
        /// 查找指定阵营的经济组件
        /// </summary>
        /// <param name="componentManager">组件管理器</param>
        /// <param name="campId">阵营ID</param>
        /// <param name="economyComponent">输出找到的经济组件</param>
        /// <param name="entity">输出找到的实体</param>
        /// <returns>是否找到指定阵营的经济组件</returns>
        public static bool TryGetEconomyComponentForCamp(ComponentManager componentManager, int campId, out EconomyComponent economyComponent, out Entity entity)
        {
            economyComponent = new EconomyComponent();
            entity = new Entity(-1);
            
            // 获取所有经济组件实体
            var economyEntities = componentManager.GetAllEntityIdsWith<EconomyComponent>();
            
            // 查找属于指定阵营的经济组件
            foreach (var entityId in economyEntities)
            {
                var currentEntity = new Entity(entityId);
                if (componentManager.HasComponent<CampComponent>(currentEntity))
                {
                    var campComponent = componentManager.GetComponent<CampComponent>(currentEntity);
                    if (campComponent.CampId == campId)
                    {
                        economyComponent = componentManager.GetComponent<EconomyComponent>(currentEntity);
                        entity = currentEntity;
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}