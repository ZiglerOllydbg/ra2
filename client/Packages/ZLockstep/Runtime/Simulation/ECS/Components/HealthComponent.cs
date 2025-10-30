namespace ZLockstep.Simulation.ECS.Components
{
    /// <summary>
    /// 生命值组件
    /// 用于单位和建筑的生命值管理
    /// </summary>
    public struct HealthComponent : IComponent
    {
        public zfloat MaxHealth;
        public zfloat CurrentHealth;

        public HealthComponent(zfloat maxHealth)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }

        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive => CurrentHealth > zfloat.Zero;

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead => CurrentHealth <= zfloat.Zero;

        /// <summary>
        /// 生命值百分比 (0~1)
        /// </summary>
        public zfloat HealthPercent
        {
            get
            {
                if (MaxHealth <= zfloat.Zero)
                    return zfloat.Zero;
                return CurrentHealth / MaxHealth;
            }
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(zfloat damage)
        {
            CurrentHealth -= damage;
            if (CurrentHealth < zfloat.Zero)
                CurrentHealth = zfloat.Zero;
        }

        /// <summary>
        /// 恢复生命
        /// </summary>
        public void Heal(zfloat amount)
        {
            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth)
                CurrentHealth = MaxHealth;
        }
    }
}

