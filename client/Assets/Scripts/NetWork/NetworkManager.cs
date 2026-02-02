using System;
using System.Collections.Generic;
using Game.Examples;
using Game.RA2.Client;
using UnityEngine;
using ZFrame;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command;

public class NetworkManager
{
    private static NetworkManager _instance;
    private static readonly object _lock = new object();
    
    // 服务器地址常量
    private const string LOCAL_SERVER_URL = "ws://127.0.0.1:8080/ws";
    private const string REMOTE_SERVER_URL = "wss://www.zhegepai.cn/ws";
    
    public static NetworkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new NetworkManager();
                    }
                }
            }
            return _instance;
        }
    }
    
    private WebSocketClient _currentWebSocket;
    private Ra2Demo _ra2Demo;
    private RoomType _roomType;

    public bool IsMatched { get; set; } = false;
    public bool IsReady { get; set; } = false;
    
    public WebSocketClient CurrentWebSocket
    {
        get { return _currentWebSocket; }
        private set { _currentWebSocket = value; }
    }
    
    /// <summary>
    /// 设置Ra2Demo引用
    /// </summary>
    /// <param name="ra2Demo">Ra2Demo实例</param>
    public void SetRa2Demo(Ra2Demo ra2Demo)
    {
        _ra2Demo = ra2Demo;
    }
    
    /// <summary>
    /// 连接到服务器
    /// </summary>
    /// <param name="useLocalServer">是否使用本地服务器</param>
    public void ConnectToServer(RoomType roomType, bool useLocalServer)
    {
        _roomType = roomType;
        // 根据选项决定使用哪个服务器地址
        string serverUrl = useLocalServer ? LOCAL_SERVER_URL : REMOTE_SERVER_URL;
        _currentWebSocket = new WebSocketClient(serverUrl, "Player1");
        
        // 注册事件处理
        _currentWebSocket.OnConnected += OnConnected;
        _currentWebSocket.OnMatchSuccess += OnMatchSuccess;
        _currentWebSocket.OnGameStart += OnGameStart;
        _currentWebSocket.OnPingUpdated += OnPingUpdated;
        
        // 连接服务器
        Debug.Log($"[NetworkManager] 正在连接服务器: {serverUrl}");
        _currentWebSocket.Connect();
    }
    
    /// <summary>
    /// Ping值更新事件处理
    /// </summary>
    private void OnPingUpdated(long ping)
    {
        Debug.Log($"[NetworkManager] Ping更新: {ping}ms");
        // 可以在这里添加更多处理逻辑
    }

    /// <summary>
    /// 连接成功事件处理
    /// </summary>
    private void OnConnected(string message)
    {
        Debug.Log("[NetworkManager] 连接成功: " + message);
        // 可以在这里添加连接成功的处理逻辑
                // 使用选定的房间类型发送匹配请求
        _currentWebSocket.SendMatchRequest(_roomType);
    }

    /// <summary>
    /// 匹配成功事件处理
    /// </summary>
    private void OnMatchSuccess(MatchSuccessData data)
    {
        IsMatched = true;

        Debug.Log($"[NetworkManager] 匹配成功：房间ID={data.RoomId}, 阵营ID={data.CampId}");
        // 可以在这里添加匹配成功的处理逻辑
        // 创建BattleGame实例
        _ra2Demo.SetBattleGame(new BattleGame(_ra2Demo.Mode, 20, 0));
        _ra2Demo.GetBattleGame().Init();
        
        // 初始化Unity视图层
        _ra2Demo.InitializeUnityView();
        
        // OnFrameSync
        new WebSocketNetworkAdaptor(_ra2Demo, _currentWebSocket);
        
        // data.Data为输入数据列表
        zUDebug.Log($"[Ra2Demo] 匹配成功：房间ID={data.RoomId}, 阵营ID={data.CampId}, InitialState={data.InitialState}");

        GlobalInfoComponent globalInfoComponent = new(data.CampId);
        _ra2Demo.GetBattleGame().World.ComponentManager.AddGlobalComponent(globalInfoComponent);
        
        // 处理创世阶段 - 初始化游戏世界
        if (data.InitialState != null)
        {
            if (data.InitialState.Count == 2)
            {
                _ra2Demo.GetBattleGame().CreateWorldByConfig();
            } else
            {
                _ra2Demo.GetBattleGame().InitializeWorldFromMatchData(data.InitialState);
            }

            
        }

        Frame.DispatchEvent(new MatchedEvent(data));
    }
    
    
    /// <summary>
    /// 游戏开始事件处理
    /// </summary>
    private void OnGameStart()
    {
        IsReady = true;

        Frame.DispatchEvent(new GameStartEvent());

        Debug.Log("[NetworkManager] 游戏开始");
        // 可以在这里添加游戏开始的处理逻辑
                // 在游戏正式启动时，发送一个初始帧确认（帧0）
        // 这样可以启动帧同步逻辑
        if (_ra2Demo.GetBattleGame() != null && _ra2Demo.GetBattleGame().FrameSyncManager != null)
        {
            // 确认第0帧（空帧），启动帧同步逻辑
            _ra2Demo.GetBattleGame().FrameSyncManager.ConfirmFrame(0, new List<ICommand>());
        }

        zUDebug.Log("[Ra2Demo] 游戏开始，帧同步已启动");
        
        // 获取我方战车工厂位置，相机移动到此位置
        _ra2Demo.MoveCameraToOurFactory();
    }
    
    
    public void ResetGame()
    {
        CloseCurrentWebSocket();
        _ra2Demo = null;
    }
    
    /// <summary>
    /// 关闭并清理当前的WebSocket连接
    /// </summary>
    public void CloseCurrentWebSocket()
    {
        if (_currentWebSocket != null)
        {
            _currentWebSocket.Disconnect();
            _currentWebSocket = null;
        }
    }
    
    /// <summary>
    /// 检查WebSocket是否已连接
    /// </summary>
    /// <returns>是否已连接</returns>
    public bool IsConnected()
    {
        return _currentWebSocket != null && _currentWebSocket.IsConnected;
    }
}