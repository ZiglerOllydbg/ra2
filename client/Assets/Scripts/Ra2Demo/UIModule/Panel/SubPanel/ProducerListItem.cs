using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

/// <summary>
/// 生产面板列表项 - 继承自BaseListItem
/// </summary>
public class ProducerListItem : BaseListItem
{
    private Image iconImage;
    private TMP_Text producerNameText;
    private TMP_Text factoryNameText;
    private Button selectBtn;
    
    /// <summary>
    /// 绑定的数据
    /// </summary>
    public ProducerItemData ItemData { get; private set; }
    
    /// <summary>
    /// 选中回调（可选，用于通知父面板）
    /// </summary>
    public Action<ProducerListItem> OnItemSelect;
    
    public ProducerListItem(GameObject __target) : base(__target)
    {
        // 获取子组件引用
        iconImage = __target.transform.Find("Image")?.GetComponent<Image>();
        producerNameText = __target.transform.Find("ProducerName")?.GetComponent<TMP_Text>();
        factoryNameText = __target.transform.Find("FactoryName")?.GetComponent<TMP_Text>();
        selectBtn = __target.transform.Find("Button")?.GetComponent<Button>();
        selectBtn?.onClick.AddListener(OnSelectClick);
    }
    
    /// <summary>
    /// 绑定数据并刷新UI
    /// </summary>
    public void SetData(ProducerItemData data)
    {
        ItemData = data;
        UpdateData();
    }
    
    /// <summary>
    /// 刷新数据显示
    /// </summary>
    public override void UpdateData()
    {
        if (producerNameText != null && ItemData != null)
            producerNameText.text = ItemData.Name;
            
        if (factoryNameText != null && ItemData != null)
            factoryNameText.text = ItemData.BelongFactory.ToString();
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