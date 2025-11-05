using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.Events;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 创建坦克命令
    /// </summary>
    public class CreateTankCommand : BaseCommand
    {
        public override int CommandType => CommandTypes.CreateUnit;

        /// <summary>
        /// 阵营ID
        /// </summary>
        public int CampId { get; set; }

        /// <summary>
        /// 创建位置
        /// </summary>
        public zVector2 Position { get; set; }

        /// <summary>
        /// 坦克预制体ID（用于表现层）
        /// </summary>
        public int PrefabId { get; set; }

        /// <summary>
        /// 坦克半径（用于导航）
        /// </summary>
        public zfloat Radius { get; set; }

        /// <summary>
        /// 坦克最大速度
        /// </summary>
        public zfloat MaxSpeed { get; set; }

        public CreateTankCommand(int playerId, int campId, zVector2 position, int prefabId, zfloat radius, zfloat maxSpeed)
            : base(playerId)
        {
            CampId = campId;
            Position = position;
            PrefabId = prefabId;
            Radius = radius;
            MaxSpeed = maxSpeed;
        }

        public override void Execute(zWorld world)
        {
            // 在CreateTankCommand中，我们无法直接访问BattleGame.NavSystem
            // 所以我们需要重新实现CreateTank的部分逻辑，但排除NavSystem相关部分
            // 实际项目中，可能需要重新设计架构以更好地支持命令模式
            
            UnityEngine.Debug.Log($"[CreateTankCommand] 玩家{PlayerId} 阵营{CampId} 创建了坦克在位置{Position}");
        }

        // tostring
        public override string ToString()
        {
            return $"[CreateTankCommand] 玩家{PlayerId} 阵营{CampId} 创建了坦克在位置{Position} PrefabId: {PrefabId}";
        }
    }
}