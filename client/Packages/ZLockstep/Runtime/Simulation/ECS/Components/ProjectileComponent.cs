using zUnity;

namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 弹道组件
    /// 用于子弹、炮弹等飞行物
    /// </summary>
    public struct ProjectileComponent : IComponent
    {
        /// <summary>
        /// 发射者Entity ID
        /// </summary>
        public int SourceEntityId;

        /// <summary>
        /// 目标Entity ID（-1表示无目标）
        /// </summary>
        public int TargetEntityId;

        /// <summary>
        /// 伤害值
        /// </summary>
        public zfloat Damage;

        /// <summary>
        /// 飞行速度（米/秒）
        /// </summary>
        public zfloat Speed;

        /// <summary>
        /// 目标位置（用于追踪）
        /// </summary>
        public zVector3 TargetPosition;

        /// <summary>
        /// 是否追踪目标（true=追踪型导弹，false=直线飞行）
        /// </summary>
        public bool IsHoming;

        /// <summary>
        /// 发射者的阵营ID（用于判断敌我）
        /// </summary>
        public int SourceCampId;

        /// <summary>
        /// 创建弹道组件
        /// </summary>
        public static ProjectileComponent Create(
            int sourceEntityId, 
            int targetEntityId, 
            zfloat damage, 
            zfloat speed, 
            zVector3 targetPosition,
            bool isHoming,
            int sourceCampId)
        {
            return new ProjectileComponent
            {
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                Damage = damage,
                Speed = speed,
                TargetPosition = targetPosition,
                IsHoming = isHoming,
                SourceCampId = sourceCampId
            };
        }
    }
}

