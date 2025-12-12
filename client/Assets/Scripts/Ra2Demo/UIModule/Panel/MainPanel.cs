using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using TMPro;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS;

/// <summary>
/// 主面板 - 游戏主界面
/// </summary>
[UIModel(
    panelID = "MainPanel",
    panelPath = "MainPanel",
    panelName = "主面板",
    panelUIDepthType = ClientUIDepthTypeID.GameTop
)]
public class MainPanel : BasePanel
{
    // 货币显示文本
    private TMP_Text moneyText;
    // 电力显示文本
    private TMP_Text powerText;
    
    // 按钮引用
    private Button selectBtn;
    private Button buildBtn;
    private Button settingBtn;
    
    // 子面板控制器
    private MainBuildingSubPanel subPanel;

    public MainPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {

    }

    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        // 获取面板中的UI组件引用
        moneyText = PanelObject.transform.Find("Info/Money/Value")?.GetComponent<TMP_Text>();
        powerText = PanelObject.transform.Find("Info/Power/Value")?.GetComponent<TMP_Text>();
        
        // 获取按钮组件
        selectBtn = PanelObject.transform.Find("SelectBtn")?.GetComponent<Button>();
        buildBtn = PanelObject.transform.Find("BuildBtn")?.GetComponent<Button>();
        settingBtn = PanelObject.transform.Find("SettingBtn")?.GetComponent<Button>();
        
        // 初始化子面板
        subPanel = new MainBuildingSubPanel(PanelObject.transform);
        subPanel.OnCloseClick = OnSubPanelClosed;
        subPanel.Hide();
    }

    protected override void AddEvent()
    {
        base.AddEvent();
        
        // 添加按钮事件监听
        if (selectBtn != null)
        {
            selectBtn.onClick.AddListener(OnSelectButtonClick);
        }
        
        if (buildBtn != null)
        {
            buildBtn.onClick.AddListener(OnBuildButtonClick);
        }
        
        if (settingBtn != null)
        {
            settingBtn.onClick.AddListener(OnSettingButtonClick);
        }
    }

    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        
        // 移除按钮事件监听
        if (selectBtn != null)
        {
            selectBtn.onClick.RemoveListener(OnSelectButtonClick);
        }
        
        if (buildBtn != null)
        {
            buildBtn.onClick.RemoveListener(OnBuildButtonClick);
        }
        
        if (settingBtn != null)
        {
            settingBtn.onClick.RemoveListener(OnSettingButtonClick);
        }
    }
    
    protected override void OnBecameInvisible()
    {
        base.OnBecameInvisible();
        
        // 清理子面板
        subPanel?.Destroy();
        subPanel = null;
    }

    /// <summary>
    /// 设置金钱数值显示
    /// </summary>
    /// <param name="money">金钱数量</param>
    public void SetMoney(int money)
    {
        if (moneyText != null)
        {
            moneyText.text = $"{money}";
        }
    }

    /// <summary>
    /// 设置电力数值显示
    /// </summary>
    /// <param name="power">当前电力</param>
    public void SetPower(int power)
    {
        if (powerText != null)
        {
            powerText.text = $"{power}";
        }
    }

    /// <summary>
    /// 获取金钱显示文本组件
    /// </summary>
    /// <returns>TMP_Text组件</returns>
    public TMP_Text GetMoneyText()
    {
        return moneyText;
    }

    /// <summary>
    /// 获取电力显示文本组件
    /// </summary>
    /// <returns>TMP_Text组件</returns>
    public TMP_Text GetPowerText()
    {
        return powerText;
    }

    /// <summary>
    /// 设置金钱显示文本组件
    /// </summary>
    /// <param name="text">TMP_Text组件</param>
    public void SetMoneyText(TMP_Text text)
    {
        moneyText = text;
    }

    /// <summary>
    /// 设置电力显示文本组件
    /// </summary>
    /// <param name="text">TMP_Text组件</param>
    public void SetPowerText(TMP_Text text)
    {
        powerText = text;
    }
    
    #region 子面板控制
    
    /// <summary>
    /// 显示子面板并传入数据
    /// </summary>
    public void ShowSubPanel(SubPanelData data)
    {
        subPanel?.Show(data);
    }
    
    /// <summary>
    /// 隐藏子面板
    /// </summary>
    public void HideSubPanel()
    {
        subPanel?.Hide();
    }
    
    /// <summary>
    /// 子面板关闭回调
    /// </summary>
    private void OnSubPanelClosed()
    {
        // 处理子面板关闭后的逻辑
        Debug.Log("子面板已关闭");
    }
    
    /// <summary>
    /// 刷新列表数据
    /// </summary>
    public void RefreshList(List<DemoItemData> dataList)
    {
        // 确保子面板已经显示
        if (subPanel != null)
        {
            // 这里可以根据实际需要决定是否自动显示子面板
            // ShowSubPanel(new SubPanelData { Title = "列表面板", Content = "这是一个列表" });
            // subPanel.RefreshList(dataList);
        }
    }
    
    #endregion
    
    /// <summary>
    /// Select按钮点击处理
    /// </summary>
    private void OnSelectButtonClick()
    {
        Debug.Log("Select按钮被点击了！");
    }
    
    /// <summary>
    /// Build按钮点击处理 - 打开子面板
    /// </summary>
    private void OnBuildButtonClick()
    {
        Debug.Log("Build按钮被点击了！");
        
        // 创建子面板数据
        var subPanelData = new SubPanelData 
        { 
            Title = "建造菜单", 
            Content = "请选择要建造的建筑" 
        };
        
        // 显示子面板
        ShowSubPanel(subPanelData);
    }
    
    /// <summary>
    /// Setting按钮点击处理
    /// </summary>
    private void OnSettingButtonClick()
    {
        Debug.Log("Setting按钮被点击了！");
    }
}