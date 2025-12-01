using System.Collections.Generic;
using UnityEngine;
using ZLockstep.View;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command;
using ZLockstep.Sync.Command.Commands;
using zUnity;
using Game.RA2.Client;
using Game.Examples;
using ZLockstep.Sync;
using ZLockstep.View.Systems;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.Flow;

public class NetworkManager : MonoBehaviour
{
    [Header("服务器设置")]
    public bool useLocalServer = false;
    public string LocalServerUrl = "ws://127.0.0.1:8080/ws";
    public string RemoteServerUrl = "wss://www.zhegepai.cn/ws";
    
    [Header("房间设置")]
    public RoomType selectedRoomType = RoomType.DUO;
    private string[] roomTypeOptions = { "单人(SOLO)", "双人(DUO)", "三人(TRIO)", "四人(QUAD)", "八人(OCTO)" };
    
    private WebSocketClient _client;
    private WebSocketNetworkAdaptor _networkAdaptor;
    private BattleGame _game;
    private bool isConnected = false;
    private bool isMatched = false;
    private bool isReady = false;
    private long currentPing = -1;
    
    public System.Action OnConnected;
    public System.Action<MatchSuccessData> OnMatchSuccess;
    public System.Action OnGameStart;
    public System.Action<long> OnPingUpdated;
    
    private void Awake()
    {
        LoadRoomTypeSelection();
        LoadLocalServerOption();
    }
    
    /// <summary>
    /// 加载保存的房间类型选择
    /// </summary>
    private void LoadRoomTypeSelection()
    {
        if (PlayerPrefs.HasKey("SelectedRoomType"))
        {
            selectedRoomType = (RoomType)PlayerPrefs.GetInt("SelectedRoomType");
        }
        else
        {
            selectedRoomType = RoomType.DUO; // 默认值
        }
    }
    
    /// <summary>
    /// 保存房间类型选择
    /// </summary>
    public void SaveRoomTypeSelection()
    {
        PlayerPrefs.SetInt("SelectedRoomType", (int)selectedRoomType);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 加载本地服务器选项
    /// </summary>
    private void LoadLocalServerOption()
    {
        useLocalServer = PlayerPrefs.GetInt("UseLocalServer", 0) == 1;
    }
    
    /// <summary>
    /// 保存本地服务器选项
    /// </summary>
    public void SaveLocalServerOption()
    {
        PlayerPrefs.SetInt("UseLocalServer", useLocalServer ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 连接到服务器
    /// </summary>
    public void ConnectToServer(BattleGame game)
    {
        _game = game;
        
        // 根据选项决定使用哪个服务器地址
        string serverUrl = useLocalServer ? LocalServerUrl : RemoteServerUrl;
        _client = new WebSocketClient(serverUrl, "Player1");
        
        // 注册事件处理
        _client.OnConnected += HandleConnected;
        _client.OnMatchSuccess += HandleMatchSuccess;
        _client.OnGameStart += HandleGameStart;
        _client.OnPingUpdated += HandlePingUpdated;

        // 连接网络适配器和客户端
        _networkAdaptor = new WebSocketNetworkAdaptor(_game, _client);
        
        // 连接服务器
        zUDebug.Log($"[WebSocketNetworkAdaptor] 正在连接服务器: {serverUrl}");
        _client.Connect();
        isConnected = true;
    }
    
    /// <summary>
    /// 连接成功事件处理
    /// </summary>
    private void HandleConnected(string message)
    {
        zUDebug.Log("[NetworkManager] 连接成功: " + message);
        // 使用选定的房间类型发送匹配请求
        _client.SendMatchRequest(selectedRoomType);
        
        OnConnected?.Invoke();
    }
    
    /// <summary>
    /// 匹配成功事件处理
    /// </summary>
    private void HandleMatchSuccess(MatchSuccessData data)
    {
        isMatched = true;
        
        // data.Data为输入数据列表
        zUDebug.Log($"[NetworkManager] 匹配成功：房间ID={data.RoomId}, 阵营ID={data.CampId}, InitialState={data.InitialState}");

        GlobalInfoComponent globalInfoComponent = new GlobalInfoComponent(data.CampId);
        _game.World.ComponentManager.AddGlobalComponent(globalInfoComponent);
        
        // 处理创世阶段 - 初始化游戏世界
        if (data.InitialState != null)
        {
            _game.InitializeWorldFromMatchData(data.InitialState);
        }

        // 发送准备就绪消息
        _client.SendReady();
        
        OnMatchSuccess?.Invoke(data);
    }
    
    /// <summary>
    /// 游戏开始事件处理
    /// </summary>
    private void HandleGameStart()
    {
        isReady = true;
        
        // 在游戏正式启动时，发送一个初始帧确认（帧0）
        // 这样可以启动帧同步逻辑
        if (_game != null && _game.FrameSyncManager != null)
        {
            // 确认第0帧（空帧），启动帧同步逻辑
            _game.FrameSyncManager.ConfirmFrame(0, new List<ICommand>());
        }

        zUDebug.Log("[NetworkManager] 游戏开始，帧同步已启动");
        
        OnGameStart?.Invoke();
    }
    
    /// <summary>
    /// Ping值更新事件处理
    /// </summary>
    private void HandlePingUpdated(long ping)
    {
        currentPing = ping;
        OnPingUpdated?.Invoke(ping);
    }
    
    public bool IsConnected => isConnected;
    public bool IsMatched => isMatched;
    public bool IsReady => isReady;
    public long CurrentPing => currentPing;
    
    public void Disconnect()
    {
        _client?.Disconnect();
        _networkAdaptor = null;
        isConnected = false;
        isMatched = false;
        isReady = false;
    }
    
    public void SendProduceCommand(int factoryEntityId, int unitType, int changeValue)
    {
        if (_game == null || factoryEntityId == -1)
            return;
            
        var produceCommand = new ProduceCommand(
            playerId: 0,
            entityId: factoryEntityId,
            unitType: unitType,
            changeValue: changeValue
        )
        {
            Source = CommandSource.Local
        };
        
        _game.SubmitCommand(produceCommand);
        Debug.Log($"[NetworkManager] 发送生产命令: 工厂{factoryEntityId} 单位类型{unitType} 变化值{changeValue}");
    }
}