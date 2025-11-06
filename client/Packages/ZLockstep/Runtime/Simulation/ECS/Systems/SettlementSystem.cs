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
        private int _localPlayerId;
        private bool _gameOver = false;

        public SettlementSystem(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
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
            // 统计每个阵营的存活坦克数量
            var tankCounts = CountAliveTanksByCamp();
            
            // 获取本地玩家阵营ID
            int localPlayerCampId = _localPlayerId;
            
            // 检查所有阵营是否都没有存活坦克（平局）
            bool allCampsDead = true;
            foreach (var kvp in tankCounts)
            {
                if (kvp.Value > 0)
                {
                    allCampsDead = false;
                    break;
                }
            }
            
            // 如果所有阵营都没有存活坦克，则为平局
            if (allCampsDead || tankCounts.Count == 0)
            {
                // 平局情况
                var gameOverEvent = new GameOverEvent
                {
                    IsVictory = false, // 平局算作失败
                    WinningCampId = -1 // -1表示平局
                };
                EventManager.Publish(gameOverEvent);
                _gameOver = true;
                return;
            }
            
            // 检查本地玩家是否失败（自己的所有坦克都死亡）
            if (tankCounts.ContainsKey(localPlayerCampId) && tankCounts[localPlayerCampId] <= 0)
            {
                // 本地玩家失败
                var gameOverEvent = new GameOverEvent
                {
                    IsVictory = false,
                    WinningCampId = GetWinningCampId(tankCounts, localPlayerCampId)
                };
                EventManager.Publish(gameOverEvent);
                _gameOver = true;
                return;
            }
            
            // 检查本地玩家是否胜利（其他所有玩家的坦克都死亡）
            bool allEnemiesDefeated = true;
            foreach (var kvp in tankCounts)
            {
                if (kvp.Key != localPlayerCampId && kvp.Value > 0)
                {
                    allEnemiesDefeated = false;
                    break;
                }
            }
            
            if (allEnemiesDefeated && tankCounts.ContainsKey(localPlayerCampId) && tankCounts[localPlayerCampId] > 0)
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
        /// 统计每个阵营的存活坦克数量
        /// </summary>
        /// <returns>阵营ID到存活坦克数量的映射</returns>
        private Dictionary<int, int> CountAliveTanksByCamp()
        {
            var tankCounts = new Dictionary<int, int>();
            
            var unitEntities = ComponentManager.GetAllEntityIdsWith<UnitComponent>();
            
            foreach (var entityId in unitEntities)
            {
                var entity = new Entity(entityId);
                
                // 检查是否为坦克单位（UnitType == 2）
                var unit = ComponentManager.GetComponent<UnitComponent>(entity);
                if (unit.UnitType != 2) // 只统计坦克
                    continue;
                    
                // 检查是否有阵营组件
                if (!ComponentManager.HasComponent<CampComponent>(entity))
                    continue;
                    
                // 检查是否存活
                if (ComponentManager.HasComponent<DeathComponent>(entity))
                    continue; // 已经死亡
                    
                if (ComponentManager.HasComponent<HealthComponent>(entity))
                {
                    var health = ComponentManager.GetComponent<HealthComponent>(entity);
                    if (health.CurrentHealth <= zfloat.Zero)
                        continue; // 生命值为0，视为死亡
                }
                
                // 统计存活坦克
                var camp = ComponentManager.GetComponent<CampComponent>(entity);
                if (tankCounts.ContainsKey(camp.CampId))
                {
                    tankCounts[camp.CampId]++;
                }
                else
                {
                    tankCounts[camp.CampId] = 1;
                }
            }
            
            return tankCounts;
        }
        
        /// <summary>
        /// 获取胜利阵营ID
        /// </summary>
        /// <param name="tankCounts">各阵营坦克数量</param>
        /// <param name="localPlayerCampId">本地玩家阵营ID</param>
        /// <returns>胜利阵营ID</returns>
        private int GetWinningCampId(Dictionary<int, int> tankCounts, int localPlayerCampId)
        {
            foreach (var kvp in tankCounts)
            {
                if (kvp.Key != localPlayerCampId && kvp.Value > 0)
                {
                    return kvp.Key;
                }
            }
            return -1; // 没有明确的胜利方
        }
    }
}