using UnityEngine;
using UnityEngine.UI;
using ZFrame;

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
    public MainPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        // 在这里可以获取面板中的UI组件引用
    }

    protected override void AddEvent()
    {
        base.AddEvent();
        // 添加事件监听
    }

    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        // 移除事件监听
    }
}