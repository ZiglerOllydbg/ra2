using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Sync.Command;

namespace ZLockstep.Sync
{
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
    ///   // Unity中
    ///   var game = new Game();
    ///   game.Init();
    ///   
    ///   // 服务器中
    ///   var game = new Game();
    ///   game.Init();
    ///   while(running) {
    ///       game.Update();
    ///   }
    /// </summary>
    public class Game
    {
        /// <summary>
        /// 唯一的游戏逻辑世界
        /// </summary>
        public zWorld World { get; private set; }

        private readonly int _frameRate;
        private readonly int _localPlayerId;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="frameRate">逻辑帧率（默认20帧/秒）</param>
        /// <param name="localPlayerId">本地玩家ID（默认0）</param>
        public Game(int frameRate = 20, int localPlayerId = 0)
        {
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

            // 注册游戏系统
            RegisterSystems();

            UnityEngine.Debug.Log($"[Game] 初始化完成 - 帧率:{_frameRate} 玩家ID:{_localPlayerId}");
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
        /// </summary>
        public void Update()
        {
            // 清空上一帧的事件（修复时序问题）
            World.EventManager.Clear();

            // 更新唯一的 zWorld
            World.Update();
        }

        /// <summary>
        /// 提交命令（统一入口）
        /// </summary>
        public void SubmitCommand(ICommand command)
        {
            command.PlayerId = _localPlayerId;
            World.CommandManager.SubmitCommand(command);
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
