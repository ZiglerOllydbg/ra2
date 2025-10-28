using zUnity;
using ZLockstep.Simulation.ECS;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场导航组件
    /// 挂载在需要使用流场寻路的实体上
    /// </summary>
    public struct FlowFieldNavigatorComponent : IComponent
    {
        /// <summary>
        /// 当前使用的流场ID（-1表示没有目标）
        /// </summary>
        public int CurrentFlowFieldId;

        /// <summary>
        /// RVO智能体ID（用于避障）
        /// </summary>
        public int RvoAgentId;

        /// <summary>
        /// 最大移动速度
        /// </summary>
        public zfloat MaxSpeed;

        /// <summary>
        /// 到达目标的阈值距离
        /// </summary>
        public zfloat ArrivalRadius;

        /// <summary>
        /// 减速开始的距离
        /// </summary>
        public zfloat SlowDownRadius;

        /// <summary>
        /// 是否已到达目标
        /// </summary>
        public bool HasReachedTarget;

        /// <summary>
        /// 是否启用流场导航
        /// </summary>
        public bool IsEnabled;

        /// <summary>
        /// 单位半径（用于RVO）
        /// </summary>
        public zfloat Radius;

        public static FlowFieldNavigatorComponent Create(zfloat radius, zfloat maxSpeed)
        {
            return new FlowFieldNavigatorComponent
            {
                CurrentFlowFieldId = -1,
                RvoAgentId = -1,
                MaxSpeed = maxSpeed,
                ArrivalRadius = new zfloat(0, 5000), // 0.5
                SlowDownRadius = new zfloat(2),
                HasReachedTarget = false,
                IsEnabled = true,
                Radius = radius
            };
        }
    }

    /// <summary>
    /// 移动目标组件
    /// 存储单位的目标位置
    /// </summary>
    public struct MoveTargetComponent : IComponent
    {
        /// <summary>
        /// 目标位置
        /// </summary>
        public zVector2 TargetPosition;

        /// <summary>
        /// 是否有目标
        /// </summary>
        public bool HasTarget;

        public static MoveTargetComponent Create(zVector2 targetPos)
        {
            return new MoveTargetComponent
            {
                TargetPosition = targetPos,
                HasTarget = true
            };
        }
    }
}
