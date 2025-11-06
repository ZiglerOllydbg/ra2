using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 死亡移除系统
    /// 负责在尸体保留时间到期后移除死亡实体
    /// </summary>
    public class DeathRemovalSystem : BaseSystem
    {
        public override void Update()
        {
            int currentTick = World.TimeManager.Tick;
            
            // 查找所有带有死亡组件的实体
            var entities = ComponentManager.GetAllEntityIdsWith<DeathComponent>();
            
            // 处理每个死亡实体
            foreach (int entityId in entities)
            {
                var entity = new Entity(entityId);
                var death = ComponentManager.GetComponent<DeathComponent>(entity);
                
                // 检查是否应该移除尸体
                if (death.ShouldRemove(currentTick))
                {
                    // 发布单位销毁事件，确保表现层能同步销毁视觉对象
                    var destroyEvent = new UnitDestroyedEvent
                    {
                        EntityId = entityId
                    };
                    EventManager.Publish(destroyEvent);
                    
                    // 销毁实体
                    World.EntityManager.DestroyEntity(entity);
                    
                    zUDebug.Log($"[DeathRemovalSystem] 实体{entityId}尸体保留时间到期，已移除");
                }
            }
        }
    }
}