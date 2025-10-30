using System.Collections.Generic;

namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 实体管理器
    /// 负责实体的创建、销毁和ID复用。
    /// </summary>
    public class EntityManager
    {
        // 用于存储已销毁并可回收利用的实体ID
        private readonly Queue<int> _recycledEntityIds = new Queue<int>();
        
        // 用于生成新的唯一ID
        private int _nextEntityId;
        private ComponentManager _componentManager;

        /// <summary>
        /// 当前活跃的实体总数
        /// </summary>
        public int ActiveEntityCount => _nextEntityId - _recycledEntityIds.Count;

        public EntityManager()
        {
            _nextEntityId = 0;
        }

        public void Init(ComponentManager componentManager)
        {
            _nextEntityId = 0;
            _componentManager = componentManager;
        }

        /// <summary>
        /// 创建一个新的实体
        /// </summary>
        /// <returns>返回创建的实体</returns>
        public Entity CreateEntity()
        {
            int entityId;
            if (_recycledEntityIds.Count > 0)
            {
                // 如果有可回收的ID，则优先使用
                entityId = _recycledEntityIds.Dequeue();
            }
            else
            {
                // 否则，生成一个新的ID
                entityId = _nextEntityId;
                _nextEntityId++;
            }

            return new Entity(entityId);
        }

        /// <summary>
        /// 销毁一个实体，将其ID回收到对象池以便复用
        /// </summary>
        /// <param name="entity">要销毁的实体</param>
        public void DestroyEntity(Entity entity)
        {
            // 在销毁实体前，先移除其所有关联的组件
            _componentManager.EntityDestroyed(entity);

            _recycledEntityIds.Enqueue(entity.Id);
        }
    }
}
