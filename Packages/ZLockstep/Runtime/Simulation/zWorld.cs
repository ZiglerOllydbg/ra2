using System.Collections;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.Events;
using ZLockstep.Sync.Command;

namespace ZLockstep.Simulation
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// 【第1层：游戏逻辑核心层】zWorld
    /// ═══════════════════════════════════════════════════════════════
    /// 
    /// 职责定位：
    /// - 游戏状态的容器：管理所有游戏实体、组件和系统
    /// - 纯逻辑计算：不关心如何被驱动，只负责状态推进
    /// - 确定性保证：所有计算使用确定性数学（zfloat/zVector3）
    /// - 跨平台核心：可在Unity、服务器、测试环境运行
    /// 
    /// 设计原则（奥卡姆剃刀）：
    /// - 不知道谁在驱动它（Game/GameWorldBridge/测试代码）
    /// - 不知道游戏模式（单机/网络/回放）
    /// - 不知道平台（Unity/服务器/命令行）
    /// - 只管理游戏状态和逻辑推进
    /// 
    /// 对外接口：
    /// - Init(frameRate)：初始化世界
    /// - Update()：推进一个逻辑帧
    /// - Shutdown()：清理资源
    /// 
    /// 使用示例：
    ///   var world = new zWorld();
    ///   world.Init(20);  // 20帧/秒
    ///   while(running) {
    ///       world.Update();  // 每帧调用
    ///   }
    /// </summary>
    public class zWorld
    {
        // 管理器引用
        public EntityManager EntityManager { get; private set; }
        public ComponentManager ComponentManager { get; private set; }
        public SystemManager SystemManager { get; private set; }
        public EventManager EventManager { get; private set; }
        public TimeManager TimeManager { get; private set; }
        public CommandManager CommandManager { get; private set; }

        /// <summary>
        /// 当前的逻辑帧数
        /// </summary>
        public int Tick => TimeManager?.Tick ?? 0;

        /// <summary>
        /// 初始化游戏世界
        /// </summary>
        /// <param name="frameRate">逻辑帧率</param>
        public void Init(int frameRate)
        {
            // 初始化各个管理器
            TimeManager = new TimeManager();
            TimeManager.Init(frameRate);

            ComponentManager = new ComponentManager();
            EntityManager = new EntityManager();
            EntityManager.Init(ComponentManager);

            SystemManager = new SystemManager(this);
            EventManager = new EventManager();
            CommandManager = new CommandManager(this);

            // TODO: 在这里注册所有的游戏系统 (e.g., SystemManager.RegisterSystem(new MovementSystem());)
        }

        /// <summary>
        /// 驱动世界前进一个逻辑帧
        /// 这是游戏逻辑的核心循环
        /// </summary>
        public void Update()
        {
            // 1. 推进时间（Tick++）
            TimeManager.Advance();

            // 2. 执行当前帧的所有命令（Command → 修改ECS状态）
            CommandManager.ExecuteFrame();

            // 3. 执行所有System（System读取/修改ECS状态，发布Event）
            SystemManager.UpdateAll();

            // 注意：不在这里清空事件！
            // 事件需要在下一帧开始前清空，让表现层有机会处理
        }

        /// <summary>
        /// 销毁世界，清理资源
        /// </summary>
        public void Shutdown()
        {
            // TODO: 调用各个管理器的清理方法
        }
    }
}
