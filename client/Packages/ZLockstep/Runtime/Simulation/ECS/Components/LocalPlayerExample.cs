using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 本地玩家组件使用示例
    /// 演示如何使用本地玩家组件和系统
    /// </summary>
    public class LocalPlayerExample
    {
        private LocalPlayerSystem _localPlayerSystem;
        private ComponentManager _componentManager;
        private EntityManager _entityManager;
        
        public LocalPlayerExample(LocalPlayerSystem localPlayerSystem, ComponentManager componentManager, EntityManager entityManager)
        {
            _localPlayerSystem = localPlayerSystem;
            _componentManager = componentManager;
            _entityManager = entityManager;
        }
        
        /// <summary>
        /// 初始化本地玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家名称</param>
        public void InitializeLocalPlayer(int playerId, string playerName)
        {
            // 设置本地玩家ID
            _localPlayerSystem.SetLocalPlayerId(playerId);
            
            // 创建本地玩家实体
            var localPlayerEntity = _entityManager.CreateEntity();
            
            // 为实体添加本地玩家组件
            _localPlayerSystem.AddLocalPlayerComponent(localPlayerEntity, playerId, playerName);
            
            // 可以添加其他组件，如变换组件、单位组件等
            // ...
        }
        
        /// <summary>
        /// 检查某个实体是否为本地玩家控制
        /// </summary>
        /// <param name="entity">要检查的实体</param>
        /// <returns>是否为本地玩家控制</returns>
        public bool CheckIfLocalPlayer(Entity entity)
        {
            return _localPlayerSystem.IsLocalPlayerEntity(entity);
        }
        
        /// <summary>
        /// 获取本地玩家实体
        /// </summary>
        /// <returns>本地玩家实体</returns>
        public Entity? GetLocalPlayer()
        {
            return _localPlayerSystem.GetLocalPlayerEntity();
        }
        
        /// <summary>
        /// 获取所有本地玩家组件的实体
        /// </summary>
        public void PrintAllLocalPlayerEntities()
        {
            var entities = _localPlayerSystem.GetLocalPlayerEntities();
            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                var localPlayerComponent = _componentManager.GetComponent<LocalPlayerComponent>(entity);
                Debug.Log($"Entity {entityId} is player {localPlayerComponent.PlayerName} (ID: {localPlayerComponent.PlayerId})");
            }
        }
    }
}