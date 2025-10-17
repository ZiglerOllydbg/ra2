using System;
using System.Collections.Generic;
using System.Linq;

namespace ZLockstep.Simulation.ECS
{
    public class ComponentManager
    {
        // 存储每个组件类型对应的组件数组
        // Key: Component Type
        // Value: Dictionary<EntityId, IComponent>
        private readonly Dictionary<Type, Dictionary<int, IComponent>> _componentStores = new Dictionary<Type, Dictionary<int, IComponent>>();

        /// <summary>
        /// [新增] 获取所有拥有指定组件类型的实体ID，并按ID排序以保证确定性。
        /// 这是为System遍历专门设计的方法。
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <returns>经过排序的只读实体ID集合</returns>
        public IEnumerable<int> GetAllEntityIdsWith<T>() where T : IComponent
        {
            var componentType = typeof(T);
            if (_componentStores.TryGetValue(componentType, out var store))
            {
                // 排序是保证确定性遍历的关键
                return store.Keys.OrderBy(id => id);
            }
            return Enumerable.Empty<int>();
        }

        public void AddComponent<T>(Entity entity, T component) where T : IComponent
        {
            var componentType = typeof(T);
            if (!_componentStores.ContainsKey(componentType))
            {
                _componentStores[componentType] = new Dictionary<int, IComponent>();
            }

            _componentStores[componentType][entity.Id] = component;
        }

        public T GetComponent<T>(Entity entity) where T : IComponent
        {
            var componentType = typeof(T);
            if (_componentStores.TryGetValue(componentType, out var store) && store.TryGetValue(entity.Id, out var component))
            {
                return (T)component;
            }

            return default;
        }

        public bool HasComponent<T>(Entity entity) where T : IComponent
        {
            var componentType = typeof(T);
            return _componentStores.ContainsKey(componentType) && _componentStores[componentType].ContainsKey(entity.Id);
        }

        public void RemoveComponent<T>(Entity entity) where T : IComponent
        {
            var componentType = typeof(T);
            if (_componentStores.TryGetValue(componentType, out var store))
            {
                store.Remove(entity.Id);
            }
        }

        public void EntityDestroyed(Entity entity)
        {
            // 当一个实体被销毁时，移除其所有组件
            foreach (var store in _componentStores.Values)
            {
                store.Remove(entity.Id);
            }
        }
    }
}
