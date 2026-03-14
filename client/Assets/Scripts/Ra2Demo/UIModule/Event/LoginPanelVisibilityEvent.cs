using ZFrame;

/// <summary>
/// 登录面板显示控制事件（支持显示/隐藏参数）
/// </summary>
public class LoginPanelVisibilityEvent : ModuleEvent
{
    public bool IsVisible { get; private set; }

    public LoginPanelVisibilityEvent(bool isVisible) : base()
    {
        IsVisible = isVisible;
    }
}