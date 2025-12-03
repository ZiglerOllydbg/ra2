using UnityEngine;
using UnityEngine.UI;
using ZFrame;

/// <summary>
/// Demo面板 - 示例如何在业务层配置路径、名称和深度类型
/// </summary>
[UIModel(
    panelID = "DemoPanel",
    panelPath = "DemoPanel",
    panelName = "Demo面板",
    panelUIDepthType = ClientUIDepthTypeID.GameMid
)]
public class DemoPanel : BasePanel
{
    // 1. 声明UI组件引用
    private Button myButton;
    
    public DemoPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    // 2. 在面板显示前获取组件引用
    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        
        // 从PanelObject获取按钮组件
        myButton = PanelObject.transform.Find("MyButton")?.GetComponent<Button>();
        // 或者如果按钮在根节点: myButton = PanelObject.GetComponentInChildren<Button>();
    }

    // 3. 在AddEvent中添加按钮事件（面板显示时自动调用）
    protected override void AddEvent()
    {
        base.AddEvent();
        
        if (myButton != null)
        {
            myButton.onClick.AddListener(OnMyButtonClick);
        }
    }

    // 4. 在RemoveEvent中移除按钮事件（面板关闭时自动调用）
    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        
        if (myButton != null)
        {
            myButton.onClick.RemoveListener(OnMyButtonClick);
        }
    }

    // 5. 按钮点击处理方法
    private void OnMyButtonClick()
    {
        // 处理按钮点击逻辑
        Debug.Log("按钮被点击了！");
    }
}