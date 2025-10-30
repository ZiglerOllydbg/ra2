namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 所有系统的基础接口。
    /// The base interface for all systems.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 设置该系统所属的世界。
        /// 这个方法会在系统被注册到 SystemManager 时调用。
        /// </summary>
        /// <param name="world">游戏世界</param>
        void SetWorld(zWorld world);

        /// <summary>
        /// 系统的主更新逻辑。
        /// </summary>
        void Update();
    }
}
