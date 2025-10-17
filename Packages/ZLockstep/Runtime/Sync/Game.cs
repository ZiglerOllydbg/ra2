
namespace ZLockstep.Sync
{
    /// <summary>
    /// 游戏世界的顶层容器和启动器 (纯 C#)。
    /// </summary>
    public class Game
    {
        public LockstepManager LockstepManager { get; private set; }

        public void Init()
        {
            LockstepManager = new LockstepManager();
            LockstepManager.Init();
        }

        /// <summary>
        /// 游戏主更新，应由外部适配器（如Unity的MonoBehaviour）驱动。
        /// </summary>
        /// <param name="deltaTime">现实世界经过的时间（秒）</param>
        public void Update(float deltaTime)
        {
            LockstepManager?.UpdateLoop(deltaTime);
        }

        public void Shutdown()
        {
            LockstepManager?.Shutdown();
        }

        /// <summary>
        /// 提交命令的统一入口。
        /// </summary>
        public void ScheduleCommand(ICommand command, int executionTick)
        {
            LockstepManager?.ScheduleCommand(command, executionTick);
        }
    }
}
