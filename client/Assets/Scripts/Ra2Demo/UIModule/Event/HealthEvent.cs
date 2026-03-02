using ZFrame;

public class HealthEvent : ModuleEvent
{
    public int Id { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsSelf { get; private set; }
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }

    public HealthEvent(int id, bool isSelf, int currentHealth, int maxHealth, bool isVisible = true) : base()
    {
        Id = id;
        IsVisible = isVisible;
        IsSelf = isSelf;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
    }
}