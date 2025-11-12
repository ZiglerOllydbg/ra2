using ZFrame;

/// <summary>
/// Demo面板 - 示例如何在业务层配置路径、名称和深度类型
/// </summary>
[UIModel(
    panelID = "DemoPanel",
    panelPath = "DemoPanel",  // 业务层配置：Prefab资源路径
    panelName = "Demo面板",           // 业务层配置：面板显示名称
    panelUIDepthType = ClientUIDepthTypeID.GameMid  // 业务层配置：UI深度类型
)]
public class DemoPanel : BasePanel
{
    public DemoPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) : base(_processor, _modelData, _disableNew)
    {
    }
}
