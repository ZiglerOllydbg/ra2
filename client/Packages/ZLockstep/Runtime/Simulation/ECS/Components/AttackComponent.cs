namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 攻击组件
    /// 用于具有攻击能力的单位
    /// </summary>
    public struct AttackComponent : IComponent
    {
        /// <summary>
        /// 攻击伤害
        /// </summary>
        public zfloat Damage;

        /// <summary>
        /// 攻击范围
        /// </summary>
        public zfloat Range;

        /// <summary>
        /// 攻击间隔（秒）
        /// </summary>
        public zfloat AttackInterval;

        /// <summary>
        /// 距离上次攻击的时间
        /// </summary>
        public zfloat TimeSinceLastAttack;

        /// <summary>
        /// 当前目标实体ID（-1表示无目标）
        /// </summary>
        public int TargetEntityId;

        /// <summary>
        /// 能否攻击
        /// </summary>
        public bool CanAttack => TimeSinceLastAttack >= AttackInterval;

        /// <summary>
        /// 是否有目标
        /// </summary>
        public bool HasTarget => TargetEntityId >= 0;

        public static AttackComponent CreateDefault()
        {
            return new AttackComponent
            {
                Damage = (zfloat)10.0f,
                Range = (zfloat)5.0f,
                AttackInterval = (zfloat)1.0f,
                TimeSinceLastAttack = zfloat.Zero,
                TargetEntityId = -1
            };
        }
    }
}

