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
    private Button addBtn;
    private Button subBtn;
    
    /// <summary>
    /// 绑定的数据
    /// </summary>
    public ProducerItemData ItemData { get; private set; }
    
    /// <summary>
    /// 添加回调（用于通知父面板增加单位生产）
    /// </summary>
    public Action<ProducerListItem> OnItemAdd;
    
    /// <summary>
    /// 减少回调（用于通知父面板减少单位生产）
    /// </summary>
    public Action<ProducerListItem> OnItemSub;
    
    public ProducerListItem(GameObject __target) : base(__target)
    {
        // 获取子组件引用
        iconImage = __target.transform.Find("Image")?.GetComponent<Image>();
        producerNameText = __target.transform.Find("ProducerName")?.GetComponent<TMP_Text>();
        factoryNameText = __target.transform.Find("FactoryName")?.GetComponent<TMP_Text>();
        addBtn = __target.transform.Find("AddBtn")?.GetComponent<Button>();
        subBtn = __target.transform.Find("SubBtn")?.GetComponent<Button>();
        addBtn?.onClick.AddListener(OnAddClick);
        subBtn?.onClick.AddListener(OnSubClick);
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
    
    private void OnAddClick()
    {
        // 触发添加回调
        OnItemAdd?.Invoke(this);
    }
    
    private void OnSubClick()
    {
        // 触发减少回调
        OnItemSub?.Invoke(this);
    }
    
    /// <summary>
    /// 销毁时调用
    /// </summary>
    public override void Destroy()
    {
        addBtn?.onClick.RemoveListener(OnAddClick);
        subBtn?.onClick.RemoveListener(OnSubClick);
        OnItemAdd = null;
        OnItemSub = null;
        base.Destroy();
    }
}