using Game.RA2.Client;
using Newtonsoft.Json.Linq;
using ZLockstep.Sync;
using ZLockstep.Sync.Command;
using ZLockstep.Sync.Command.Commands;
using ZLockstep.View;
using System.Collections.Generic;
using zUnity;
using Newtonsoft.Json;
using System;
using System.Reflection;
using Game.Examples;
using UnityEngine;
using ZLockstep.Simulation.ECS.Components;

public class WebSocketNetworkAdaptor : INetworkAdapter
{
    // 添加本地测试选项
    public bool useLocalServer = false;
    public string LocalServerUrl = "ws://127.0.0.1:8080/ws";
    // ws://101.126.136.178:8080/ws | ws://www.zhegepai.cn:8080/ws
    public string RemoteServerUrl = "wss://www.zhegepai.cn/ws";
    public WebSocketClient Client;

    // 添加状态标志
    public bool IsConnected = false;
    public bool IsMatched = false;
    public bool IsReady = false;

    // Ping相关
    public long CurrentPing = -1; // 当前ping值（毫秒）

    private Ra2Demo _ra2Demo;

    public WebSocketNetworkAdaptor(Ra2Demo ra2Demo)
    {
        _ra2Demo = ra2Demo; // 保存引用
    }

        /// <summary>
    /// 加载本地服务器选项
    /// </summary>
    public void LoadLocalServerOption()
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
    public void ConnectToServer()
    {
        // 根据选项决定使用哪个服务器地址
        string serverUrl = useLocalServer ? LocalServerUrl : RemoteServerUrl;
        Client = new WebSocketClient(serverUrl, "Player1");
        
        // 注册事件处理
        Client.OnConnected += OnConnected;
        Client.OnMatchSuccess += OnMatchSuccess;
        Client.OnGameStart += OnGameStart;
        Client.OnPingUpdated += OnPingUpdated; // 订阅ping更新事件

        // 连接网络适配器和客户端 (注意：此时_game还未创建)
        // _networkAdaptor = new WebSocketNetworkAdaptor(_game, _client);
        
        // 连接服务器
        zUDebug.Log($"[WebSocketNetworkAdaptor] 正在连接服务器: {serverUrl}");
        Client.Connect();
        IsConnected = true;
    }

        /// <summary>
    /// Ping值更新事件处理
    /// </summary>
    private void OnPingUpdated(long ping)
    {
        CurrentPing = ping;
    }

    /// <summary>
    /// 游戏开始事件处理
    /// </summary>
    private void OnGameStart()
    {
        IsReady = true;
        
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
        
        // 调整相机位置
        Vector3 adjustedPosition = new(RTSCameraTargetController.Instance.CameraTarget.position.x, -50, RTSCameraTargetController.Instance.CameraTarget.position.z);
        RTSCameraTargetController.Instance.CameraTarget.position = adjustedPosition;

    }

    /// <summary>
    /// 连接成功事件处理
    /// </summary>
    private void OnConnected(string message)
    {
        zUDebug.Log("[Ra2Demo] 连接成功: " + message);
        // 使用选定的房间类型发送匹配请求
        Client.SendMatchRequest(_ra2Demo.selectedRoomType);
    }

    /// <summary>
    /// 匹配成功事件处理
    /// </summary>
    private void OnMatchSuccess(MatchSuccessData data)
    {
        IsMatched = true;
        
        // 创建BattleGame实例
        _ra2Demo.SetBattleGame(new BattleGame(_ra2Demo.Mode, 20, 0));
        _ra2Demo.GetBattleGame().Init();
        
        // 初始化Unity视图层
        _ra2Demo.InitializeUnityView();
        
        // OnFrameSync
        Client.OnFrameSync += OnFrameSync(_ra2Demo.GetBattleGame());

        // 绑定网络适配器
        _ra2Demo.GetBattleGame().FrameSyncManager.NetworkAdapter = this;

        
        // data.Data为输入数据列表
        zUDebug.Log($"[Ra2Demo] 匹配成功：房间ID={data.RoomId}, 阵营ID={data.CampId}, InitialState={data.InitialState}");

        GlobalInfoComponent globalInfoComponent = new(data.CampId);
        _ra2Demo.GetBattleGame().World.ComponentManager.AddGlobalComponent(globalInfoComponent);
        
        // 处理创世阶段 - 初始化游戏世界
        if (data.InitialState != null)
        {
            _ra2Demo.GetBattleGame().InitializeWorldFromMatchData(data.InitialState);
        }

        // 发送准备就绪消息
        Client.SendReady();
    }

    public void SendCommandToServer(ICommand command)
    {
        Client.SendFrameInput(command.ExecuteFrame, command);
    }

    private static System.Action<FrameSyncData> OnFrameSync(ZLockstep.Sync.Game game)
    {
        return (data) =>
        {
            // data.Data为输入数据列表
            var commandsArray = data.Data["data"] as JArray;
            var commandList = new List<ICommand>();

            bool print = false;
            if (commandsArray != null)
            {
                foreach (var commandToken in commandsArray)
                {
                    try
                    {
                        // 解析命令类型和数据
                        var commandObj = commandToken as JObject;
                        int campId = commandObj["campId"]?.ToObject<int>() ?? 0;
                        var inputs = commandObj["inputs"] as JArray;

                        for (int i = 0; i < inputs.Count; i++)
                        {
                            var input = inputs[i] as JObject;
                            int commandType = input["commandType"]?.ToObject<int>() ?? 0;

                            // 根据命令类型创建相应的命令对象
                            ICommand command = null;
                            
                            // 使用映射关系替代switch语句
                            if (CommandMapper.CommandTypeMap.ContainsKey(commandType))
                            {
                                Type commandClass = CommandMapper.CommandTypeMap[commandType];
                                command = (ICommand)JsonConvert.DeserializeObject(input["command"].ToString(), commandClass);
                            }
                            else
                            {
                                zUDebug.LogWarning($"[Ra2Demo] 未知的命令类型: {commandType}");
                            }

                            if (command != null)
                            {
                                commandList.Add(command);
                                zUDebug.Log($"[Ra2Demo] 接收到的操作命令: {command}");
                            }

                            print = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        zUDebug.LogError($"[Ra2Demo] 解析命令时出错: {ex.Message}");
                    }
                }
            }

            if (print)
            {
                zUDebug.Log($"[Ra2Demo] 接收到的命令帧: {data}");
            }

            game.FrameSyncManager.ConfirmFrame(data.Frame, commandList);
        };
    }

    public void ReStartGame()
    {
        // 重置游戏状态
        IsConnected = false;
        IsMatched = false;
        IsReady = false;

        Client?.Disconnect();
        Client = null;
    }
}
