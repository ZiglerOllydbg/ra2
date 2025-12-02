using System;

namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 经济组件，包含资金和电力资源
    /// </summary>
    [Serializable]
    public struct EconomyComponent : IComponent
    {
        /// <summary>
        /// 资金
        /// </summary>
        public int Money;

        /// <summary>
        /// 电力
        /// </summary>
        public int Power;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="money">初始资金</param>
        /// <param name="power">初始电力</param>
        public EconomyComponent(int money, int power)
        {
            Money = money;
            Power = power;
        }

        /// <summary>
        /// 创建默认经济组件（1500资金，0电力）
        /// </summary>
        /// <returns>经济组件</returns>
        public static EconomyComponent CreateDefault()
        {
            return new EconomyComponent(1500, 0);
        }

        /// <summary>
        /// 创建指定资金和电力的经济组件
        /// </summary>
        /// <param name="money">资金</param>
        /// <param name="power">电力</param>
        /// <returns>经济组件</returns>
        public static EconomyComponent Create(int money, int power)
        {
            return new EconomyComponent(money, power);
        }
    }
}