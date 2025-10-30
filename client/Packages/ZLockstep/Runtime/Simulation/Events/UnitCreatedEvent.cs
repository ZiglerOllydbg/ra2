using zUnity;

namespace ZLockstep.Simulation.Events
{
    /// <summary>
    /// 单位创建事件
    /// 逻辑层创建单位后，通知表现层创建视图
    /// </summary>
    public struct UnitCreatedEvent : IEvent
    {
        /// <summary>
        /// 实体ID
        /// </summary>
        public int EntityId;

        /// <summary>
        /// 单位类型
        /// </summary>
        public int UnitType;

        /// <summary>
        /// 创建位置
        /// </summary>
        public zVector3 Position;

        /// <summary>
        /// 所属玩家ID
        /// </summary>
        public int PlayerId;

        /// <summary>
        /// 预制体ID（用于表现层）
        /// </summary>
        public int PrefabId;
    }
}

