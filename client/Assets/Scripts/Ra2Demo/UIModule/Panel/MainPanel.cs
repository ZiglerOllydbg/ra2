using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using TMPro;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS;
using ZLib;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command.Commands;
using ZLockstep.Sync.Command;

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
    // 能量显示文本
    private TMP_Text powerText;
    
    // 按钮引用
    private Button selectBtn;
    private Button buildBtn;
    private Button settingBtn;
    private Button producerBtn; // 新增生产按钮引用
    private Button sellBtn;     // 新增售卖按钮引用
    
    // 售卖状态标识
    private bool isSelling = false;
    
    // 选择模式切换按钮组
    private Button soloBtn;      // 单个单位模式
    private Button fewBtn;       // 小部队模式
    private Button manyBtn;      // 大部队模式
    
    // 退出按钮
    private Button exitBtn;

    // Zoom Slider 组件
    private Slider zoomSlider;
    public const float CAMERA_ZOOM_MIN = 10f;   // 相机最近距离
    public const float CAMERA_ZOOM_MAX = 30f; // 相机最远距离
    public const float CAMERA_ZOOM_DEFAULT = 15f; // 默认相机高度

    // 确认对话框组件
    private ConfirmDialogComponent confirmDialog;

    // 消息提示相关
    private TMP_Text messageText;
    private CanvasGroup messageCanvasGroup;
    private int messageTimerId = 0;
    private float fadeStartTime = -1f;
    private const float DISPLAY_DURATION = 3f;
    private const float FADE_START_TIME = 2f;
    private const float FADE_DURATION = 1f;

    // 小地图刷新定时器ID
    private int miniMapRefreshTimerId = 0;
    private const float MINIMAP_REFRESH_INTERVAL = 0.1f; // 0.1秒刷新间隔

    public Ra2Demo Ra2Demo { get; set; }
    
    // 子面板控制器
    private MainBuildingSubPanel buildingSubPanel;

    // 生产按钮子子面板
    private MainProducerSubPanel producerSubPanel;

    // 小地图子面板
    private MiniMapSubPanel miniMapSubPanel;

    // 热键快捷栏子面板
    private HotKeySubPanel hotKeySubPanel;

    // 设置子面板
    private SettingSubPanel settingSubPanel;

    // 快捷生产按钮
    private Button infantryFastBtn;
    private Button badgerTankFastBtn;
    private Button grizzlyTankFastBtn;

    // 快捷生产刷新定时器
    private int fastProductionRefreshTimerId = 0;
    private const float FAST_PRODUCTION_REFRESH_INTERVAL = 1f;

    public MainPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {

    }

    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        // 获取面板中的 UI 组件引用
        moneyText = PanelObject.transform.Find("Info/Money/Value")?.GetComponent<TMP_Text>();
        powerText = PanelObject.transform.Find("Info/Power/Value")?.GetComponent<TMP_Text>();
        
        // 获取主功能按钮组件
        selectBtn = PanelObject.transform.Find("SelectBtn")?.GetComponent<Button>();
        buildBtn = PanelObject.transform.Find("BuildingBtn")?.GetComponent<Button>();
        settingBtn = PanelObject.transform.Find("SettingBtn")?.GetComponent<Button>();
        producerBtn = PanelObject.transform.Find("ProducerBtn")?.GetComponent<Button>();
        sellBtn = PanelObject.transform.Find("SellBtn")?.GetComponent<Button>();

        // 获取选择模式切换按钮组（三个新按钮）
        soloBtn = PanelObject.transform.Find("SoloBtn")?.GetComponent<Button>();
        fewBtn = PanelObject.transform.Find("FewBtn")?.GetComponent<Button>();
        manyBtn = PanelObject.transform.Find("ManyBtn")?.GetComponent<Button>();

        // 获取退出按钮
        exitBtn = PanelObject.transform.Find("ExitBtn")?.GetComponent<Button>();
        
        // 获取 Zoom Slider 组件
        zoomSlider = PanelObject.transform.Find("ZoomSlider")?.GetComponent<Slider>();
        
        // 初始化选择按钮组的背景图片状态（默认选中第一个按钮 - Solo 模式）
        ClickFewMode();
        
        // 初始化售卖状态
        InitializeSellState();
        
        // 初始化和获取确认对话框组件
        confirmDialog = new ConfirmDialogComponent(PanelObject.transform.Find("Confirm"));
        
        // 获取消息提示组件
        var messageGroup = PanelObject.transform.Find("Tips");
        if (messageGroup != null) 
        {
            messageCanvasGroup = messageGroup.GetComponent<CanvasGroup>();
            if (messageCanvasGroup == null)
                messageCanvasGroup = messageGroup.gameObject.AddComponent<CanvasGroup>();
            
            // 初始隐藏消息
            messageCanvasGroup.alpha = 0;
        }

        messageText = PanelObject.transform.Find("Tips/Message")?.GetComponent<TMP_Text>();

        // 初始化建造子面板
        buildingSubPanel = new MainBuildingSubPanel(PanelObject.transform)
        {
            OnCloseClick = OnSubPanelClosed
        };
        buildingSubPanel.Hide();

        // 初始化生产子面板
        producerSubPanel = new MainProducerSubPanel(PanelObject.transform)
        {
            OnCloseClick = OnSubPanelClosed
        };
        producerSubPanel.Hide();
        producerSubPanel.SetGameContext(Ra2Demo.GetBattleGame());

        // 初始化小地图子面板
        miniMapSubPanel = new MiniMapSubPanel(PanelObject.transform);
        miniMapSubPanel.Show(new MiniMapPanelData() { Title = "小地图", SizeText = "地图尺寸：128*128"});
        
        // 设置小地图纹理
        if (Ra2Demo != null && miniMapSubPanel != null)
        {
            var miniMapTexture = Ra2Demo.GetMiniMapTexture();
            miniMapSubPanel.SetMiniMapTexture(miniMapTexture);
        }
        
        // 启动小地图定时刷新
        StartMiniMapRefresh();

        // 初始化 Zoom Slider
        InitializeZoomSlider();

        // 初始化热键快捷栏子面板
        hotKeySubPanel = new HotKeySubPanel(PanelObject.transform);
        hotKeySubPanel.Show(new HotKeyPanelData() { HotKeys = new string[] { "1", "2", "3", "4" } });

        // 初始化设置子面板
        settingSubPanel = new SettingSubPanel(PanelObject.transform)
        {
            OnCloseClick = OnSubPanelClosed
        };
        settingSubPanel.Hide();

        // 初始化快捷生产按钮
        InitializeFastProductionButtons();
    }

    /// <summary>
    /// 启动小地图刷新定时器
    /// </summary>
    private void StartMiniMapRefresh()
    {
        if (miniMapRefreshTimerId != 0)
        {
            Tick.ClearTimeout(miniMapRefreshTimerId);
        }
        
        miniMapRefreshTimerId = Tick.SetTimeout(RefreshMiniMapTexture, MINIMAP_REFRESH_INTERVAL);
    }
    
    /// <summary>
    /// 刷新小地图纹理
    /// </summary>
    private void RefreshMiniMapTexture()
    {
        // 检查面板是否可见且Ra2Demo和miniMapSubPanel存在
        if (Ra2Demo != null && miniMapSubPanel != null)
        {
            var miniMapTexture = Ra2Demo.GetMiniMapTexture();
            miniMapSubPanel.SetMiniMapTexture(miniMapTexture);
        }
        
        // 重新启动定时器以实现持续刷新
        StartMiniMapRefresh();
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

        // 新增生产按钮事件监听
        if (producerBtn != null)
        {
            producerBtn.onClick.AddListener(OnProducerButtonClick);
        }

        // 新增售卖按钮事件监听
        if (sellBtn != null)
        {
            sellBtn.onClick.AddListener(OnSellButtonClick);
        }

        // 选择模式切换按钮事件监听
        if (soloBtn != null)
        {
            soloBtn.onClick.AddListener(OnSoloModeClick);
        }
        
        if (fewBtn != null)
        {
            fewBtn.onClick.AddListener(OnFewModeClick);
        }
        
        if (manyBtn != null)
        {
            manyBtn.onClick.AddListener(OnManyModeClick);
        }

        // 退出按钮事件监听
        if (exitBtn != null)
        {
            exitBtn.onClick.AddListener(OnExitButtonClick);
        }

        // Zoom Slider 事件监听
        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.AddListener(OnZoomSliderChanged);
        }

        // 快捷生产按钮事件监听
        if (infantryFastBtn != null) infantryFastBtn.onClick.AddListener(OnInfantryFastBtnClick);
        if (badgerTankFastBtn != null) badgerTankFastBtn.onClick.AddListener(OnBadgerTankFastBtnClick);
        if (grizzlyTankFastBtn != null) grizzlyTankFastBtn.onClick.AddListener(OnGrizzlyTankFastBtnClick);

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

        // 移除生产按钮事件监听
        if (producerBtn != null)
        {
            producerBtn.onClick.RemoveListener(OnProducerButtonClick);
        }

        // 移除售卖按钮事件监听
        if (sellBtn != null)
        {
            sellBtn.onClick.RemoveListener(OnSellButtonClick);
        }

        // 移除选择模式切换按钮事件监听
        if (soloBtn != null)
        {
            soloBtn.onClick.RemoveListener(OnSoloModeClick);
        }
        
        if (fewBtn != null)
        {
            fewBtn.onClick.RemoveListener(OnFewModeClick);
        }
        
        if (manyBtn != null)
        {
            manyBtn.onClick.RemoveListener(OnManyModeClick);
        }

        // 退出按钮事件监听
        if (exitBtn != null)
        {
            exitBtn.onClick.RemoveListener(OnExitButtonClick);
        }

        // Zoom Slider 事件监听
        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.RemoveListener(OnZoomSliderChanged);
        }

        // 移除快捷生产按钮事件监听
        if (infantryFastBtn != null) infantryFastBtn.onClick.RemoveListener(OnInfantryFastBtnClick);
        if (badgerTankFastBtn != null) badgerTankFastBtn.onClick.RemoveListener(OnBadgerTankFastBtnClick);
        if (grizzlyTankFastBtn != null) grizzlyTankFastBtn.onClick.RemoveListener(OnGrizzlyTankFastBtnClick);

    }
    
    protected override void OnBecameInvisible()
    {
        base.OnBecameInvisible();
        
        // 清理子面板
        buildingSubPanel?.Destroy();
        buildingSubPanel = null;

        producerSubPanel?.Destroy();
        producerSubPanel = null;

        // 清理设置子面板
        settingSubPanel?.Destroy();
        settingSubPanel = null;

        // 停止正在进行的消息显示定时器
        if (messageTimerId != 0)
        {
            Tick.ClearTimeout(messageTimerId);
            messageTimerId = 0;
        }
        
        // 停止小地图刷新定时器
        if (miniMapRefreshTimerId != 0)
        {
            Tick.ClearTimeout(miniMapRefreshTimerId);
            miniMapRefreshTimerId = 0;
        }

        // 停止快捷生产刷新定时器
        if (fastProductionRefreshTimerId != 0)
        {
            Tick.ClearTimeout(fastProductionRefreshTimerId);
            fastProductionRefreshTimerId = 0;
        }

        // 销毁热键快捷栏子面板
        hotKeySubPanel?.Destroy();
        hotKeySubPanel = null;
        
        // 清理确认对话框组件
        if (confirmDialog != null)
        {
            confirmDialog.UnregisterEvents();
        }

        // 停止BGM
        StopMusicBGM();
    }
    private void StopMusicBGM()
    {
        // 获取 BGM 的 AudioSource 组件
        GameObject bgmObject = GameObject.Find("BGM");
        if (bgmObject != null)
        {
            AudioSource bgmAudioSource = bgmObject.GetComponent<AudioSource>();
            if (bgmAudioSource != null)
            {
                // 获取保存的背景音乐音量
                if (bgmAudioSource.isPlaying)
                {
                    bgmAudioSource.Stop();
                }
            }
        }
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
    /// 设置能量数值显示
    /// </summary>
    /// <param name="power">当前能量</param>
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
    /// 获取能量显示文本组件
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
    /// 设置能量显示文本组件
    /// </summary>
    /// <param name="text">TMP_Text组件</param>
    public void SetPowerText(TMP_Text text)
    {
        powerText = text;
    }
    
    #region 消息提示系统
    
    /// <summary>
    /// 显示消息提示，持续3秒，2秒后开始淡出
    /// </summary>
    /// <param name="message">要显示的消息内容</param>
    public void ShowMessage(string message)
    {
        if (messageText == null || messageCanvasGroup == null) return;
        
        // 如果已经有消息正在显示，则停止它
        if (messageTimerId != 0)
        {
            Tick.ClearTimeout(messageTimerId);
            messageTimerId = 0;
        }
        
        // 设置消息文本
        messageText.text = message;
        
        // 显示消息 (alpha从0到1)
        messageCanvasGroup.alpha = 1f;
        
        // 启动新的消息显示定时器
        messageTimerId = Tick.SetTimeout(StartFadeOut, FADE_START_TIME);
    }
    
    /// <summary>
    /// 开始淡出消息
    /// </summary>
    private void StartFadeOut()
    {
        messageTimerId = Tick.SetInterval(FadeOut, 0.02f); // 每0.02秒更新一次淡出效果
        fadeStartTime = Time.time;
    }
    
    /// <summary>
    /// 淡出消息
    /// </summary>
    private void FadeOut()
    {
        float elapsed = Time.time - fadeStartTime;
        if (elapsed >= FADE_DURATION)
        {
            // 淡出完成
            messageCanvasGroup.alpha = 0f;
            messageText.text = "";
            Tick.ClearTimeout(messageTimerId);
            messageTimerId = 0;
        }
        else
        {
            // 更新透明度
            messageCanvasGroup.alpha = 1f - (elapsed / FADE_DURATION);
        }
    }
    
    #endregion
    
    /// <summary>
    /// 显示确认售卖建筑对话框
    /// </summary>
    /// <param name="entityId">建筑实体 ID</param>
    /// <param name="buildingName">建筑名称</param>
    public void ShowConfirmSellBuilding(int entityId)
    {
        Entity entity = new Entity(entityId);
        BuildingComponent buildingComponent = Ra2Demo.GetBattleGame().World.ComponentManager.GetComponent<BuildingComponent>(entity);

        ConfBuilding confBuilding = ConfigManager.Get<ConfBuilding>(buildingComponent.BuildingType);
        if (confBuilding == null)
        {
            zUDebug.LogError($"[MainPanel] 找不到建筑配置: BuildingType={buildingComponent.BuildingType}");
            return;
        }

        confirmDialog.Show(
            onConfirm: () =>
            {
                // 确认售卖，调用售卖 command
                zUDebug.Log($"[MainPanel] 确认售卖建筑：{confBuilding.Name}, EntityId: {entityId}");

                // 获取玩家阵营 ID（假设为 0）
                int campId = 0;
                
                // 创建售卖建筑命令
                var sellCommand = new SellStructureCommand(campId, entityId);
                
                // 提交命令到游戏世界
                Ra2Demo?.GetBattleGame()?.SubmitCommand(sellCommand);
                
                zUDebug.Log($"[MainPanel] 已提交售卖建筑命令");
            },
            message: $"是否要售卖\"{confBuilding.Name}\"建筑？"
        );
    }

    private void OnExitButtonClick()
    {
        UISound.Instance.PlayClick();

        confirmDialog.Show(
            onConfirm: () =>
            {
                Frame.DispatchEvent(new RestartGameEvent());
            },
            message: "确定要退出游戏吗？"
        );
    }
    
    /// <summary>
    /// 子面板关闭回调
    /// </summary>
    private void OnSubPanelClosed()
    {
        // 处理子面板关闭后的逻辑
        zUDebug.Log("子面板已关闭");
    }
    
    /// <summary>
    /// 刷新列表数据
    /// </summary>
    public void RefreshList(List<DemoItemData> dataList)
    {
        // 确保子面板已经显示
        if (buildingSubPanel != null)
        {
            // 这里可以根据实际需要决定是否自动显示子面板
            // ShowSubPanel(new SubPanelData { Title = "列表面板", Content = "这是一个列表" });
            // subPanel.RefreshList(dataList);
        }
    }

    /// <summary>
    /// Select 按钮点击处理
    /// </summary>
    private void OnSelectButtonClick()
    {
        // 获取按钮上的文本组件
        TMP_Text buttonText = selectBtn.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            // 切换按钮文本
            if (buttonText.text == "选择")
            {
                Ra2Demo.IsSelectMode = false;
                buttonText.text = "移动";
                ShowMessage("切换到移动模式");
            }
            else
            {
                Ra2Demo.IsSelectMode = true;
                buttonText.text = "选择";
                ShowMessage("切换到选择模式");
            }
        }
        else
        {
            zUDebug.LogError("Can't find button text component!");
        }
    }
    
    /// <summary>
    /// Build按钮点击处理 - 打开子面板
    /// </summary>
    private void OnBuildButtonClick()
    {
        UISound.Instance.PlayClick();
        zUDebug.Log("Build按钮被点击了！");

        producerSubPanel.Hide();
        settingSubPanel.Hide();

        if (buildingSubPanel.IsVisiable())
        {
            buildingSubPanel.Hide();
        }
        else
        {
            // 创建子面板数据
            var subPanelData = new SubPanelData
            {
                Title = "建造菜单",
                Content = "请选择要建造的建筑"
            };
            // 显示子面板
            buildingSubPanel.Show(subPanelData);
        }
    }

    /// <summary>
    /// 生产按钮点击处理 - 打开生产子面板
    /// </summary>
    private void OnProducerButtonClick()
    {
        UISound.Instance.PlayClick();
        zUDebug.Log("Producer 按钮被点击了！");
        
        buildingSubPanel.Hide();
        settingSubPanel.Hide();

        if (producerSubPanel.IsVisiable())
        {
            producerSubPanel.Hide();
        }
        else
        {
            // 创建子面板数据
            var subPanelData = new SubPanelData
            {
                Title = "生产菜单",
                Content = "请选择要生产的单位"
            };
            // 显示子面板
            producerSubPanel.Show(subPanelData);
        }
    }
    
    /// <summary>
    /// 售卖按钮点击处理 - 切换售卖状态和背景图片
    /// </summary>
    private void OnSellButtonClick()
    {
        UISound.Instance.PlayClick();
        zUDebug.Log("Sell 按钮被点击了！");
        
        // 切换售卖状态
        isSelling = !isSelling;
        
        // 根据状态加载对应的背景图片
        Sprite sellSprite = isSelling 
            ? ResourceCache.GetSprite("images/SellSelected") 
            : ResourceCache.GetSprite("images/Sell");
        
        // 设置按钮背景图片
        SetButtonBackground(sellBtn, sellSprite);

        Ra2Demo.SetSellMode(isSelling);
        
        // 显示提示信息
        ShowMessage(isSelling ? "进入售卖模式，8折出售！" : "退出售卖模式");
    }
    
    /// <summary>
    /// Setting 按钮点击处理 - 打开/关闭设置面板
    /// </summary>
    private void OnSettingButtonClick()
    {
        UISound.Instance.PlayClick();
        zUDebug.Log("Setting 按钮被点击了！");
        
        // 关闭其他子面板
        buildingSubPanel.Hide();
        producerSubPanel.Hide();

        if (settingSubPanel.IsVisiable())
        {
            settingSubPanel.Hide();
        }
        else
        {
            // 创建子面板数据
            var subPanelData = new SubPanelData
            {
                Title = "设置菜单",
                Content = "调节游戏音量和画面设置"
            };
            // 显示子面板
            settingSubPanel.Show(subPanelData);
        }
    }
    
    /// <summary>
    /// 单个单位模式按钮点击处理
    /// </summary>
    private void OnSoloModeClick()
    {
        UISound.Instance.PlayClick();
        if (soloBtn == null) return;
        
        TMP_Text buttonText = soloBtn.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            Ra2Demo.SetUnitSelectionRadius(5f);
            Ra2Demo.SetMaxSelectionCount(1); // 只选择 1 个单位
            ShowMessage("选择单人");
        }
        else
        {
            zUDebug.LogError("[MainPanel] Can't find solo button text component!");
        }
        
        // 更新所有按钮的背景图片状态
        UpdateSelectionButtonsState(0); // Solo 按钮被选中
    }
    
    /// <summary>
    /// 小部队模式按钮点击处理
    /// </summary>
    private void OnFewModeClick()
    {
        UISound.Instance.PlayClick();
        ClickFewMode();
    }

    private void ClickFewMode()
    {
        TMP_Text buttonText = fewBtn.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            Ra2Demo.SetUnitSelectionRadius(5f);
            Ra2Demo.SetMaxSelectionCount(0); // 不限制数量
            ShowMessage("选择小部队");
        }
        else
        {
            zUDebug.LogError("[MainPanel] Can't find few button text component!");
        }
        
        // 更新所有按钮的背景图片状态
        UpdateSelectionButtonsState(1); // Few 按钮被选中
    }
    
    /// <summary>
    /// 大部队模式按钮点击处理
    /// </summary>
    private void OnManyModeClick()
    {
        UISound.Instance.PlayClick();
        if (manyBtn == null) return;
        
        TMP_Text buttonText = manyBtn.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            Ra2Demo.SetUnitSelectionRadius(15f);
            Ra2Demo.SetMaxSelectionCount(0); // 不限制数量
            ShowMessage("选择大部队");
        }
        else
        {
            zUDebug.LogError("[MainPanel] Can't find many button text component!");
        }
        
        // 更新所有按钮的背景图片状态
        UpdateSelectionButtonsState(2); // 大部队按钮被选中
    }
    
    /// <summary>
    /// 更新选择模式按钮组的背景图片状态
    /// </summary>
    /// <param name="selectedIndex">选中的按钮索引 (0=Solo, 1=Few, 2=Many)</param>
    private void UpdateSelectionButtonsState(int selectedIndex)
    {
        // 加载背景图片资源
        Sprite f1 = ResourceCache.GetSprite("images/f1");
        Sprite f2 = ResourceCache.GetSprite("images/f2");
        Sprite f3 = ResourceCache.GetSprite("images/f3");
        
        Sprite b1 = ResourceCache.GetSprite("images/b1");
        Sprite b2 = ResourceCache.GetSprite("images/b2");
        Sprite b3 = ResourceCache.GetSprite("images/b3");
        
        // 根据选中的按钮设置背景图片
        switch (selectedIndex)
        {
            case 0: // Solo 按钮被选中
                SetButtonBackground(soloBtn, b1);
                SetButtonBackground(fewBtn, f2);
                SetButtonBackground(manyBtn, f3);
                break;
            case 1: // Few 按钮被选中
                SetButtonBackground(soloBtn, f1);
                SetButtonBackground(fewBtn, b2);
                SetButtonBackground(manyBtn, f3);
                break;
            case 2: // Many 按钮被选中
                SetButtonBackground(soloBtn, f1);
                SetButtonBackground(fewBtn, f2);
                SetButtonBackground(manyBtn, b3);
                break;
        }
    }
    
    /// <summary>
    /// 设置按钮的背景图片
    /// </summary>
    /// <param name="button">目标按钮</param>
    /// <param name="sprite">要设置的图片</param>
    private void SetButtonBackground(Button button, Sprite sprite)
    {
        if (button == null || sprite == null) return;
        
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }
    }
    
    /// <summary>
    /// 初始化售卖状态为默认值 (关闭状态)
    /// </summary>
    private void InitializeSellState()
    {
        // 设置初始售卖状态为 false
        isSelling = false;
        
        // 加载默认的"Sell"背景图片并应用
        Sprite sellSprite = ResourceCache.GetSprite("images/Sell");
        SetButtonBackground(sellBtn, sellSprite);
        
        // 同步游戏逻辑状态
        Ra2Demo.SetSellMode(isSelling);
    }
    
    /// <summary>
    /// 初始化 Zoom Slider
    /// </summary>
    private void InitializeZoomSlider()
    {
        if (zoomSlider == null || Ra2Demo == null) return;
        
        // 设置 Slider 的范围
        zoomSlider.minValue = CAMERA_ZOOM_MIN;
        zoomSlider.maxValue = CAMERA_ZOOM_MAX;
        
        // 获取当前相机高度作为初始值
        Camera camera = Camera.main;
        camera.orthographicSize = CAMERA_ZOOM_DEFAULT;
        // 设置 Slider 当前值为相机当前高度
        zoomSlider.value = CAMERA_ZOOM_DEFAULT;
        
        zUDebug.Log($"[MainPanel] Zoom Slider 初始化 - 范围：[{CAMERA_ZOOM_MIN}, {CAMERA_ZOOM_MAX}], 当前高度：{CAMERA_ZOOM_DEFAULT:F2}");
    }
    
    /// <summary>
    /// Zoom Slider 值改变处理
    /// </summary>
    /// <param name="value">Slider 当前值 (相机 Y 坐标)</param>
    private void OnZoomSliderChanged(float value)
    {
        if (Ra2Demo == null) return;

        Camera camera = Camera.main;
        camera.orthographicSize = value;
        
        // 调试日志
        zUDebug.Log($"[MainPanel] Zoom Slider 值改变 - 新高度：{value:F2}");
    }

    #region 快捷生产按钮

    /// <summary>
    /// 初始化快捷生产按钮
    /// </summary>
    private void InitializeFastProductionButtons()
    {
        // 显示FastProduction
        PanelObject.transform.Find("FastProduction")?.transform.gameObject.SetActive(true);
        infantryFastBtn = PanelObject.transform.Find("FastProduction/InfantryBtn")?.GetComponent<Button>();
        badgerTankFastBtn = PanelObject.transform.Find("FastProduction/BadgerTankBtn")?.GetComponent<Button>();
        grizzlyTankFastBtn = PanelObject.transform.Find("FastProduction/GrizzlyTankBtn")?.GetComponent<Button>();

        // 默认隐藏
        if (infantryFastBtn != null) infantryFastBtn.gameObject.SetActive(false);
        if (badgerTankFastBtn != null) badgerTankFastBtn.gameObject.SetActive(false);
        if (grizzlyTankFastBtn != null) grizzlyTankFastBtn.gameObject.SetActive(false);

        // 启动定时刷新
        StartFastProductionRefresh();
    }

    /// <summary>
    /// 启动快捷生产刷新定时器
    /// </summary>
    private void StartFastProductionRefresh()
    {
        if (fastProductionRefreshTimerId != 0)
        {
            Tick.ClearTimeout(fastProductionRefreshTimerId);
        }
        fastProductionRefreshTimerId = Tick.SetTimeout(RefreshFastProductionButtons, FAST_PRODUCTION_REFRESH_INTERVAL);
    }

    /// <summary>
    /// 刷新快捷生产按钮的显示状态
    /// </summary>
    private void RefreshFastProductionButtons()
    {
        var game = Ra2Demo?.GetBattleGame();
        if (game == null)
        {
            StartFastProductionRefresh();
            return;
        }

        // 获取所有可生产的工厂（与 MainProducerSubPanel 相同的筛选条件）
        var components = game.World.ComponentManager
            .GetComponentsWithCondition<ProduceComponent>(entity =>
                game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity) &&
                !game.World.ComponentManager.HasComponent<BuildingConstructionComponent>(entity) &&
                game.World.ComponentManager.HasComponent<BuildingComponent>(entity));

        // 收集所有可生产的单位类型及生产数量
        var supportedTypes = new HashSet<UnitType>();
        var produceCounts = new Dictionary<UnitType, int>();
        foreach (var (produceComponent, entity) in components)
        {
            foreach (var unitType in produceComponent.SupportedUnitTypes)
            {
                supportedTypes.Add(unitType);
                // 累加该单位类型的生产数量
                int count = 0;
                if (produceComponent.ProduceNumbers.ContainsKey(unitType))
                {
                    count = produceComponent.ProduceNumbers[unitType];
                }
                if (!produceCounts.ContainsKey(unitType)) produceCounts[unitType] = 0;
                produceCounts[unitType] += count;
            }
        }

        // 根据可生产类型显示/隐藏按钮，并更新文字
        UpdateFastBtn(infantryFastBtn, UnitType.Infantry, supportedTypes, produceCounts);
        UpdateFastBtn(badgerTankFastBtn, UnitType.badgerTank, supportedTypes, produceCounts);
        UpdateFastBtn(grizzlyTankFastBtn, UnitType.grizzlyTank, supportedTypes, produceCounts);

        // 重新启动定时器
        StartFastProductionRefresh();
    }

    /// <summary>
    /// 更新快捷生产按钮的显示状态和文字
    /// </summary>
    private void UpdateFastBtn(Button btn, UnitType unitType, HashSet<UnitType> supportedTypes, Dictionary<UnitType, int> produceCounts)
    {
        if (btn == null) return;
        bool canProduce = supportedTypes.Contains(unitType);
        btn.gameObject.SetActive(canProduce);
        if (!canProduce) return;

        ConfUnit confUnit = ConfigManager.Get<ConfUnit>((int)unitType);
        if (confUnit == null) return;

        int count = produceCounts.ContainsKey(unitType) ? produceCounts[unitType] : 0;
        TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
        {
            btnText.text = count > 0 ? $"{confUnit.Name}+{count}" : confUnit.Name;
        }
    }

    private void OnInfantryFastBtnClick()
    {
        UISound.Instance.PlayClick();
        FastProduceUnit(UnitType.Infantry);
    }

    private void OnBadgerTankFastBtnClick()
    {
        UISound.Instance.PlayClick();
        FastProduceUnit(UnitType.badgerTank);
    }

    private void OnGrizzlyTankFastBtnClick()
    {
        UISound.Instance.PlayClick();
        FastProduceUnit(UnitType.grizzlyTank);
    }

    /// <summary>
    /// 快捷生产单位 - 找到第一个支持该单位类型的工厂并发送生产命令
    /// </summary>
    private void FastProduceUnit(UnitType unitType)
    {
        var game = Ra2Demo?.GetBattleGame();
        if (game == null) return;

        var components = game.World.ComponentManager
            .GetComponentsWithCondition<ProduceComponent>(entity =>
                game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity) &&
                !game.World.ComponentManager.HasComponent<BuildingConstructionComponent>(entity) &&
                game.World.ComponentManager.HasComponent<BuildingComponent>(entity));

        foreach (var (produceComponent, entity) in components)
        {
            if (produceComponent.SupportedUnitTypes.Contains(unitType))
            {
                var produceCommand = new ProduceCommand(
                    campId: 0,
                    entityId: entity.Id,
                    unitType: unitType,
                    changeValue: 1)
                {
                    Source = CommandSource.Local
                };
                game.SubmitCommand(produceCommand);
                zUDebug.Log($"[快捷生产] 发送生产命令: 单位{unitType}, 工厂{entity.Id}");
                return;
            }
        }
    }

    #endregion
}