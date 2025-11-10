using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using ZLockstep.View;
using ZLockstep.Flow;

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

                // 检查是否应该移除尸体；检查是否存在可视化组件
                if (death.ShouldRemove(currentTick) && !HasViewComponent(entity))
                {
                    // 如果实体有导航组件，则先移除导航能力
                    if (ComponentManager.HasComponent<FlowFieldNavigatorComponent>(entity))
                    {
                        var flowFieldSystem = World.GameInstance.GetNavSystem();
                        flowFieldSystem?.RemoveNavigator(entity);
                        ComponentManager.RemoveComponent<FlowFieldNavigatorComponent>(entity);
                    }
                    
                    // 销毁实体
                    World.EntityManager.DestroyEntity(entity);
                    zUDebug.Log($"[DeathRemovalSystem] 实体{entityId}尸体保留时间到期，已移除");
                }
            }
        }

        private bool HasViewComponent(Entity entity)
        {
            return ComponentManager.HasComponent<ViewComponent>(entity);
        }

    }
}