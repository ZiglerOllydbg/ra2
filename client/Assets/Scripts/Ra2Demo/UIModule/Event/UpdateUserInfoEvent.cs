using ZFrame;

/// <summary>
/// 更新用户信息事件
/// 使用方法：
/// Frame.DispatchEvent(new UpdateUserInfoEvent("Nickname"));
/// </summary>
public class UpdateUserInfoEvent : ModuleEvent
{
    public string Nickname;

    public string AvatarUrl;

    public UpdateUserInfoEvent(string nickname, string avatarUrl) : base()
    {
        Nickname = nickname;
        AvatarUrl = avatarUrl;
    }
}