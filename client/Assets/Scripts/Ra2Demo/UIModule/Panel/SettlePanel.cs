using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using TMPro;

/// <summary>
/// 结算面板 - 显示游戏胜负结果
/// </summary>
[UIModel(
    panelID = "SettlePanel",
    panelPath = "SettlePanel",
    panelName = "结算面板",
    panelUIDepthType = ClientUIDepthTypeID.GameTop
)]
public class SettlePanel : BasePanel
{
    // 胜负结果显示文本
    private TMP_Text resultText;
    
    // 按钮引用
    private Button okBtn;
    
    public SettlePanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        
        // 获取面板中的UI组件引用
        resultText = PanelObject.transform.Find("Result/Text")?.GetComponent<TMP_Text>();
        okBtn = PanelObject.transform.Find("Result/OKBtn")?.GetComponent<Button>();
    }

    protected override void AddEvent()
    {
        base.AddEvent();
        
        // 添加按钮事件监听
        if (okBtn != null)
        {
            okBtn.onClick.AddListener(OnConfirmButtonClick);
        }
    }

    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        
        // 移除按钮事件监听
        if (okBtn != null)
        {
            okBtn.onClick.RemoveListener(OnConfirmButtonClick);
        }
    }
    
    /// <summary>
    /// 设置游戏结果
    /// </summary>
    /// <param name="isVictory">是否胜利</param>
    public void SetResult(bool isVictory)
    {
        if (resultText != null)
        {
            resultText.text = isVictory ? "胜利!" : "失败!";
            resultText.color = isVictory ? Color.green : Color.red;
        }
    }
    
    /// <summary>
    /// 确认按钮点击处理
    /// </summary>
    private void OnConfirmButtonClick()
    {
        Close();
        // 可以在这里添加返回主菜单或其他操作
        Frame.DispatchEvent(new RestartGameEvent());
    }
}