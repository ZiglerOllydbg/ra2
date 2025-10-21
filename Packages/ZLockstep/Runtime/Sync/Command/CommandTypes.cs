namespace ZLockstep.Sync.Command
{
    /// <summary>
    /// 命令类型常量定义
    /// 用于序列化和网络传输时识别命令类型
    /// </summary>
    public static class CommandTypes
    {
        public const int None = 0;
        
        // 单位相关命令 (1-99)
        public const int CreateUnit = 1;
        public const int Move = 2;
        public const int Attack = 3;
        public const int Stop = 4;
        public const int Patrol = 5;
        
        // 建筑相关命令 (100-199)
        public const int BuildStructure = 100;
        public const int SellStructure = 101;
        public const int RepairStructure = 102;
        
        // 生产相关命令 (200-299)
        public const int ProduceUnit = 200;
        public const int CancelProduction = 201;
        
        // 资源相关命令 (300-399)
        public const int GatherResource = 300;
        
        // AI相关命令 (1000+)
        public const int AIDecision = 1000;
    }
}

