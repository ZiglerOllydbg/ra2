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
    private WebSocketClient _client;

    private Ra2Demo _ra2Demo;

    public WebSocketNetworkAdaptor(Ra2Demo ra2Demo, WebSocketClient client)
    {
        _ra2Demo = ra2Demo; // 保存引用
        _client = client;

        // 绑定网络适配器
        _ra2Demo.GetBattleGame().FrameSyncManager.NetworkAdapter = this;
        _client.OnFrameSync += OnFrameSync(_ra2Demo.GetBattleGame());
    }

    public void SendCommandToServer(ICommand command)
    {
        _client.SendFrameInput(command.ExecuteFrame, command);
    }

    private static Action<FrameSyncData> OnFrameSync(ZLockstep.Sync.Game game)
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
}
