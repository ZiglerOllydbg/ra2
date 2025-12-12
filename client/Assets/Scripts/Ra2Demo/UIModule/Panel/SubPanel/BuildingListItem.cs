using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

/// <summary>
/// 主面板列表项 - 继承自BaseListItem
/// </summary>
public class BuildingListItem : BaseListItem
{
    private Image iconImage;
    private TMP_Text buildingNameText;
    private Button selectBtn;
    
    /// <summary>
    /// 绑定的数据
    /// </summary>
    public BuildItemData ItemData { get; private set; }
    
    /// <summary>
    /// 选中回调（可选，用于通知父面板）
    /// </summary>
    public Action<BuildingListItem> OnItemSelect;
    
    public BuildingListItem(GameObject __target) : base(__target)
    {
        // 获取子组件引用
        iconImage = __target.transform.Find("Image")?.GetComponent<Image>();
        buildingNameText = __target.transform.Find("BuildingName")?.GetComponent<TMP_Text>();
        selectBtn = __target.transform.Find("Button")?.GetComponent<Button>();
        selectBtn?.onClick.AddListener(OnSelectClick);
    }
    
    /// <summary>
    /// 绑定数据并刷新UI
    /// </summary>
    public void SetData(BuildItemData data)
    {
        ItemData = data;
        UpdateData();
    }
    
    /// <summary>
    /// 刷新数据显示
    /// </summary>
    public override void UpdateData()
    {
        if (buildingNameText != null && ItemData != null)
            buildingNameText.text = ItemData.Name;
    }
    
    private void OnSelectClick()
    {
        // 触发选中回调
        OnItemSelect?.Invoke(this);
    }
    
    /// <summary>
    /// 销毁时调用
    /// </summary>
    public override void Destroy()
    {
        selectBtn?.onClick.RemoveListener(OnSelectClick);
        OnItemSelect = null;
        base.Destroy();
    }
}