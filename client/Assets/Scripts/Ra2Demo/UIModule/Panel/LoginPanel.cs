using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using TMPro;

/// <summary>
/// 登录面板 - 简化的登录界面
/// </summary>
[UIModel(
    panelID = "LoginPanel",
    panelPath = "LoginPanel",
    panelName = "登录面板",
    panelUIDepthType = ClientUIDepthTypeID.GameMid
)]
public class LoginPanel : BasePanel
{
    // 1. 声明 UI 组件引用
    private Button _loginButton;
    
    public LoginPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    // 2. 在面板显示前获取组件引用
    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        
        // 获取 Login 按钮组件
        _loginButton = PanelObject.transform.Find("Login/LoginBtn")?.GetComponent<Button>();
        
        Debug.Log("[LoginPanel] 面板已显示");
    }

    // 3. 在 AddEvent 中添加按钮事件（面板显示时自动调用）
    protected override void AddEvent()
    {
        base.AddEvent();
        
        // 为 Login 按钮添加点击事件监听
        if (_loginButton != null)
        {
            _loginButton.onClick.AddListener(OnLoginButtonClick);
            Debug.Log("[LoginPanel] 登录按钮事件已绑定");
        }
        else
        {
            Debug.LogWarning("[LoginPanel] 未找到登录按钮组件");
        }
    }

    // 4. 在 RemoveEvent 中移除按钮事件（面板关闭时自动调用）
    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        
        // 移除 Login 按钮的点击事件监听
        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(OnLoginButtonClick);
            Debug.Log("[LoginPanel] 登录按钮事件已移除");
        }
    }

    // 5. 在面板隐藏时清理资源
    protected override void OnBecameInvisible()
    {
        base.OnBecameInvisible();
        Debug.Log("[LoginPanel] 面板已隐藏");
    }
    
    // Login 按钮点击处理方法
    private void OnLoginButtonClick()
    {
        Debug.Log("[LoginPanel] 登录按钮被点击");
        
        // TODO: 实现登录逻辑
        // 示例：触发登录事件或调用登录接口
        if (Application.isEditor)
        {
            string nickName = "Unity用户";
            string avatarUrl = "https://thirdwx.qlogo.cn/mmopen/vi_32/Q0j4TwGTfTLXlC8Ynp4rPicm4icSMDic9cXJzfS4abSzRdWMraKrO8o6kiap7EsjPEXL8jiaPphXoOmLKr5QPWDMNBQ/132";

            Frame.DispatchEvent(new LoginEvent(nickName, avatarUrl));
        }
        else
        {
            // 微信环境登录
            LoginWX loginWX = Object.FindObjectOfType<LoginWX>();
            if (loginWX != null)
            {
                loginWX.LoaderWXMess();
            }
            else 
            {
                Debug.LogError("[MatchPanel] 未找到 LoginWX 组件");
            }
        }
    }
}
