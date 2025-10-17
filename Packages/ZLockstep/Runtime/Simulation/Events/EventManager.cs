using System;
using System.Collections.Generic;
using System.Linq;

namespace ZLockstep.Simulation.Events
{
    /// <summary>
    /// 事件管理器
    /// 采用"拉"模式以保证确定性。System负责在自己的更新周期内主动查询和处理事件。
    /// </summary>
    public class EventManager
    {
        // Key: Event Type
        // Value: Queue of event instances
        private readonly Dictionary<Type, Queue<IEvent>> _eventQueues = new Dictionary<Type, Queue<IEvent>>();
        private static readonly Queue<IEvent> _emptyQueue = new Queue<IEvent>();
        
        /// <summary>
        /// 发布一个事件。事件会被加入对应类型的队列中，等待System来处理。
        /// </summary>
        public void Publish<T>(T gameEvent) where T : IEvent
        {
            var eventType = typeof(T);
            if (!_eventQueues.ContainsKey(eventType))
            {
                _eventQueues[eventType] = new Queue<IEvent>();
            }
            _eventQueues[eventType].Enqueue(gameEvent);
        }

        /// <summary>
        /// 获取指定类型的所有事件。
        /// System应在自己的Update方法中调用此方法来处理事件。
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <returns>只读的事件队列</returns>
        public Queue<IEvent> GetEvents<T>() where T : IEvent
        {
            var eventType = typeof(T);
            if (_eventQueues.TryGetValue(eventType, out var queue))
            {
                return queue;
            }
            return _emptyQueue;
        }

        /// <summary>
        /// 在每帧结束时调用，清空所有事件队列。
        /// </summary>
        public void Clear()
        {
            foreach (var queue in _eventQueues.Values)
            {
                queue.Clear();
            }
        }
    }
}
