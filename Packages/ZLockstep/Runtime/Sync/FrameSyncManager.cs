using System.Collections.Generic;
using ZLockstep.Simulation;
using ZLockstep.Sync.Command;
using UnityEngine;

namespace ZLockstep.Sync
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// 帧同步管理器
    /// ═══════════════════════════════════════════════════════════════
    /// 
    /// 职责：
    /// - 管理帧确认状态（哪些帧可以执行）
    /// - 缓冲未来帧的命令
    /// - 等待网络确认（防止客户端超前）
    /// 
    /// 核心逻辑：
    /// - confirmedFrame：服务器已确认的最大帧号
    /// - currentFrame：客户端当前执行到的帧号
    /// - 只有 currentFrame < confirmedFrame 时才能执行
    /// 
    /// 设计原则：
    /// - FrameSyncManager 只负责"判断"和"准备命令"
    /// - 不直接调用 world.Update()
    /// - 实际执行由 Game 负责（通过回调）
    /// 
    /// 使用场景：
    /// - 单机模式：不使用（直接执行）
    /// - 网络模式：必须使用（等待服务器确认）
    /// - 回放模式：不使用（按录制顺序执行）
    /// </summary>
    public class FrameSyncManager
    {
        private readonly zWorld _world;
        
        /// <summary>
        /// 服务器已确认的最大帧号
        /// 只有 <= confirmedFrame 的帧才能执行
        /// </summary>
        private int _confirmedFrame = -1;
        
        /// <summary>
        /// 客户端当前执行到的帧号
        /// 注意：初始为-1，第一帧是0（与 world.Tick 同步）
        /// </summary>
        private int _currentFrame = -1;
        
        /// <summary>
        /// 未来帧的命令缓冲区
        /// Key: 帧号, Value: 该帧的所有命令
        /// </summary>
        private readonly Dictionary<int, List<ICommand>> _frameCommands = 
            new Dictionary<int, List<ICommand>>();
        
        /// <summary>
        /// 等待确认的本地命令
        /// 已发送到服务器，但还未收到确认
        /// </summary>
        private readonly List<ICommand> _pendingLocalCommands = new List<ICommand>();

        /// <summary>
        /// 最大等待帧数（超过则认为掉线）
        /// </summary>
        public int MaxWaitFrames { get; set; } = 300; // 默认15秒（假设20FPS）

        public FrameSyncManager(zWorld world)
        {
            _world = world;
            
            // 同步初始状态：world.Tick 初始为 0，currentFrame 初始为 -1
            // 第一次 PrepareNextFrame() 会变成 0，与 world.Tick 同步
            _currentFrame = _world.Tick - 1;
            
            UnityEngine.Debug.Log($"[FrameSyncManager] 初始化：world.Tick={_world.Tick}, currentFrame={_currentFrame}");
        }

        /// <summary>
        /// 检查是否可以推进到下一帧
        /// </summary>
        /// <returns>true=可以推进，false=需要等待</returns>
        public bool CanAdvanceFrame()
        {
            int nextFrame = _currentFrame + 1;

            // 检查下一帧是否已确认
            if (nextFrame > _confirmedFrame)
            {
                // 未确认，需要等待
                int waitingFrames = nextFrame - _confirmedFrame;
                
                if (waitingFrames > MaxWaitFrames)
                {
                    Debug.LogError($"[FrameSyncManager] 等待超时！当前帧={_currentFrame}, 确认帧={_confirmedFrame}, 等待={waitingFrames}帧");
                    // TODO: 触发断线重连逻辑
                }
                else if (waitingFrames > 10)
                {
                    Debug.LogWarning($"[FrameSyncManager] 等待服务器确认... 延迟={waitingFrames}帧");
                }
                
                return false; // 不能推进，等待中
            }

            return true; // 可以推进
        }

        /// <summary>
        /// 准备执行下一帧（提交命令，但不执行 world.Update）
        /// 应该在 CanAdvanceFrame() 返回 true 后调用
        /// </summary>
        /// <returns>准备好的帧号</returns>
        public int PrepareNextFrame()
        {
            int nextFrame = _currentFrame + 1;

            // 从缓冲区取出该帧的命令并提交
            if (_frameCommands.TryGetValue(nextFrame, out var commands))
            {
                // 按确定性顺序提交命令
                commands.Sort((a, b) => 
                {
                    // 先按PlayerId排序，再按CommandType排序
                    int result = a.PlayerId.CompareTo(b.PlayerId);
                    if (result == 0)
                        result = a.CommandType.CompareTo(b.CommandType);
                    return result;
                });

                foreach (var cmd in commands)
                {
                    // 关键：设置 ExecuteFrame 为 nextFrame
                    // 当 ExecuteLogicFrame(nextFrame) 被调用时，world.Tick 会被设置为 nextFrame
                    // 然后 CommandManager.ExecuteFrame() 会查找 _futureCommands[nextFrame]
                    cmd.ExecuteFrame = nextFrame;
                    _world.CommandManager.SubmitCommand(cmd);
                }

                _frameCommands.Remove(nextFrame);
                
                Debug.Log($"[FrameSyncManager] 准备Frame {nextFrame}，命令数={commands.Count}");
            }
            else
            {
                // 没有命令也要推进（空帧）
                Debug.Log($"[FrameSyncManager] 准备Frame {nextFrame}（空帧）");
            }

            // 更新当前帧
            _currentFrame = nextFrame;

            Debug.Log($"[FrameSyncManager] currentFrame={_currentFrame}, world.Tick={_world.Tick} (将在ExecuteLogicFrame中同步)");

            return nextFrame;
        }

        /// <summary>
        /// 服务器确认帧（网络层调用）
        /// </summary>
        /// <param name="frame">确认的帧号</param>
        /// <param name="commands">该帧的所有命令（来自所有玩家）</param>
        public void ConfirmFrame(int frame, List<ICommand> commands)
        {
            if (frame <= _confirmedFrame)
            {
                Debug.LogWarning($"[FrameSyncManager] 收到重复确认：Frame {frame}（已确认到{_confirmedFrame}）");
                return;
            }

            // 更新确认帧
            _confirmedFrame = frame;

            // 存储该帧的命令
            if (commands != null && commands.Count > 0)
            {
                _frameCommands[frame] = new List<ICommand>(commands);
            }

            Debug.Log($"[FrameSyncManager] 服务器确认Frame {frame}，命令数={commands?.Count ?? 0}");
        }

        /// <summary>
        /// 批量确认多帧（用于追帧/重连）
        /// </summary>
        /// <param name="frameCommandsMap">帧号到命令列表的映射</param>
        public void ConfirmFrames(Dictionary<int, List<ICommand>> frameCommandsMap)
        {
            if (frameCommandsMap == null || frameCommandsMap.Count == 0)
                return;

            int minFrame = int.MaxValue;
            int maxFrame = int.MinValue;

            foreach (var kvp in frameCommandsMap)
            {
                int frame = kvp.Key;
                var commands = kvp.Value;

                if (frame > _confirmedFrame)
                {
                    _confirmedFrame = frame;
                }

                if (commands != null && commands.Count > 0)
                {
                    _frameCommands[frame] = new List<ICommand>(commands);
                }

                minFrame = System.Math.Min(minFrame, frame);
                maxFrame = System.Math.Max(maxFrame, frame);
            }

            Debug.Log($"[FrameSyncManager] 批量确认 {frameCommandsMap.Count} 帧，范围: {minFrame}-{maxFrame}，最新确认帧: {_confirmedFrame}");
        }

        /// <summary>
        /// 检查是否还有待执行的帧（用于追帧）
        /// </summary>
        public bool HasPendingFrames()
        {
            return _currentFrame < _confirmedFrame;
        }

        /// <summary>
        /// 获取待执行的帧数
        /// </summary>
        public int GetPendingFrameCount()
        {
            return System.Math.Max(0, _confirmedFrame - _currentFrame);
        }


        /// <summary>
        /// 提交本地命令（会发送到服务器）
        /// </summary>
        public void SubmitLocalCommand(ICommand command, INetworkAdapter networkAdapter)
        {
            // 1. 加入待确认列表
            _pendingLocalCommands.Add(command);

            // 2. 发送到服务器
            networkAdapter?.SendCommandToServer(command);

            Debug.Log($"[FrameSyncManager] 提交本地命令：{command.GetType().Name}，等待服务器确认");
        }

        /// <summary>
        /// 获取当前状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            int waitingFrames = (_confirmedFrame - _currentFrame);
            return $"当前帧={_currentFrame}, 确认帧={_confirmedFrame}, " +
                   $"等待={waitingFrames}, 缓冲命令={_frameCommands.Count}帧";
        }

        /// <summary>
        /// 重置状态（用于断线重连）
        /// </summary>
        public void Reset()
        {
            _confirmedFrame = -1;
            _currentFrame = -1;
            _frameCommands.Clear();
            _pendingLocalCommands.Clear();
            
            Debug.Log("[FrameSyncManager] 重置状态");
        }
    }

    /// <summary>
    /// 网络适配器接口（用于解耦）
    /// </summary>
    public interface INetworkAdapter
    {
        void SendCommandToServer(ICommand command);
    }
}

