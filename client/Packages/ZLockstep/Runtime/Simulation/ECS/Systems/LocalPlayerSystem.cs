using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 本地玩家管理系统
    /// 负责管理本地玩家相关逻辑
    /// </summary>
    public class LocalPlayerSystem : ISystem
    {
        private ComponentManager _componentManager;
        private EntityManager _entityManager;
        private int _localPlayerId;
        
        public LocalPlayerSystem(ComponentManager componentManager, EntityManager entityManager)
        {
            _componentManager = componentManager;
            _entityManager = entityManager;
            _localPlayerId = -1; // 默认无本地玩家
        }
        
        public void SetWorld(zWorld world)
        {
            // 初始化系统
        }
        
        public void Update()
        {
            // 每帧更新逻辑
        }
        
        /// <summary>
        /// 设置本地玩家ID
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        public void SetLocalPlayerId(int playerId)
        {
            _localPlayerId = playerId;
        }
        
        /// <summary>
        /// 获取本地玩家ID
        /// </summary>
        /// <returns>本地玩家ID</returns>
        public int GetLocalPlayerId()
        {
            return _localPlayerId;
        }
        
        /// <summary>
        /// 为实体添加本地玩家组件
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家名称</param>
        /// <param name="isLocal">是否为本地玩家</param>
        public void AddLocalPlayerComponent(Entity entity, int playerId, string playerName, bool isLocal = true)
        {
            var localPlayerComponent = new Components.LocalPlayerComponent(playerId, playerName, isLocal);
            _componentManager.AddComponent(entity, localPlayerComponent);
        }
        
        /// <summary>
        /// 获取拥有本地玩家组件的所有实体ID
        /// </summary>
        /// <returns>实体ID枚举</returns>
        public IEnumerable<int> GetLocalPlayerEntities()
        {
            return _componentManager.GetAllEntityIdsWith<Components.LocalPlayerComponent>();
        }
        
        /// <summary>
        /// 检查实体是否为本地玩家控制
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>是否为本地玩家控制</returns>
        public bool IsLocalPlayerEntity(Entity entity)
        {
            if (_componentManager.HasComponent<Components.LocalPlayerComponent>(entity))
            {
                var localPlayerComponent = _componentManager.GetComponent<Components.LocalPlayerComponent>(entity);
                return localPlayerComponent.IsLocal && localPlayerComponent.PlayerId == _localPlayerId;
            }
            return false;
        }
        
        /// <summary>
        /// 获取本地玩家控制的实体
        /// </summary>
        /// <returns>本地玩家实体，如果不存在则返回null</returns>
        public Entity? GetLocalPlayerEntity()
        {
            var entities = _componentManager.GetAllEntityIdsWith<Components.LocalPlayerComponent>();
            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                var localPlayerComponent = _componentManager.GetComponent<Components.LocalPlayerComponent>(entity);
                if (localPlayerComponent.IsLocal && localPlayerComponent.PlayerId == _localPlayerId)
                {
                    return entity;
                }
            }
            return null;
        }
        
        public int GetOrder()
        {
            // 本地玩家系统的执行优先级
            return (int)SystemOrder.Normal;
        }
    }
}