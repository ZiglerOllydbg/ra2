using ZFrame;

public class HealthEvent : ModuleEvent
{
    public int Id { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsSelf { get; private set; }

    public HealthEvent(int id, bool isSelf, bool isVisible = true) : base()
    {
        Id = id;
        IsVisible = isVisible;
        IsSelf = isSelf;
    }

}

/// <summary>
/// 血条显示设置变更事件
/// </summary>
public class HealthBarSettingChangedEvent : ModuleEvent
{
    public bool ShowAlways { get; private set; }

    public HealthBarSettingChangedEvent(bool showAlways) : base()
    {
        ShowAlways = showAlways;
    }
}
