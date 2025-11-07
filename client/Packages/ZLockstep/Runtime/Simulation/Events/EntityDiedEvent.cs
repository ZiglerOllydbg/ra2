namespace ZLockstep.Simulation.Events
{
    /// <summary>
    /// 实体死亡事件
    /// 当实体生命值降为0时发布，用于通知表现层播放死亡动画
    /// 注意：这个事件不代表实体被销毁，只是标记死亡状态
    /// 实体销毁由 UnitDestroyedEvent 处理
    /// </summary>
    public struct EntityDiedEvent : IEvent
    {
        /// <summary>
        /// 死亡实体的ID
        /// </summary>
        public int EntityId;

        /// <summary>
        /// 死亡实体的阵营ID
        /// </summary>
        public int CampId;
    }
}

