using System;
using Newtonsoft.Json.Linq;
using ZLockstep.Simulation;

namespace ZLockstep.Sync.Command
{
    /// <summary>
    /// 命令基类
    /// 提供通用的属性实现
    /// </summary>
    [Serializable]
    public abstract class BaseCommand : ICommand
    {
        public abstract int CommandType { get; }
        
        public int PlayerId { get; set; }
        
        public CommandSource Source { get; set; }
        
        public int ExecuteFrame { get; set; }

        protected BaseCommand(int playerId)
        {
            PlayerId = playerId;
            Source = CommandSource.Local;
            ExecuteFrame = -1; // -1 表示立即执行
        }

        public abstract void Execute(zWorld world);
    }
}

