using System;

namespace ZLockstep.Simulation.Events
{
    /// <summary>
    /// 资金变化事件
    /// 当玩家的资金发生变化时触发
    /// </summary>
    public struct MoneyChangedEvent : IEvent
    {
        /// <summary>
        /// 玩家阵营ID
        /// </summary>
        public int CampId;

        /// <summary>
        /// 变化前的资金值
        /// </summary>
        public int OldMoney;

        /// <summary>
        /// 变化后的资金值
        /// </summary>
        public int NewMoney;

        /// <summary>
        /// 资金变化的原因（可选）
        /// </summary>
        public string Reason;
    }

    /// <summary>
    /// 电力变化事件
    /// 当玩家的电力发生变化时触发
    /// </summary>
    public struct PowerChangedEvent : IEvent
    {
        /// <summary>
        /// 玩家阵营ID
        /// </summary>
        public int CampId;

        /// <summary>
        /// 变化前的电力值
        /// </summary>
        public int OldPower;

        /// <summary>
        /// 变化后的电力值
        /// </summary>
        public int NewPower;

        /// <summary>
        /// 电力变化的原因（可选）
        /// </summary>
        public string Reason;
    }
}