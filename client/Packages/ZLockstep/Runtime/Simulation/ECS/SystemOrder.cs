namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 系统执行顺序枚举
    /// 数字越小优先级越高，越早执行
    /// </summary>
    public enum SystemOrder 
    {
        /// <summary>
        /// 普通系统（默认）
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// 生产系统
        /// </summary>
        Produce = 1,
        
        /// <summary>
        /// 表现系统
        /// </summary>
        Presentation = 2
    }
}