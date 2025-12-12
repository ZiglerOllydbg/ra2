using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZFrame;
using ZLib;

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
    
    // 添加游戏实例引用
    private ZLockstep.Sync.Game game;
    
    // 定时刷新相关
    private int refreshTimerId = -1;
    private const float REFRESH_INTERVAL = 0.1f; // 每秒刷新一次
    
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
    }
    
    /// <summary>
    /// 设置游戏实例引用
    /// </summary>
    public void SetGameContext(ZLockstep.Sync.Game game)
    {
        this.game = game;
    }
    
    /// <summary>
    /// 设置数据并显示
    /// </summary>
    public void Show(SubPanelData data)
    {
        RefreshProducerList();
        Data = data;
        UpdateUI();
        SetActive(true);
        
        // 启动定时刷新
        StartAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        // 如果已经有定时器在运行，先停止它
        if (refreshTimerId != -1)
        {
            // 注意：这里可能需要根据实际的API来停止定时器
            refreshTimerId = -1;
        }
        
        // 启动新的定时器，每秒刷新一次
        refreshTimerId = Tick.SetTimeout(RefreshProductionData, REFRESH_INTERVAL);
    }
    
    /// <summary>
    /// 刷新生产数据
    /// </summary>
    private void RefreshProductionData()
    {
        // 检查面板是否处于激活状态
        if (root != null && root.activeInHierarchy)
        {
            UpdateProducerListData(); // 只更新数据，不重建控件
            
            // 重新启动定时器以实现持续刷新
            if (refreshTimerId != -1)
            {
                refreshTimerId = Tick.SetTimeout(RefreshProductionData, REFRESH_INTERVAL);
            }
        }
    }
    
    private void RefreshProducerList()
    {
        // 获取建筑列表
        if (game == null)
            return;

        // 查找所有具有ProduceComponent的实体
        var entities = game.World.ComponentManager.GetAllEntityIdsWith<ProduceComponent>();
        var dataList = new List<ProducerItemData>();
        
        foreach (var entityId in entities)
        {
            var entity = new Entity(entityId);
            if (game.World.ComponentManager.HasComponent<ProduceComponent>(entity))
            {
                var produceComponent = game.World.ComponentManager.GetComponent<ProduceComponent>(entity);
                
                // 为每个支持的单位类型创建一个ProducerItemData
                foreach (var unitType in produceComponent.SupportedUnitTypes)
                {
                    // 获取当前生产数量
                    int currentProduceCount = 0;
                    if (produceComponent.ProduceNumbers.ContainsKey(unitType))
                    {
                        currentProduceCount = produceComponent.ProduceNumbers[unitType];
                    }
                    
                    // 获取当前生产进度
                    int productionProgressPercentage = 0;
                    if (produceComponent.ProduceProgress.ContainsKey(unitType))
                    {
                        productionProgressPercentage = produceComponent.ProduceProgress[unitType];
                    }
                    
                    // 构造带生产数量和进度的名称
                    string displayName = GetUnitTypeName(unitType);
                    if (currentProduceCount > 0)
                    {
                        displayName = $"{displayName} (+{currentProduceCount}) {productionProgressPercentage}%";
                    }
                    
                    var itemData = new ProducerItemData
                    {
                        Name = displayName,
                        BelongFactory = "坦克工厂" + entityId,
                        Description = GetUnitTypeDescription(unitType),
                        UnitType = unitType,
                        FactoryEntityId = entityId,
                    };
                    dataList.Add(itemData);
                }
            }
        }
        
        RefreshList(dataList);
    }
    
    /// <summary>
    /// 更新生产者列表数据，不重建控件
    /// </summary>
    private void UpdateProducerListData()
    {
        if (game == null || listItems == null)
            return;

        // 查找所有具有ProduceComponent的实体
        var entities = game.World.ComponentManager.GetAllEntityIdsWith<ProduceComponent>();
        
        int itemIndex = 0;
        foreach (var entityId in entities)
        {
            var entity = new Entity(entityId);
            if (game.World.ComponentManager.HasComponent<ProduceComponent>(entity))
            {
                var produceComponent = game.World.ComponentManager.GetComponent<ProduceComponent>(entity);
                
                // 为每个支持的单位类型更新ProducerItemData
                foreach (var unitType in produceComponent.SupportedUnitTypes)
                {
                    if (itemIndex >= listItems.Count)
                        return; // 避免索引越界
                    
                    // 获取当前生产数量
                    int currentProduceCount = 0;
                    if (produceComponent.ProduceNumbers.ContainsKey(unitType))
                    {
                        currentProduceCount = produceComponent.ProduceNumbers[unitType];
                    }
                    
                    // 获取当前生产进度
                    int productionProgressPercentage = 0;
                    if (produceComponent.ProduceProgress.ContainsKey(unitType))
                    {
                        productionProgressPercentage = produceComponent.ProduceProgress[unitType];
                    }
                    
                    // 构造带生产数量和进度的名称
                    string displayName = GetUnitTypeName(unitType);
                    if (currentProduceCount > 0)
                    {
                        displayName = $"{displayName} (+{currentProduceCount}) {productionProgressPercentage}%";
                    }
                    
                    var itemData = new ProducerItemData
                    {
                        Name = displayName,
                        BelongFactory = "坦克工厂" + entityId,
                        Description = GetUnitTypeDescription(unitType),
                        UnitType = unitType,
                        FactoryEntityId = entityId,
                    };
                    
                    // 更新对应索引的列表项数据
                    listItems[itemIndex].SetData(itemData);
                    itemIndex++;
                }
            }
        }
    }
    
    /// <summary>
    /// 获取单位类型名称
    /// </summary>
    private string GetUnitTypeName(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.Infantry: return "动员兵";
            case UnitType.Tank: return "坦克($300)";
            case UnitType.Harvester: return "矿车";
            default: return $"单位{unitType}";
        }
    }
    
    /// <summary>
    /// 获取单位类型描述
    /// </summary>
    private string GetUnitTypeDescription(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.Infantry: return "基础步兵单位";
            case UnitType.Tank: return "重装甲战斗单位";
            case UnitType.Harvester: return "采集矿物单位";
            default: return $"单位类型{unitType}";
        }
    }
    
    /// <summary>
    /// 隐藏子面板
    /// </summary>
    public void Hide()
    {
        SetActive(false);
        // 停止定时刷新
        StopAutoRefresh();
    }
    
    private void StopAutoRefresh()
    {
        if (refreshTimerId != -1)
        {
            // 注意：这里可能需要根据实际的API来停止定时器
            refreshTimerId = -1;
        }
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
        // 实现添加逻辑，例如发送命令增加单位生产
        if (item.ItemData != null && game != null)
        {
            SendProduceCommand(item.ItemData.FactoryEntityId, item.ItemData.UnitType, 1);
        }
    }
    
    /// <summary>
    /// 列表项减少回调
    /// </summary>
    private void OnListItemSub(ProducerListItem item)
    {
        Debug.Log($"请求减少生产项: {item.ItemData?.Name}");
        // 实现减少逻辑，例如发送命令减少单位生产
        if (item.ItemData != null && game != null)
        {
            SendProduceCommand(item.ItemData.FactoryEntityId, item.ItemData.UnitType, -1);
        }
    }
    
    /// <summary>
    /// 发送生产命令
    /// </summary>
    /// <param name="factoryEntityId">工厂实体ID</param>
    /// <param name="unitType">单位类型</param>
    /// <param name="changeValue">变化值</param>
    private void SendProduceCommand(int factoryEntityId, UnitType unitType, int changeValue)
    {
        if (game == null || factoryEntityId == -1)
            return;

        var produceCommand = new ZLockstep.Sync.Command.Commands.ProduceCommand(
            campId: 0,
            entityId: factoryEntityId,
            unitType: unitType,
            changeValue: changeValue
        )
        {
            Source = ZLockstep.Sync.Command.CommandSource.Local
        };

        game.SubmitCommand(produceCommand);
        Debug.Log($"[Test] 发送生产命令: 工厂{factoryEntityId} 单位类型{unitType} 变化值{changeValue}");
    }
    
    /// <summary>
    /// 销毁时调用，清理事件
    /// </summary>
    public void Destroy()
    {
        closeBtn?.onClick.RemoveListener(OnClose);
        OnCloseClick = null;
        
        // 停止定时刷新
        StopAutoRefresh();
        
        // 清理列表
        ClearList();
    }
}