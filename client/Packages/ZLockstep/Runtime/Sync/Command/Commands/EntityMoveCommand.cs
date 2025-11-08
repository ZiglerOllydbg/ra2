using System.Collections.Generic;
using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using System.Text;

namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 实体移动命令
    /// 直接使用导航系统设置移动目标
    /// </summary>
    
    [CommandType(CommandTypes.EntityMove)]
    public class EntityMoveCommand : BaseCommand
    {
        /// <summary>
        /// 要移动的实体ID列表
        /// </summary>
        public int[] EntityIds { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        public zVector2 TargetPosition { get; set; }

        public EntityMoveCommand(int playerId, int[] entityIds, zVector2 targetPosition)
            : base(playerId)
        {
            EntityIds = entityIds;
            TargetPosition = targetPosition;
        }

        public override void Execute(zWorld world)
        {
            // 通过world获取Game实例
            var game = world.GameInstance;
            if (game != null)
            {
                // 通过Game实例获取NavSystem
                var navSystem = game.GetNavSystem();
                if (navSystem != null)
                {
                    // 执行移动操作
                    List<Entity> entities = new List<Entity>();
                    foreach (var entityId in EntityIds)
                    {
                        entities.Add(new Entity(entityId));
                    }
                    
                    navSystem.SetMultipleTargets(entities, TargetPosition);
                    
                    if (entities.Count > 0)
                    {
                        UnityEngine.Debug.Log($"[EntityMoveCommand] 玩家{PlayerId} 命令{entities.Count}个单位移动到 {TargetPosition}");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[EntityMoveCommand] NavSystem未找到！");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[EntityMoveCommand] Game实例未找到！");
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"[EntityMoveCommand] 玩家{PlayerId} 命令{EntityIds.Length}个单位移动到 {TargetPosition}");
            return sb.ToString();
        }
    }
}