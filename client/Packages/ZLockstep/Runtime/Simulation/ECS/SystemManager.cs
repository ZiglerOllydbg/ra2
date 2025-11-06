using System.Collections.Generic;

namespace ZLockstep.Simulation.ECS
{
    /// <summary>
    /// 系统管理器
    /// 负责注册、管理和以确定性顺序更新所有系统。
    /// </summary>
    public class SystemManager
    {
        private readonly List<ISystem> _systems = new List<ISystem>();
        private readonly List<ISystem> _presentationSystems = new List<ISystem>();
        private readonly zWorld _world;

        public SystemManager(zWorld world)
        {
            _world = world;
        }

        /// <summary>
        //  注册一个新的系统。
        //  注册的顺序将决定其更新的顺序。
        /// </summary>
        /// <param name="system">要注册的系统</param>
        public void RegisterSystem(ISystem system)
        {
            system.SetWorld(_world);
            
            // 检查是否为表现系统
            if (system is PresentationBaseSystem presentationSystem)
            {
                _presentationSystems.Add(presentationSystem);
            }
            else
            {
                _systems.Add(system);
            }
        }

        /// <summary>
        /// 按照注册顺序，更新所有系统。
        /// 普通系统优先更新，然后更新表现系统
        /// </summary>
        public void UpdateAll()
        {
            // 更新普通系统
            foreach (var system in _systems)
            {
                system.Update();
            }
            
            // 更新表现系统
            foreach (var system in _presentationSystems)
            {
                system.Update();
            }
        }
    }
}