using UnityEngine;
using System.Collections.Generic;
using ZLockstep.Simulation;
using ZLockstep.Simulation.Events;
using ZLockstep.Simulation.ECS;

namespace ZLockstep.View
{
    /// <summary>
    /// 视图事件监听器
    /// 监听逻辑层事件，创建和管理Unity表现
    /// </summary>
    public class ViewEventListener
    {
        private readonly zWorld _world;
        private readonly Transform _viewRoot;
        
        // 预制体映射表（单位类型 -> 预制体）
        private readonly Dictionary<int, GameObject> _unitPrefabs = new Dictionary<int, GameObject>();

        public ViewEventListener(zWorld world, Transform viewRoot)
        {
            _world = world;
            _viewRoot = viewRoot;
        }

        /// <summary>
        /// 注册单位预制体
        /// </summary>
        public void RegisterUnitPrefab(int unitType, GameObject prefab)
        {
            _unitPrefabs[unitType] = prefab;
        }

        /// <summary>
        /// 批量注册预制体
        /// </summary>
        public void RegisterUnitPrefabs(Dictionary<int, GameObject> prefabs)
        {
            foreach (var kvp in prefabs)
            {
                _unitPrefabs[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// 处理所有事件（在每帧调用）
        /// </summary>
        public void ProcessEvents()
        {
            // 处理单位创建事件
            var unitCreatedEvents = _world.ViewEventManager.GetEvents<UnitCreatedEvent>();
            foreach (var evt in unitCreatedEvents)
            {
                OnUnitCreated(evt);
            }

            _world.ViewEventManager.Clear();
        }

        /// <summary>
        /// 处理单位创建事件
        /// </summary>
        private void OnUnitCreated(UnitCreatedEvent evt)
        {
            var entity = new Entity(evt.EntityId);

            // 获取预制体
            GameObject prefab = null;
            if (evt.PrefabId > 0 && _unitPrefabs.ContainsKey(evt.PrefabId))
            {
                prefab = _unitPrefabs[evt.PrefabId];
            }
            else if (_unitPrefabs.ContainsKey(evt.UnitType))
            {
                prefab = _unitPrefabs[evt.UnitType];
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[ViewEventListener] 找不到单位类型 {evt.UnitType} 的预制体！");
                return;
            }

            // 实例化GameObject
            GameObject viewObject = Object.Instantiate(prefab, _viewRoot);
            viewObject.name = $"Unit_{evt.EntityId}_Type{evt.UnitType}_P{evt.PlayerId}";
            
            // 设置初始位置
            viewObject.transform.position = evt.Position.ToVector3();

            // 创建ViewComponent并添加到实体
            var viewComponent = ViewComponent.Create(viewObject, enableInterpolation: true);
            _world.ComponentManager.AddComponent(entity, viewComponent);

            Debug.Log($"[ViewEventListener] 为 Entity_{evt.EntityId} 创建了视图对象");
        }
    }
}

