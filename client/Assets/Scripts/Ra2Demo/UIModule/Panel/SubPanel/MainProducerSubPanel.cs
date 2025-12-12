using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using ZLockstep.Simulation.ECS;
using ZFrame;

/// <summary>
/// 主面板的生产子面板控制器 - 封装子面板的UI逻辑和数据
/// </summary>
public class MainProducerSubPanel
{
    private GameObject root;
    private TMP_Text titleText;
    private Button closeBtn;
    
    // 列表相关（支持ScrollRect）
    private ScrollRect listScrollRect;
    private Transform listContent;      // ScrollRect的Content，列表项放这里
    private GameObject itemTemplate;
    private List<ProducerListItem> listItems = new List<ProducerListItem>();
    
    /// <summary>
    /// 绑定的数据
    /// </summary>
    public SubPanelData Data { get; private set; }
    
    /// <summary>
    /// 关闭按钮点击回调（可选，用于通知父面板）
    /// </summary>
    public Action OnCloseClick;
    
    public MainProducerSubPanel(Transform parent)
    {
        root = parent.Find("Producer")?.gameObject;
        if (root == null) return;
        
        // 获取组件引用
        titleText = root.transform.Find("Title")?.GetComponent<TMP_Text>();
        closeBtn = root.transform.Find("CloseBtn")?.GetComponent<Button>();
        
        closeBtn?.onClick.AddListener(OnClose);
        
        // 初始化列表（ScrollRect结构）
        // 预期结构: ListScrollView(ScrollRect) -> Viewport -> Content
        var scrollViewObj = root.transform.Find("ScrollView");
        if (scrollViewObj != null)
        {
            listScrollRect = scrollViewObj.GetComponent<ScrollRect>();
            listContent = listScrollRect?.content;  // ScrollRect.content 自动指向 Content
        }
        
        // 模板可以放在Content下或者单独放置
        itemTemplate = root.transform.Find("ItemTemplate")?.gameObject;
        if (itemTemplate == null && listContent != null)
        {
            // 尝试从Content下查找模板
            itemTemplate = listContent.Find("ItemTemplate")?.gameObject;
        }
        itemTemplate?.SetActive(false); // 隐藏模板

        var testData = new List<ProducerItemData>
        {
            new() { Name = "动员兵", BelongFactory = BuildingType.Factory, Description = "基础步兵单位", UnitType = UnitType.Infantry },
            new() { Name = "坦克", BelongFactory = BuildingType.Factory, Description = "重装甲战斗单位", UnitType = UnitType.Tank },
        };
        RefreshList(testData);
    }
    
    /// <summary>
    /// 设置数据并显示
    /// </summary>
    public void Show(SubPanelData data)
    {
        Data = data;
        UpdateUI();
        SetActive(true);
    }
    
    /// <summary>
    /// 隐藏子面板
    /// </summary>
    public void Hide()
    {
        SetActive(false);
    }
    
    /// <summary>
    /// 刷新UI显示
    /// </summary>
    public void UpdateUI()
    {
        if (Data == null) return;
        
        if (titleText != null) titleText.text = Data.Title;
    }
    
    /// <summary>
    /// 设置显示状态
    /// </summary>
    public void SetActive(bool active)
    {
        root?.SetActive(active);
    }
    
    private void OnClose()
    {
        Hide();
        OnCloseClick?.Invoke();
    }
    
    /// <summary>
    /// 刷新列表数据
    /// 注意：Content 节点需要添加 VerticalLayoutGroup 或 HorizontalLayoutGroup 组件来自动排列子项
    /// </summary>
    public void RefreshList(List<ProducerItemData> dataList)
    {
        ClearList();
        
        if (itemTemplate == null || listContent == null)
        {
            Debug.LogWarning("列表模板或Content容器未找到");
            return;
        }
        
        // 检查是否有 LayoutGroup，没有的话给出提示
        var layoutGroup = listContent.GetComponent<LayoutGroup>();
        if (layoutGroup == null)
        {
            Debug.LogWarning("Content 上没有 LayoutGroup 组件，列表项可能会重叠。请添加 VerticalLayoutGroup 或 HorizontalLayoutGroup");
        }
        
        foreach (var data in dataList)
        {
            var itemGo = GameObject.Instantiate(itemTemplate, listContent);
            
            // 重置 RectTransform，确保不继承模板的偏移
            var rectTransform = itemGo.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
                // 如果有 LayoutGroup，位置会被自动管理；没有的话重置为零点
                if (layoutGroup == null)
                {
                    rectTransform.anchoredPosition = Vector2.zero;
                }
            }
            
            itemGo.SetActive(true);
            
            var item = new ProducerListItem(itemGo);
            item.SetData(data);
            item.OnItemAdd = OnListItemAdd;
            item.OnItemSub = OnListItemSub;
            listItems.Add(item);
        }
        
        // 强制刷新布局
        if (layoutGroup != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent as RectTransform);
        }
        
        // 刷新后滚动到顶部
        ScrollToTop();
    }
    
    /// <summary>
    /// 清空列表
    /// </summary>
    private void ClearList()
    {
        foreach (var item in listItems)
        {
            item.Destroy();
        }
        listItems.Clear();
    }
    
    /// <summary>
    /// 滚动到顶部
    /// </summary>
    public void ScrollToTop()
    {
        if (listScrollRect != null)
        {
            listScrollRect.verticalNormalizedPosition = 1f;  // 1 = 顶部
        }
    }
    
    /// <summary>
    /// 滚动到底部
    /// </summary>
    public void ScrollToBottom()
    {
        if (listScrollRect != null)
        {
            listScrollRect.verticalNormalizedPosition = 0f;  // 0 = 底部
        }
    }
    
    /// <summary>
    /// 滚动到指定位置 (0-1, 0=底部, 1=顶部)
    /// </summary>
    public void ScrollTo(float normalizedPosition)
    {
        if (listScrollRect != null)
        {
            listScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
        }
    }
    
    /// <summary>
    /// 列表项添加回调
    /// </summary>
    private void OnListItemAdd(ProducerListItem item)
    {
        Debug.Log($"请求添加生产项: {item.ItemData?.Name}");
        // TODO: 实现添加逻辑，例如发送命令增加单位生产
    }
    
    /// <summary>
    /// 列表项减少回调
    /// </summary>
    private void OnListItemSub(ProducerListItem item)
    {
        Debug.Log($"请求减少生产项: {item.ItemData?.Name}");
        // TODO: 实现减少逻辑，例如发送命令减少单位生产
    }
    
    /// <summary>
    /// 列表项选中回调
    /// </summary>
    private void OnListItemSelect(ProducerListItem item)
    {
        Debug.Log($"选中了生产项: {item.ItemData?.Name}");
        Frame.DispatchEvent(new SelectBuildingEvent(item.ItemData.BelongFactory));
        Hide();
    }
    
    /// <summary>
    /// 销毁时调用，清理事件
    /// </summary>
    public void Destroy()
    {
        closeBtn?.onClick.RemoveListener(OnClose);
        OnCloseClick = null;
        
        // 清理列表
        ClearList();
    }
}