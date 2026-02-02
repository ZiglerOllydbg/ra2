using Game.Examples;
using Game.RA2.Client;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync;
using ZLockstep.Sync.Command;

/// <summary>
/// Demo面板 - 示例如何在业务层配置路径、名称和深度类型
/// </summary>
[UIModel(
    panelID = "MatchPanel",
    panelPath = "MatchPanel",
    panelName = "Match面板",
    panelUIDepthType = ClientUIDepthTypeID.GameMid
)]
public class MatchPanel : BasePanel
{
    // 1. 声明UI组件引用
    private Button _soloButton;
    private Button _duoButton;
    private Button _quadButton;
    private Button _replayButton;
    private Toggle _useLocalNetToggle;
    private Transform _matchGroup;
    private Transform _matchingGroup;
    
    public MatchPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    // 2. 在面板显示前获取组件引用
    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        
        // 获取Match按钮组和Matching组容器
        _matchGroup = PanelObject.transform.Find("Match");
        _matchingGroup = PanelObject.transform.Find("Matching");
        
        // 从PanelObject获取按钮组件
        _soloButton = PanelObject.transform.Find("Match/SOLO")?.GetComponent<Button>();
        _duoButton = PanelObject.transform.Find("Match/DUO")?.GetComponent<Button>();
        _quadButton = PanelObject.transform.Find("Match/QUAD")?.GetComponent<Button>();
        _replayButton = PanelObject.transform.Find("Match/REPLAY")?.GetComponent<Button>();
        
        // 获取UseLocalNet Toggle组件
        _useLocalNetToggle = PanelObject.transform.Find("Match/UseLocalNet")?.GetComponent<Toggle>();
        
        // 加载保存的UseLocalNet选项
        LoadUseLocalNetOption();

        // 显示Match按钮组
        _matchGroup.gameObject.SetActive(true);
        // 隐藏匹配中界面
        _matchingGroup.gameObject.SetActive(false);
    }

    // 3. 在AddEvent中添加按钮事件（面板显示时自动调用）
    protected override void AddEvent()
    {
        base.AddEvent();
        
        if (_soloButton != null)
        {
            _soloButton.onClick.AddListener(OnSoloButtonClick);
        }

        if (_duoButton != null)
        {
            _duoButton.onClick.AddListener(OnDuoButtonClick);
        }

        if (_quadButton != null)
        {
            _quadButton.onClick.AddListener(OnQuadButtonClick);
        }
        
        if (_replayButton != null)
        {
            _replayButton.onClick.AddListener(OnReplayButtonClick);
        }

        // 为UseLocalNet Toggle添加值变化监听
        if (_useLocalNetToggle != null)
        {
            _useLocalNetToggle.onValueChanged.AddListener(OnUseLocalNetValueChanged);
        }
    }

    // 4. 在RemoveEvent中移除按钮事件（面板关闭时自动调用）
    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        
        if (_soloButton != null)
        {
            _soloButton.onClick.RemoveListener(OnSoloButtonClick);
        }

        if (_duoButton != null)
        {
            _duoButton.onClick.RemoveListener(OnDuoButtonClick);
        }

        if (_quadButton != null)
        {
            _quadButton.onClick.RemoveListener(OnQuadButtonClick);
        }
        
        if (_replayButton != null)
        {
            _replayButton.onClick.RemoveListener(OnReplayButtonClick);
        }

        // 移除UseLocalNet Toggle的值变化监听
        if (_useLocalNetToggle != null)
        {
            _useLocalNetToggle.onValueChanged.RemoveListener(OnUseLocalNetValueChanged);
        }
    }

    // 5. 按钮点击处理方法
    private void OnSoloButtonClick()
    {
        // 生成带当前日期时间的文件名
        string fileName = $"command_record_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        
        // 可以在这里添加匹配成功的处理逻辑
        Ra2Demo ra2Demo = Object.FindObjectOfType<Ra2Demo>();
        // 创建BattleGame实例
        ra2Demo.Mode = GameMode.Standalone;
        ra2Demo.SetBattleGame(new BattleGame(ra2Demo.Mode, 20, 0));
        

        ra2Demo.GetBattleGame().Init();
        
        // 初始化Unity视图层
        ra2Demo.InitializeUnityView();

        // 全局数据
        GlobalInfoComponent globalInfoComponent = new(1);
        ra2Demo.GetBattleGame().World.ComponentManager.AddGlobalComponent(globalInfoComponent);
        // 创建世界
        ra2Demo.GetBattleGame().CreateWorldByConfig();

        // 在创建BattleGame后，立即开始记录命令
        ra2Demo.GetBattleGame().World.CommandManager.StartRecording(System.IO.Path.Combine(Application.persistentDataPath, fileName));


        Frame.DispatchEvent(new SoloGameStartEvent());

        HideButtons();
    }

    // Replay按钮点击处理方法
    private void OnReplayButtonClick()
    {
        zUDebug.Log("Replay按钮被点击了！");
        
        // 获取最近一次录制的命令文件路径
        // 查找persistentDataPath目录下的command_record_*.txt文件
        string persistentPath = Application.persistentDataPath;
        string[] files = System.IO.Directory.GetFiles(persistentPath, "command_record_*.txt");
        
        if (files.Length == 0)
        {
            zUDebug.LogWarning("没有找到命令记录文件");
            return;
        }
        
        // 找到最新的命令记录文件
        string latestFile = "";
        System.DateTime latestTime = System.DateTime.MinValue;
        
        foreach (string file in files)
        {
            string fileName = System.IO.Path.GetFileName(file);
            string dateStr = fileName.Replace("command_record_", "").Replace(".txt", "");
            
            if (System.DateTime.TryParseExact(dateStr, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out System.DateTime fileTime))
            {
                if (fileTime > latestTime)
                {
                    latestTime = fileTime;
                    latestFile = file;
                }
            }
        }
        
        if (string.IsNullOrEmpty(latestFile))
        {
            zUDebug.LogWarning("未能找到有效的命令记录文件");
            return;
        }
        
        zUDebug.Log($"正在读取命令记录文件: {latestFile}");
        
        // 使用CommandReader读取命令记录
        Utils.CommandReader commandReader = new Utils.CommandReader();
        commandReader.LoadFromFile(latestFile);
        
        // 输出统计信息
        zUDebug.Log($"总共加载了 {commandReader.FrameInputs.Count} 条frameInput记录");
        
        // 获取所有帧号
        var allFrames = commandReader.GetAllFrames();
        zUDebug.Log($"共有 {allFrames.Count} 个不同的帧号");
        
        // 输出前几个和后几个帧的信息
        for (int i = 0; i < Mathf.Min(5, allFrames.Count); i++)
        {
            int frame = allFrames[i];
            var commands = commandReader.GetCommandsByFrame(frame);
            zUDebug.Log($"帧 {frame}: 包含 {commands.Count} 个命令");
            
            // 如果有命令，输出第一个命令的类型
            if (commands.Count > 0)
            {
                var firstCmd = commands[0];
                zUDebug.Log($"  第一个命令类型ID: {firstCmd.commandType}, 命令内容: {firstCmd.command}");
            }
        }
        
        // 如果帧数过多，只显示最后几个
        if (allFrames.Count > 5)
        {
            for (int i = Mathf.Max(5, allFrames.Count - 5); i < allFrames.Count; i++)
            {
                int frame = allFrames[i];
                var commands = commandReader.GetCommandsByFrame(frame);
                zUDebug.Log($"帧 {frame}: 包含 {commands.Count} 个命令");
            }
        }
        
        zUDebug.Log("命令记录读取完成");

        // 开始回放模式
        Ra2Demo ra2Demo = Object.FindObjectOfType<Ra2Demo>();
        // 创建BattleGame实例，使用Replay模式
        ra2Demo.Mode = GameMode.Replay;
        ra2Demo.SetBattleGame(new BattleGame(ra2Demo.Mode, 20, 0));
    
        ra2Demo.GetBattleGame().Init();
        
        // 初始化Unity视图层
        ra2Demo.InitializeUnityView();

        // 全局数据
        GlobalInfoComponent globalInfoComponent = new(1);
        ra2Demo.GetBattleGame().World.ComponentManager.AddGlobalComponent(globalInfoComponent);
        // 创建世界
        ra2Demo.GetBattleGame().CreateWorldByConfig();

        // 设置回放数据到战斗游戏中
        for (int i = 0; i < commandReader.FrameInputs.Count; i++)
        {
            for (int j = 0; j < commandReader.FrameInputs[i].data.Count; j++)
            {
                ICommand cmd = commandReader.FrameInputs[i].data[j].command;
                cmd.ExecuteFrame = commandReader.FrameInputs[i].frame;
                
                ra2Demo.GetBattleGame().World.CommandManager.SubmitCommand(cmd);
            }
        }

        Frame.DispatchEvent(new ReplayGameStartEvent());

        HideButtons();
    }

    private bool GetIsLocalNet()
    {
        return _useLocalNetToggle?.isOn ?? false;
    }

    private void OnDuoButtonClick()
    {
        bool isLocalNet = GetIsLocalNet();
        NetworkManager.Instance.ConnectToServer(RoomType.DUO, isLocalNet);
        HideButtons();
    }

    private void OnQuadButtonClick()
    {
        bool isLocalNet = GetIsLocalNet();
        NetworkManager.Instance.ConnectToServer(RoomType.QUAD, isLocalNet);
        HideButtons();
    }

    // UseLocalNet Toggle值变化处理方法
    private void OnUseLocalNetValueChanged(bool value)
    {
        Debug.Log($"UseLocalNet状态变化为: {value}");
        SaveUseLocalNetOption(value);
    }

    // 保存UseLocalNet选项
    private void SaveUseLocalNetOption(bool value)
    {
        PlayerPrefs.SetInt("UseLocalNet", value ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    // 加载保存的UseLocalNet选项
    private void LoadUseLocalNetOption()
    {
        if (_useLocalNetToggle != null)
        {
            bool savedValue = PlayerPrefs.GetInt("UseLocalNet", 0) == 1;
            _useLocalNetToggle.isOn = savedValue;
        }
    }

    // 隐藏所有按钮并显示匹配中界面
    private void HideButtons()
    {
        if (_matchGroup != null)
        {
            _matchGroup.gameObject.SetActive(false);
        }
        
        if (_matchingGroup != null)
        {
            _matchingGroup.gameObject.SetActive(true);
        }
    }

    public void HideMatchingGroup()
    {
        if (_matchingGroup != null)
        {
            _matchingGroup.gameObject.SetActive(false);
        }
    }
}