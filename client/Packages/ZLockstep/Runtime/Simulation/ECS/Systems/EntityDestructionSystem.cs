using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 实体销毁系统
    /// 检测死亡单位并在延迟后发布销毁事件
    /// </summary>
    public class EntityDestructionSystem : BaseSystem
    {
        // 记录待销毁的实体和销毁时间
        private readonly List<(int entityId, int destroyTick)> _pendingDestruction = new List<(int, int)>();
        
        // 延迟销毁的tick数 (3秒，假设逻辑帧率为30帧/秒)
        private const int DESTROY_DELAY_TICKS = 3 * 30; // 3秒 * 30帧/秒 = 90帧
        
        public override void Update()
        {
            int currentTick = World.TimeManager.Tick;
            
            // 检查是否有新死亡的单位
            CheckForNewlyDeadEntities(currentTick);
            
            // 处理待销毁的实体
            ProcessPendingDestruction(currentTick);
        }
        
        /// <summary>
        /// 检查新死亡的实体
        /// </summary>
        private void CheckForNewlyDeadEntities(int currentTick)
        {
            var entities = ComponentManager.GetAllEntityIdsWith<HealthComponent>();
            
            foreach (int entityId in entities)
            {
                var entity = new Entity(entityId);
                var health = ComponentManager.GetComponent<HealthComponent>(entity);
                
                // 检查是否刚死亡且尚未在待销毁列表中
                if (health.IsDead && !_pendingDestruction.Exists(x => x.entityId == entityId))
                {
                    int destroyAt = currentTick + DESTROY_DELAY_TICKS;
                    _pendingDestruction.Add((entityId, destroyAt));
                    UnityEngine.Debug.Log($"[EntityDestructionSystem] 实体 {entityId} 死亡，将在 {destroyAt} tick 销毁");
                }
            }
        }
        
        /// <summary>
        /// 处理待销毁的实体
        /// </summary>
        private void ProcessPendingDestruction(int currentTick)
        {
            // 处理到期的销毁任务
            for (int i = _pendingDestruction.Count - 1; i >= 0; i--)
            {
                var (entityId, destroyTick) = _pendingDestruction[i];
                
                if (currentTick >= destroyTick)
                {
                    var entity = new Entity(entityId);
                    // 销毁实体
                    World.EntityManager.DestroyEntity(entity);

                    zUDebug.Log($"[ProjectileSystem] 实体{entity.Id}已死亡并被销毁");
                    
                    // 从待销毁列表中移除
                    _pendingDestruction.RemoveAt(i);
                }
            }
        }
    }
}