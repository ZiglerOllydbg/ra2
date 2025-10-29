using System.Linq;
using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 移动命令
    /// 让一个或多个单位移动到指定位置
    /// </summary>
    public class MoveCommand : BaseCommand
    {
        public override int CommandType => CommandTypes.Move;

        /// <summary>
        /// 要移动的实体ID列表
        /// </summary>
        public int[] EntityIds { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        public zVector3 TargetPosition { get; set; }

        /// <summary>
        /// 停止距离
        /// </summary>
        public zfloat StopDistance { get; set; }

        public MoveCommand(int playerId, int[] entityIds, zVector3 targetPosition, zfloat stopDistance = default)
            : base(playerId)
        {
            EntityIds = entityIds;
            TargetPosition = targetPosition;
            StopDistance = stopDistance > zfloat.Zero ? stopDistance : (zfloat)0.1f;
        }

        public override void Execute(zWorld world)
        {
            if (EntityIds == null || EntityIds.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[MoveCommand] 没有指定要移动的实体！");
                return;
            }

            int successCount = 0;
            foreach (var entityId in EntityIds)
            {
                var entity = new Entity(entityId);

                // 检查实体是否存在且属于该玩家
                if (!world.ComponentManager.HasComponent<UnitComponent>(entity))
                {
                    UnityEngine.Debug.LogWarning($"[MoveCommand] Entity_{entityId} 不存在或不是单位！");
                    continue;
                }

                var unit = world.ComponentManager.GetComponent<UnitComponent>(entity);
                if (unit.PlayerId != PlayerId)
                {
                    UnityEngine.Debug.LogWarning($"[MoveCommand] Entity_{entityId} 不属于玩家{PlayerId}！");
                    continue;
                }

                // 添加移动命令组件
                var moveCmd = new MoveCommandComponent(TargetPosition, StopDistance);
                world.ComponentManager.AddComponent(entity, moveCmd);

                successCount++;
            }

            if (successCount > 0)
            {
                UnityEngine.Debug.Log($"[MoveCommand] 玩家{PlayerId} 命令{successCount}个单位移动到 {TargetPosition}");
            }
        }

        // tostring
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"[MoveCommand] 玩家{PlayerId} 命令{EntityIds.Length}个单位移动到 {TargetPosition}");
            return sb.ToString();
        }

    }
    
}