// GMManager.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZLockstep.Simulation;
using ZLockstep.Sync.Command.Commands;
using zUnity;

public class GMManager
{
    // 存储指令和对应方法的字典
    private readonly Dictionary<string, Action<string[]>> _commandDictionary = new(StringComparer.OrdinalIgnoreCase);

    private readonly zWorld _world;

    public List<string> LogHistory = new();
    
    public GMManager(zWorld world)
    {
        _world = world;
        RegisterCommands(); // 注册所有指令
    }

    // 注册指令的方法
    private void RegisterCommands()
    {
        _commandDictionary.Clear();
        
        // 使用反射注册所有带有GMCommandAttribute的方法
        MethodInfo[] methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        
        foreach (MethodInfo method in methods)
        {
            GMCommandAttribute attribute = method.GetCustomAttribute<GMCommandAttribute>();
            if (attribute != null)
            {
                string commandName = attribute.CommandName;
                Action<string[]> action = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), this, method);
                _commandDictionary[commandName] = action;
            }
        }
        
        // 特殊处理help命令，因为它是动态生成的
        _commandDictionary.Add("help", (args) => { AddLog("Available Commands: " + string.Join(", ", _commandDictionary.Keys)); });
    }

    // 执行指令的核心方法
    public void ExecuteCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            AddLog("Invalid Command: Empty");
            return;
        }

        AddLog($"> {commandLine}"); // 记录输入

        // 解析指令，例如 "additem 1 5" -> 命令是 "additem", 参数是 ["1", "5"]
        string[] parts = commandLine.Split(' ');
        string command = parts[0].ToLower();

        if (_commandDictionary.ContainsKey(command))
        {
            try
            {
                // 执行指令对应的Action，并传入参数（parts[1..]）
                _commandDictionary[command]?.Invoke(parts[1..]);
                AddLog($"Command '{command}' executed successfully.");
            }
            catch (Exception e)
            {
                AddLog($"Error executing '{command}': {e.Message}");
            }
        }
        else
        {
            AddLog($"Command not found: {command}. Type 'help' for list.");
        }
    }

    private void AddLog(string log)
    {
        LogHistory.Add($"GM: {log}");
        zUDebug.Log($"[GM] {log}"); // 同时输出到Unity控制台
    }

    // ========== 具体的GM指令方法 ==========
    [GMCommand("additem")]
    private void AddItem(string[] args)
    {
        if (args.Length < 2)
        {
            AddLog("Usage: additem <itemId> <amount>");
            return;
        }
        int itemId = int.Parse(args[0]);
        int amount = int.Parse(args[1]);
        // 调用你的游戏系统，例如：InventoryManager.Instance.AddItem(itemId, amount);
        AddLog($"GM: Added item {itemId} x {amount}");
    }

    [GMCommand("setlevel")]
    private void SetLevel(string[] args)
    {
        if (args.Length < 1)
        {
            AddLog("Usage: setlevel <level>");
            return;
        }
        int level = int.Parse(args[0]);
        // 调用你的游戏系统，例如：PlayerExperience.Instance.SetLevel(level);
        AddLog($"GM: Set level to {level}");
    }

    [GMCommand("god")]
    private void ToggleGodMode(string[] args)
    {
        // 切换无敌模式
        // bool isGodMode = !PlayerStats.Instance.isInvincible;
        // PlayerStats.Instance.isInvincible = isGodMode;
        AddLog($"GM: God Mode Toggled");
    }

    [GMCommand("addmoney")]
    private void AddMoney(string[] args)
    {
        if (args.Length < 1)
        {
            AddLog("Usage: addmoney <amount>");
            return;
        }
        int amount = int.Parse(args[0]);
        // CurrencyManager.Instance.AddCurrency(amount);
        AddLog($"GM: Added money: {amount}");
    }

    [GMCommand("addtank")]
    private void AddTank(string[] args)
    {
        // 获取BattleGame实例
        if (_world == null)
        {
            AddLog("Error: BattleGame instance not found");
            return;
        }

        // 获取屏幕中心位置的世界坐标
        Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        Ray ray = Camera.main.ScreenPointToRay(screenCenter);
        
        // 创建一个平面（y=0）来接收射线
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float distance;
        Vector3 worldPosition;
        
        if (groundPlane.Raycast(ray, out distance))
        {
            worldPosition = ray.GetPoint(distance);
        }
        else
        {
            // 如果射线未击中平面，使用默认位置
            worldPosition = new Vector3(128, 0, 128); // 地图中心
            AddLog("Warning: Could not raycast to ground, using default position");
        }

        // 默认参数
        int playerId = 0; // 默认为玩家0（本地玩家）

        // 解析参数
        if (args.Length >= 1)
        {
            int.TryParse(args[0], out playerId);
        }

        CreateTankCommand createTankCommand = new(
            playerId: playerId,
            unitType: 2, // 坦克类型
            position: new zVector3((zfloat)worldPosition.x, (zfloat)worldPosition.y, (zfloat)worldPosition.z),
            prefabId: 2, // 坦克预制体ID
            radius: (zfloat)2,
            maxSpeed: (zfloat)10
        );

        _world.GameInstance.SubmitCommand(createTankCommand);

        AddLog($"GM: Added tank at center position ({worldPosition.x:F2}, {worldPosition.z:F2}) for player {playerId}");
    }
}