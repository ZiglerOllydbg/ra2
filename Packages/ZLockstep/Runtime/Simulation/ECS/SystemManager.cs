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
            _systems.Add(system);
            system.SetWorld(_world);
        }

        /// <summary>
        /// 按照注册顺序，更新所有系统。
        /// </summary>
        public void UpdateAll()
        {
            foreach (var system in _systems)
            {
                system.Update();
            }
        }
    }
}
