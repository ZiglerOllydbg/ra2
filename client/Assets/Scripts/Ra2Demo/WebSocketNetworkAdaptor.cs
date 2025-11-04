using Game.RA2.Client;
using Newtonsoft.Json.Linq;
using ZLockstep.Sync;
using ZLockstep.Sync.Command;
using ZLockstep.Sync.Command.Commands;
using ZLockstep.View;
using System.Collections.Generic;
using zUnity;
using Newtonsoft.Json;

public class WebSocketNetworkAdaptor : INetworkAdapter
{
    private readonly GameWorldBridge _gameWorldBridge; // 保存引用
    private WebSocketClient _client;
    private RoomType _selectedRoomType = RoomType.DUO; // 默认房间类型

    public WebSocketNetworkAdaptor(GameWorldBridge gameWorldBridge)
    {
        _gameWorldBridge = gameWorldBridge; // 保存引用
    }

    public void Connect(string url, string clientId)
    {
        _client = new WebSocketClient(url, clientId);
                // OnConnected
        _client.OnConnected += OnConnected();
        // OnMatchSuccess
        _client.OnMatchSuccess += OnMatchSuccess(); // 注册匹配成功事件
        
        // OnGameStart
        _client.OnGameStart += OnGameStart();
        // OnFrameSync
        _client.OnFrameSync += OnFrameSync(_gameWorldBridge);

        // 绑定网络适配器
        _gameWorldBridge.Game.FrameSyncManager.NetworkAdapter = this;

        // 连接服务器
        zUDebug.Log("[WebSocketNetworkAdaptor] 正在连接服务器...");
        _client.Connect();

    }
    
    public void OnDispatchMessageQueue()
    {
        _client?.DispatchMessageQueue();
    }
    
    public void SetRoomType(RoomType roomType)
    {
        _selectedRoomType = roomType;
    }

    private System.Action<string> OnConnected()
    {
        return (message) => {
            // 使用选定的房间类型发送匹配请求
            _client.SendMatchRequest(_selectedRoomType);
        };
    }
    
    private System.Action OnGameStart()
    {
        return () => {
            // 在游戏正式启动时，发送一个初始帧确认（帧0）
            // 这样可以启动帧同步逻辑
            if (_gameWorldBridge != null && _gameWorldBridge.Game != null &&
                _gameWorldBridge.Game.FrameSyncManager != null)
            {
                // 确认第0帧（空帧），启动帧同步逻辑
                _gameWorldBridge.Game.FrameSyncManager.ConfirmFrame(0, new List<ICommand>());
            }
            
            zUDebug.Log("[WebSocketNetworkAdaptor] 游戏开始，帧同步已启动");
        };
    }

    private static System.Action<FrameSyncData> OnFrameSync(GameWorldBridge gameWorldBridge)
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
                            switch (commandType)
                            {
                                case CommandTypes.CreateUnit:
                                    command = JsonConvert.DeserializeObject<CreateUnitCommand>(input["command"].ToString());
                                    break;
                                case CommandTypes.Move:
                                    command = JsonConvert.DeserializeObject<MoveCommand>(input["command"].ToString());
                                    break;
                                default:
                                    zUDebug.LogWarning($"[Ra2Demo] 未知的命令类型: {commandType}");
                                    break;
                            }

                            if (command != null)
                            {
                                commandList.Add(command);
                                zUDebug.Log($"[Ra2Demo] 接收到的操作命令: {command}");
                            }

                            print = true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        zUDebug.LogError($"[Ra2Demo] 解析命令时出错: {ex.Message}");
                    }
                }
            }

            if (print)
            {
                zUDebug.Log($"[Ra2Demo] 接收到的命令帧: {data}");
            }

            gameWorldBridge.Game.FrameSyncManager.ConfirmFrame(data.Frame, commandList);
        };
    }
    
    private System.Action<MatchSuccessData> OnMatchSuccess()
    {
        return (data) =>
        {
            // data.Data为输入数据列表
            zUDebug.Log($"[Ra2Demo] 匹配成功：房间ID={data.RoomId}, 阵营ID={data.CampId}, data={data}");
            
            // 更新 Game 中的玩家ID
            _gameWorldBridge.Game.SetLocalPlayerId(data.CampId);
            
            // 处理创世阶段 - 初始化游戏世界
            if (data.InitialState != null)
            {
                _gameWorldBridge.Game.InitializeWorldFromMatchData(data.InitialState);
            }
            
            // 发送准备就绪消息
            _client.SendReady();
        };
    }

    public void SendCommandToServer(ICommand command)
    {
        _client.SendFrameInput(command.ExecuteFrame, command);
    }
}