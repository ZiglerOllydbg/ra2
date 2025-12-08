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
    }

    /// <summary>
    /// 游戏开始事件处理
    /// </summary>
    private void OnGameStart()
    {
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

    public void SendCommandToServer(ICommand command)
    {
        _client.SendFrameInput(command.ExecuteFrame, command);
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
        
    }
}
