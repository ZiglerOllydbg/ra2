using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.Events;
using zUnity;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 结算系统
    /// 负责检测游戏结束条件并发布游戏结束事件
    /// </summary>
    public class SettlementSystem : BaseSystem
    {

        private bool _gameOver = false;

        public SettlementSystem()
        {
        
        }

        /// <summary>
        /// 游戏结束事件
        /// </summary>
        public class GameOverEvent : IEvent
        {
            /// <summary>
            /// 是否胜利
            /// true表示本地玩家胜利，false表示失败
            /// </summary>
            public bool IsVictory;

            /// <summary>
            /// 胜利方阵营ID
            /// </summary>
            public int WinningCampId;
        }
        
        public override void Update()
        {
            // 如果游戏已经结束，不再检查结束条件
            if (_gameOver)
                return;
                
            CheckGameOverCondition();
        }
        
        /// <summary>
        /// 检查游戏结束条件
        /// </summary>
        private void CheckGameOverCondition()
        {
            if (!ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            {
                // 可能是单机模式
                return;
            }

            // 获取本地玩家阵营ID
            int localPlayerCampId = ComponentManager.GetGlobalComponent<GlobalInfoComponent>().LocalPlayerCampId;
            
            // 获取所有参与游戏的阵营
            var campIds = GetAllCampIds();
            
            // 查找各阵营的主建筑状态
            var baseStatus = new Dictionary<int, bool>(); // true表示主建筑存活，false表示被摧毁
            foreach (var campId in campIds)
            {
                bool isBaseAlive = IsCampBaseAlive(campId);
                baseStatus[campId] = isBaseAlive;
            }
            
            // 检查本地玩家是否失败（自己的主建筑被摧毁）
            if (!baseStatus.ContainsKey(localPlayerCampId) || !baseStatus[localPlayerCampId])
            {
                // 本地玩家失败
                var gameOverEvent = new GameOverEvent
                {
                    IsVictory = false,
                    WinningCampId = GetFirstAliveEnemyCamp(baseStatus, localPlayerCampId)
                };
                EventManager.Publish(gameOverEvent);
                _gameOver = true;
                return;
            }
            
            // 检查本地玩家是否胜利（所有敌方主建筑都被摧毁）
            bool allEnemiesDefeated = true;
            foreach (var kvp in baseStatus)
            {
                if (kvp.Key != localPlayerCampId && kvp.Value)
                {
                    allEnemiesDefeated = false;
                    break;
                }
            }
            
            if (allEnemiesDefeated)
            {
                // 本地玩家胜利
                var gameOverEvent = new GameOverEvent
                {
                    IsVictory = true,
                    WinningCampId = localPlayerCampId
                };
                EventManager.Publish(gameOverEvent);
                _gameOver = true;
            }
        }
        
        /// <summary>
        /// 获取所有参与游戏的阵营ID
        /// </summary>
        /// <returns>阵营ID列表</returns>
        private List<int> GetAllCampIds()
        {
            var campIds = new List<int>();
            var economyEntities = ComponentManager.GetAllEntityIdsWith<EconomyComponent>();
            
            foreach (var entityId in economyEntities)
            {
                var entity = new Entity(entityId);
                if (ComponentManager.HasComponent<CampComponent>(entity))
                {
                    var camp = ComponentManager.GetComponent<CampComponent>(entity);
                    campIds.Add(camp.CampId);
                }
            }
            
            return campIds;
        }
        
        /// <summary>
        /// 检查指定阵营的主建筑是否存活
        /// </summary>
        /// <param name="campId">阵营ID</param>
        /// <returns>true表示主建筑存活，false表示被摧毁</returns>
        private bool IsCampBaseAlive(int campId)
        {
            // 查找该阵营的主建筑（使用MainBase组件优化查找）
            var (mainBaseComponent, buildingEntity) = ComponentManager.GetComponentWithCondition<MainBaseComponent>(
                e => ComponentManager.HasComponent<CampComponent>(e) && 
                     ComponentManager.GetComponent<CampComponent>(e).CampId == campId);
            
            // 如果找不到主建筑，认为已被摧毁
            if (buildingEntity.Id == -1)
                return false;
                
            // 检查主建筑是否被标记为死亡
            if (ComponentManager.HasComponent<DeathComponent>(buildingEntity))
                return false;
                
            // 检查主建筑血量
            if (ComponentManager.HasComponent<HealthComponent>(buildingEntity))
            {
                var health = ComponentManager.GetComponent<HealthComponent>(buildingEntity);
                if (health.CurrentHealth <= zfloat.Zero)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取第一个存活的敌方阵营ID
        /// </summary>
        /// <param name="baseStatus">各阵营主建筑状态</param>
        /// <param name="localPlayerCampId">本地玩家阵营ID</param>
        /// <returns>第一个存活的敌方阵营ID，如果没有则返回-1</returns>
        private int GetFirstAliveEnemyCamp(Dictionary<int, bool> baseStatus, int localPlayerCampId)
        {
            foreach (var kvp in baseStatus)
            {
                if (kvp.Key != localPlayerCampId && kvp.Value)
                {
                    return kvp.Key;
                }
            }
            return -1;
        }
    }
}