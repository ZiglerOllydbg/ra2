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
    private readonly WebSocketClient _client;

    public WebSocketNetworkAdaptor(WebSocketClient client, GameWorldBridge gameWorldBridge)
    {
        _client = client;
        // OnFrameSync
        _client.OnFrameSync += OnFrameSync(gameWorldBridge);
        // 绑定网络适配器
        gameWorldBridge.Game.FrameSyncManager.NetworkAdapter = this;
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

    public void SendCommandToServer(ICommand command)
    {
        _client.SendFrameInput(command.ExecuteFrame, command);
    }
}