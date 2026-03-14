using ZFrame;

/// <summary>
/// 更新用户信息事件
/// 使用方法：
/// Frame.DispatchEvent(new UpdateUserInfoEvent("Nickname"));
/// </summary>
public class UpdateUserInfoEvent : ModuleEvent
{
    public string Nickname;

    public UpdateUserInfoEvent(string nickname) : base()
    {
        Nickname = nickname;
    }
}