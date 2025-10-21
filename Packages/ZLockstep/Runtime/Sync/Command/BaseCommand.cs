using ZLockstep.Simulation;

namespace ZLockstep.Sync.Command
{
    /// <summary>
    /// 命令基类
    /// 提供通用的属性实现
    /// </summary>
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

        public virtual byte[] Serialize()
        {
            // 默认实现（子类可以重写）
            // TODO: 实现序列化逻辑
            return new byte[0];
        }

        public virtual void Deserialize(byte[] data)
        {
            // 默认实现（子类可以重写）
            // TODO: 实现反序列化逻辑
        }
    }
}

