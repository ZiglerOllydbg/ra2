using System.Collections.Generic;
using ZLockstep.Simulation;

namespace ZLockstep.Sync
{
    /// <summary>
    /// 锁步同步管理器
    /// 负责驱动仿真核心，并收集、调度和分发命令。
    /// </summary>
    public class LockstepManager
    {
        private zWorld _world;
        private Dictionary<int, List<ICommand>> _commandBuffer = new Dictionary<int, List<ICommand>>();
        private bool _isRunning = false;
        
        // 模拟一个固定的逻辑帧率，例如30 FPS
        private const float FrameInterval = 0.033f;
        private float _timeAccumulator = 0f;

        public void Init()
        {
            _world = new zWorld();
            _world.Init(30); // 假设逻辑帧率为30
            _isRunning = true;
        }

        /// <summary>
        /// 供外部提交命令的接口
        /// </summary>
        public void ScheduleCommand(ICommand command, int executionTick)
        {
            if (!_commandBuffer.ContainsKey(executionTick))
            {
                _commandBuffer[executionTick] = new List<ICommand>();
            }
            _commandBuffer[executionTick].Add(command);
        }

        /// <summary>
        /// 主更新循环，应由一个外部时钟（如GameLauncher）以固定频率调用
        /// </summary>
        /// <param name="deltaTime">现实世界经过的时间（秒）</param>
        public void UpdateLoop(float deltaTime)
        {
            if (!_isRunning) return;

            _timeAccumulator += deltaTime;

            // 模拟追帧逻辑：如果真实时间超过了逻辑帧的间隔，就执行一次或多次逻辑更新
            while (_timeAccumulator >= FrameInterval)
            {
                // 1. 从缓冲区获取当前帧的命令
                List<ICommand> commandsForCurrentTick = null;
                if (_commandBuffer.TryGetValue(_world.Tick, out var commands))
                {
                    commandsForCurrentTick = commands;
                    // TODO: 对命令进行确定性排序
                }

                // 2. TODO: 将命令传递给CommandProcessingSystem

                // 3. 驱动世界前进一帧
                _world.Update();

                _timeAccumulator -= FrameInterval;
            }
        }
        
        public void Shutdown()
        {
            _isRunning = false;
            _world?.Shutdown();
        }
    }
}
