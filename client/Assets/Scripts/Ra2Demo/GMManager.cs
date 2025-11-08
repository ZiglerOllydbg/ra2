// GMManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using ZLockstep.Sync.Command.Commands;
using zUnity;

public class GMManager
{
    public static GMManager Instance;
    
    // 存储指令和对应方法的字典
    private Dictionary<string, Action<string[]>> _commandDictionary = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase);

    // 控制台UI相关（简易实现）
    public bool IsConsoleVisible = false;
    public string InputField = "";
    public Vector2 ScrollPosition;
    public List<string> LogHistory = new List<string>();
    
    private ZLockstep.Sync.Game _game;

    public GMManager(ZLockstep.Sync.Game game)
    {
        if (Instance == null)
        {
            Instance = this;
            _game = game;
            RegisterCommands(); // 注册所有指令
        }
    }

    // 注册指令的方法
    private void RegisterCommands()
    {
        _commandDictionary.Add("help", (args) => { AddLog("Available Commands: additem, setlevel, god, addmoney, addtank"); });
        _commandDictionary.Add("additem", AddItem);
        _commandDictionary.Add("setlevel", SetLevel);
        _commandDictionary.Add("god", ToggleGodMode);
        _commandDictionary.Add("addmoney", AddMoney);
        _commandDictionary.Add("addtank", AddTank);
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
        LogHistory.Add(log);
        Debug.Log($"[GM] {log}"); // 同时输出到Unity控制台
    }

    // ========== 具体的GM指令方法 ==========
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

    private void ToggleGodMode(string[] args)
    {
        // 切换无敌模式
        // bool isGodMode = !PlayerStats.Instance.isInvincible;
        // PlayerStats.Instance.isInvincible = isGodMode;
        AddLog($"GM: God Mode Toggled");
    }

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

    private void AddTank(string[] args)
    {
        // 获取BattleGame实例
        if (_game == null)
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

        _game.SubmitCommand(createTankCommand);

        AddLog($"GM: Added tank at center position ({worldPosition.x:F2}, {worldPosition.z:F2}) for player {playerId}");
    }
}