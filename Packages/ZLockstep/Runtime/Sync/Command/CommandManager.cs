using System.Collections.Generic;
using ZLockstep.Simulation;

namespace ZLockstep.Sync.Command
{
    /// <summary>
    /// 命令管理器
    /// 负责收集、排序和执行命令
    /// 支持确定性执行和网络同步
    /// </summary>
    public class CommandManager
    {
        private readonly zWorld _world;
        
        // 当前帧要执行的命令队列
        private readonly Queue<ICommand> _currentFrameCommands = new Queue<ICommand>();
        
        // 未来帧的命令缓冲区（用于网络同步）
        private readonly Dictionary<int, List<ICommand>> _futureCommands = new Dictionary<int, List<ICommand>>();
        
        // 已执行的命令历史（用于回放）
        private readonly List<ICommand> _commandHistory = new List<ICommand>();
        
        /// <summary>
        /// 是否记录命令历史（用于回放）
        /// </summary>
        public bool RecordHistory { get; set; } = false;

        public CommandManager(zWorld world)
        {
            _world = world;
        }

        /// <summary>
        /// 提交命令（立即执行或加入队列）
        /// </summary>
        public void SubmitCommand(ICommand command)
        {
            if (command.ExecuteFrame < 0 || command.ExecuteFrame == _world.Tick)
            {
                // 立即执行
                _currentFrameCommands.Enqueue(command);
            }
            else
            {
                // 延迟到指定帧执行（用于网络同步）
                if (!_futureCommands.ContainsKey(command.ExecuteFrame))
                {
                    _futureCommands[command.ExecuteFrame] = new List<ICommand>();
                }
                _futureCommands[command.ExecuteFrame].Add(command);
            }
        }

        /// <summary>
        /// 批量提交命令
        /// </summary>
        public void SubmitCommands(IEnumerable<ICommand> commands)
        {
            foreach (var command in commands)
            {
                SubmitCommand(command);
            }
        }

        /// <summary>
        /// 执行当前帧的所有命令
        /// 在逻辑帧更新时调用
        /// </summary>
        public void ExecuteFrame()
        {
            int currentTick = _world.Tick;

            // 从未来命令缓冲区取出当前帧的命令
            if (_futureCommands.TryGetValue(currentTick, out var futureCommands))
            {
                foreach (var cmd in futureCommands)
                {
                    _currentFrameCommands.Enqueue(cmd);
                }
                _futureCommands.Remove(currentTick);
            }

            // 按确定性顺序执行所有命令
            int executedCount = 0;
            while (_currentFrameCommands.Count > 0)
            {
                var command = _currentFrameCommands.Dequeue();
                
                try
                {
                    // 执行命令
                    command.Execute(_world);
                    
                    // 记录历史
                    if (RecordHistory)
                    {
                        _commandHistory.Add(command);
                    }
                    
                    executedCount++;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"[CommandManager] 执行命令失败：{command.GetType().Name}, 错误：{e.Message}");
                }
            }

            if (executedCount > 0)
            {
                UnityEngine.Debug.Log($"[CommandManager] Tick {currentTick}: 执行了 {executedCount} 个命令");
            }
        }

        /// <summary>
        /// 清空所有命令
        /// </summary>
        public void Clear()
        {
            _currentFrameCommands.Clear();
            _futureCommands.Clear();
        }

        /// <summary>
        /// 获取命令历史（用于回放）
        /// </summary>
        public IReadOnlyList<ICommand> GetCommandHistory()
        {
            return _commandHistory.AsReadOnly();
        }

        /// <summary>
        /// 清空命令历史
        /// </summary>
        public void ClearHistory()
        {
            _commandHistory.Clear();
        }

        /// <summary>
        /// 获取待执行的命令数量
        /// </summary>
        public int GetPendingCommandCount()
        {
            int count = _currentFrameCommands.Count;
            foreach (var futureList in _futureCommands.Values)
            {
                count += futureList.Count;
            }
            return count;
        }
    }
}

