using System.Collections.Generic;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Sync.Command;

namespace ZLockstep.Sync
{
    /// <summary>
    /// 游戏模式枚举
    /// </summary>
    public enum GameMode
    {
        /// <summary>单机模式（无需等待）</summary>
        Standalone,
        
        /// <summary>网络客户端（需要帧同步）</summary>
        NetworkClient,
        
        /// <summary>网络服务器（不需要等待，直接推进）</summary>
        NetworkServer,
        
        /// <summary>回放模式（按录制顺序执行）</summary>
        Replay
    }

    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// 【第2层：应用控制层】Game
    /// ═══════════════════════════════════════════════════════════════
    /// 
    /// 职责定位：
    /// - 应用生命周期管理：Init/Update/Shutdown
    /// - 业务流程编排：决定游戏如何运行（单机/网络/回放）
    /// - 跨平台入口：可在Unity、服务器、测试环境使用
    /// - 唯一zWorld创建者：创建并持有唯一的游戏世界实例
    /// 
    /// 设计原则（奥卡姆剃刀）：
    /// - 只负责"控制"，不负责"呈现"（不管Unity资源）
    /// - 纯C#实现，不依赖Unity
    /// - 可被不同平台的适配器调用（GameWorldBridge/服务器/测试）
    /// 
    /// 层次关系：
    /// - 上层：被 GameWorldBridge(Unity) 或 Main()(服务器) 调用
    /// - 下层：创建并驱动 zWorld
    /// 
    /// 使用场景：
    /// - Unity客户端：通过 GameWorldBridge 调用
    /// - 服务器：直接在 Main() 中使用
    /// - 单元测试：直接实例化测试
    /// 
    /// 使用示例：
    ///   // 单机模式
    ///   var game = new Game(GameMode.Standalone);
    ///   game.Init();
    ///   while(running) {
    ///       game.Update(); // 每帧都执行
    ///   }
    ///   
    ///   // 网络模式
    ///   var game = new Game(GameMode.NetworkClient);
    ///   game.Init();
    ///   while(running) {
    ///       game.Update(); // 只有确认的帧才执行
    ///   }
    /// </summary>
    public class Game
    {
        /// <summary>
        /// 唯一的游戏逻辑世界
        /// </summary>
        public zWorld World { get; private set; }

        /// <summary>
        /// 帧同步管理器（仅网络模式使用）
        /// </summary>
        public FrameSyncManager FrameSyncManager { get; private set; }

        /// <summary>
        /// 游戏模式
        /// </summary>
        public GameMode Mode { get; private set; }

        /// <summary>
        /// 是否正在追帧（断线重连后快速追赶）
        /// </summary>
        public bool IsCatchingUp { get; private set; }

        private readonly int _frameRate;
        private readonly int _localPlayerId;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="mode">游戏模式（默认单机）</param>
        /// <param name="frameRate">逻辑帧率（默认20帧/秒）</param>
        /// <param name="localPlayerId">本地玩家ID（默认0）</param>
        public Game(GameMode mode = GameMode.Standalone, int frameRate = 20, int localPlayerId = 0)
        {
            Mode = mode;
            _frameRate = frameRate;
            _localPlayerId = localPlayerId;
        }

        /// <summary>
        /// 初始化游戏
        /// </summary>
        public void Init()
        {
            // 创建唯一的 zWorld
            World = new zWorld();
            World.Init(_frameRate);

            // 根据游戏模式创建帧同步管理器
            if (Mode == GameMode.NetworkClient)
            {
                FrameSyncManager = new FrameSyncManager(World);
                UnityEngine.Debug.Log($"[Game] 网络客户端模式 - 启用帧同步");
            }

            // 注册游戏系统
            RegisterSystems();

            UnityEngine.Debug.Log($"[Game] 初始化完成 - 模式:{Mode}, 帧率:{_frameRate}, 玩家ID:{_localPlayerId}");
        }

        /// <summary>
        /// 注册所有游戏系统
        /// 子类可以重写此方法自定义系统
        /// </summary>
        protected virtual void RegisterSystems()
        {
            // 注册移动系统
            World.SystemManager.RegisterSystem(new MovementSystem());

            // TODO: 注册其他系统
            // World.SystemManager.RegisterSystem(new CombatSystem());
            // World.SystemManager.RegisterSystem(new AISystem());
        }

        /// <summary>
        /// 游戏主更新循环
        /// 由外部驱动（Unity的FixedUpdate或服务器的循环）
        /// 
        /// 不同模式的行为：
        /// - Standalone: 每帧都执行
        /// - NetworkClient: 只有服务器确认的帧才执行（可能等待）
        /// - NetworkServer: 每帧都执行（服务器不等待）
        /// - Replay: 按录制顺序执行
        /// </summary>
        /// <returns>是否执行了逻辑更新（网络模式可能返回false表示等待中）</returns>
        public bool Update()
        {
            // 追帧模式：优先处理
            if (IsCatchingUp)
            {
                return UpdateCatchUp();
            }

            switch (Mode)
            {
                case GameMode.Standalone:
                case GameMode.NetworkServer:
                case GameMode.Replay:
                    // 单机/服务器/回放模式：直接执行
                    ExecuteLogicFrame();
                    return true;

                case GameMode.NetworkClient:
                    // 网络客户端模式：需要等待服务器确认
                    if (FrameSyncManager == null)
                    {
                        UnityEngine.Debug.LogError("[Game] 网络模式下FrameSyncManager未初始化！");
                        return false;
                    }

                    // 1. 检查是否可以推进
                    if (!FrameSyncManager.CanAdvanceFrame())
                    {
                        return false; // 等待服务器确认
                    }

                    // 2. 准备下一帧的命令（不执行world.Update）
                    int frameNumber = FrameSyncManager.PrepareNextFrame();
                    
                    // 3. 执行逻辑帧（传入目标帧号，确保 Tick 同步）
                    ExecuteLogicFrame(frameNumber);
                    
                    // UnityEngine.Debug.Log($"[Game] 执行网络帧 {frameNumber}, world.Tick={World.Tick}");
                    return true;

                default:
                    UnityEngine.Debug.LogError($"[Game] 未知的游戏模式: {Mode}");
                    return false;
            }
        }

        /// <summary>
        /// 追帧更新（快速执行，不等待）
        /// </summary>
        /// <returns>是否还在追帧</returns>
        private bool UpdateCatchUp()
        {
            if (FrameSyncManager == null)
            {
                UnityEngine.Debug.LogError("[Game] 追帧模式下FrameSyncManager未初始化！");
                IsCatchingUp = false;
                return false;
            }

            // 检查是否还有待执行的帧
            if (!FrameSyncManager.HasPendingFrames())
            {
                // 追帧完成
                StopCatchUp();
                return false;
            }

            // 执行一帧
            int frameNumber = FrameSyncManager.PrepareNextFrame();
            ExecuteLogicFrame(frameNumber);

            // 继续追帧
            return true;
        }

        /// <summary>
        /// 执行一个逻辑帧（内部方法）
        /// 所有模式都统一使用此方法
        /// </summary>
        /// <param name="targetTick">目标帧号（可选，用于锁帧同步）</param>
        private void ExecuteLogicFrame(int? targetTick = null)
        {
            // 1. 清空上一帧的事件（修复时序问题）
            World.EventManager.Clear();

            // 2. 更新唯一的 zWorld
            // - 单机模式：不传 targetTick，自动递增
            // - 网络模式：传入 targetTick，由锁帧系统控制
            World.Update(targetTick);
        }

        /// <summary>
        /// 提交命令（统一入口）
        /// 
        /// 不同模式的行为：
        /// - Standalone: 直接提交到CommandManager（立即执行）
        /// - NetworkClient: 发送到服务器，等待确认后执行
        /// - NetworkServer: 直接提交（服务器权威）
        /// </summary>
        public void SubmitCommand(ICommand command)
        {
            command.PlayerId = _localPlayerId;

            switch (Mode)
            {
                case GameMode.Standalone:
                case GameMode.NetworkServer:
                case GameMode.Replay:
                    // 单机/服务器模式：直接提交
                    World.CommandManager.SubmitCommand(command);
                    break;

                case GameMode.NetworkClient:
                    // 网络客户端模式：发送到服务器
                    if (FrameSyncManager == null)
                    {
                        UnityEngine.Debug.LogError("[Game] 网络模式下FrameSyncManager未初始化！");
                        return;
                    }

                    FrameSyncManager.SubmitLocalCommand(command);
                    break;
            }
        }

        /// <summary>
        /// 开始追帧（断线重连后调用）
        /// </summary>
        /// <param name="catchUpCommands">需要追赶的帧命令（帧号 → 命令列表）</param>
        public void StartCatchUp(Dictionary<int, List<ICommand>> catchUpCommands)
        {
            if (Mode != GameMode.NetworkClient)
            {
                UnityEngine.Debug.LogWarning("[Game] 只有网络客户端模式才能追帧");
                return;
            }

            if (FrameSyncManager == null)
            {
                UnityEngine.Debug.LogError("[Game] 追帧失败：FrameSyncManager 未初始化");
                return;
            }

            if (catchUpCommands == null || catchUpCommands.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[Game] 没有需要追赶的帧");
                return;
            }

            // 批量确认所有帧
            FrameSyncManager.ConfirmFrames(catchUpCommands);

            // 进入追帧模式
            IsCatchingUp = true;

            int pendingFrames = FrameSyncManager.GetPendingFrameCount();
            UnityEngine.Debug.Log($"[Game] 开始追帧，当前帧: {World.Tick}, 目标帧: {World.Tick + pendingFrames}, 需追赶: {pendingFrames} 帧");
        }

        /// <summary>
        /// 停止追帧（追赶完成）
        /// </summary>
        private void StopCatchUp()
        {
            IsCatchingUp = false;
            
            UnityEngine.Debug.Log($"[Game] 追帧完成，当前帧: {World.Tick}");
            
            // TODO: 通知表现层重新同步所有视图
            // 因为追帧期间可能禁用了渲染
        }

        /// <summary>
        /// 获取追帧进度（0-1）
        /// </summary>
        public float GetCatchUpProgress()
        {
            if (!IsCatchingUp || FrameSyncManager == null)
                return 1f;

            int pending = FrameSyncManager.GetPendingFrameCount();
            if (pending == 0)
                return 1f;

            // 假设开始时有 totalFrames，现在还剩 pending
            // progress = 1 - (pending / totalFrames)
            // 简化：只返回剩余帧数的倒数
            return System.Math.Max(0f, 1f - pending / 100f);
        }

        /// <summary>
        /// 关闭游戏
        /// </summary>
        public void Shutdown()
        {
            World?.Shutdown();
            UnityEngine.Debug.Log("[Game] 已关闭");
        }
    }
}
