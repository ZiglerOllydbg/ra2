using Game.RA2.Client;
using UnityEngine;
using UnityEngine.UI;
using ZFrame;

/// <summary>
/// Demo面板 - 示例如何在业务层配置路径、名称和深度类型
/// </summary>
[UIModel(
    panelID = "MatchPanel",
    panelPath = "MatchPanel",
    panelName = "Match面板",
    panelUIDepthType = ClientUIDepthTypeID.GameMid
)]
public class MatchPanel : BasePanel
{
    // 1. 声明UI组件引用
    private Button _soloButton;
    private Button _duoButton;
    private Button _quadButton;
    private Toggle _useLocalNetToggle;
    private Transform _matchGroup;
    private Transform _matchingGroup;
    
    public MatchPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    // 2. 在面板显示前获取组件引用
    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        
        // 获取Match按钮组和Matching组容器
        _matchGroup = PanelObject.transform.Find("Match");
        _matchingGroup = PanelObject.transform.Find("Matching");
        
        // 从PanelObject获取按钮组件
        _soloButton = PanelObject.transform.Find("Match/SOLO")?.GetComponent<Button>();
        _duoButton = PanelObject.transform.Find("Match/DUO")?.GetComponent<Button>();
        _quadButton = PanelObject.transform.Find("Match/QUAD")?.GetComponent<Button>();
        
        // 获取UseLocalNet Toggle组件
        _useLocalNetToggle = PanelObject.transform.Find("Match/UseLocalNet")?.GetComponent<Toggle>();
        
        // 加载保存的UseLocalNet选项
        LoadUseLocalNetOption();
    }

    // 3. 在AddEvent中添加按钮事件（面板显示时自动调用）
    protected override void AddEvent()
    {
        base.AddEvent();
        
        if (_soloButton != null)
        {
            _soloButton.onClick.AddListener(OnSoloButtonClick);
        }

        if (_duoButton != null)
        {
            _duoButton.onClick.AddListener(OnDuoButtonClick);
        }

        if (_quadButton != null)
        {
            _quadButton.onClick.AddListener(OnQuadButtonClick);
        }
        
        // 为UseLocalNet Toggle添加值变化监听
        if (_useLocalNetToggle != null)
        {
            _useLocalNetToggle.onValueChanged.AddListener(OnUseLocalNetValueChanged);
        }
    }

    // 4. 在RemoveEvent中移除按钮事件（面板关闭时自动调用）
    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        
        if (_soloButton != null)
        {
            _soloButton.onClick.RemoveListener(OnSoloButtonClick);
        }

        if (_duoButton != null)
        {
            _duoButton.onClick.RemoveListener(OnDuoButtonClick);
        }

        if (_quadButton != null)
        {
            _quadButton.onClick.RemoveListener(OnQuadButtonClick);
        }
        
        // 移除UseLocalNet Toggle的值变化监听
        if (_useLocalNetToggle != null)
        {
            _useLocalNetToggle.onValueChanged.RemoveListener(OnUseLocalNetValueChanged);
        }
    }

    // 5. 按钮点击处理方法
    private void OnSoloButtonClick()
    {
        bool isLocalNet = GetIsLocalNet();
        NetworkManager.Instance.ConnectToServer(RoomType.SOLO, isLocalNet);

        HideButtons();
    }

    private bool GetIsLocalNet()
    {
        return _useLocalNetToggle?.isOn ?? false;
    }

    private void OnDuoButtonClick()
    {
        bool isLocalNet = GetIsLocalNet();
        NetworkManager.Instance.ConnectToServer(RoomType.DUO, isLocalNet);
        HideButtons();
    }

    private void OnQuadButtonClick()
    {
        bool isLocalNet = GetIsLocalNet();
        NetworkManager.Instance.ConnectToServer(RoomType.QUAD, isLocalNet);
        HideButtons();
    }

    // UseLocalNet Toggle值变化处理方法
    private void OnUseLocalNetValueChanged(bool value)
    {
        Debug.Log($"UseLocalNet状态变化为: {value}");
        SaveUseLocalNetOption(value);
    }

    // 保存UseLocalNet选项
    private void SaveUseLocalNetOption(bool value)
    {
        PlayerPrefs.SetInt("UseLocalNet", value ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    // 加载保存的UseLocalNet选项
    private void LoadUseLocalNetOption()
    {
        if (_useLocalNetToggle != null)
        {
            bool savedValue = PlayerPrefs.GetInt("UseLocalNet", 0) == 1;
            _useLocalNetToggle.isOn = savedValue;
        }
    }

    // 隐藏所有按钮并显示匹配中界面
    private void HideButtons()
    {
        if (_matchGroup != null)
        {
            _matchGroup.gameObject.SetActive(false);
        }
        
        if (_matchingGroup != null)
        {
            _matchingGroup.gameObject.SetActive(true);
        }
    }

    public void HideMatchingGroup()
    {
        if (_matchingGroup != null)
        {
            _matchingGroup.gameObject.SetActive(false);
        }
    }
}