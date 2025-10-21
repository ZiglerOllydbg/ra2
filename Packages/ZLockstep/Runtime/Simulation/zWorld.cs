using System.Collections;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.Events;
using ZLockstep.Sync.Command;

namespace ZLockstep.Simulation
{
    /// <summary>
    /// 游戏世界，所有游戏状态的顶层容器和管理者。
    /// 仿真循环的入口点。
    /// </summary>
    public class zWorld
    {
        // 管理器引用
        public EntityManager EntityManager { get; private set; }
        public ComponentManager ComponentManager { get; private set; }
        public SystemManager SystemManager { get; private set; }
        public EventManager ViewEventManager { get; private set; }
        public EventManager LogicEventManager { get; private set; }
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
            ViewEventManager = new EventManager();
            LogicEventManager = new EventManager();
            CommandManager = new CommandManager(this);

            // TODO: 在这里注册所有的游戏系统 (e.g., SystemManager.RegisterSystem(new MovementSystem());)
        }

        /// <summary>
        /// 驱动世界前进一个逻辑帧
        /// </summary>
        public void Update()
        {
            // 1. 推进时间
            TimeManager.Advance();

            // 2. 执行当前帧的所有命令
            CommandManager.ExecuteFrame();

            // 3. 执行 System
            SystemManager.UpdateAll();

            // 4. 在帧末尾清空事件队列
            LogicEventManager.Clear();
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
