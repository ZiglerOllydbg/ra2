using System;
using System.Collections.Generic;
using ZFrame;
using ZLib;
using ZLockstep.Simulation.ECS.Components; // 添加这个using语句以使用Tick

public class Ra2Processor : BaseProcessor
{
    private Ra2Demo _ra2Demo;

    private MatchPanel _matchPanel;
    public MatchPanel MatchPanel
    {
        get
        {
            if (_matchPanel == null)
            {
                // 使用字符串ID，业务层可以定义自己的枚举并通过ToString()转换
                _matchPanel = _matchPanel.New<MatchPanel>(this, "MatchPanel");
            }
            return _matchPanel;
        }
    }

    // loading 面板
    private LoadingPanel _loadingPanel;
    public LoadingPanel LoadingPanel
    {
        get
        {
            if (_loadingPanel == null)
            {
                // 使用字符串ID，业务层可以定义自己的枚举并通过ToString()转换
                _loadingPanel = _loadingPanel.New<LoadingPanel>(this, "LoadingPanel");
            }
            return _loadingPanel;
        }
    }

    private MainPanel _mainPanel;
    public MainPanel MainPanel
    {
        get
        {
            if (_mainPanel == null)
            {
                // 使用字符串ID，业务层可以定义自己的枚举并通过ToString()转换
                _mainPanel = _mainPanel.New<MainPanel>(this, "MainPanel");
            }
            return _mainPanel;
        }
    }

    // 结算面板
    private SettlePanel _settlePanel;
    public SettlePanel SettlePanel
    {
        get
        {
            if (_settlePanel == null)
            {
                _settlePanel = _settlePanel.New<SettlePanel>(this, "SettlePanel");
            }
            return _settlePanel;
        }
    }

    // 血量面板
    private HealthPanel _healthPanel;
    public HealthPanel HealthPanel
    {
        get
        {
            if (_healthPanel == null)
            {
                // 使用字符串ID，业务层可以定义自己的枚举并通过ToString()转换
                _healthPanel = _healthPanel.New<HealthPanel>(this, "HealthPanel");
            }
            return _healthPanel;
        }
    }

    public Ra2Processor(Module _module) : base(_module)
    {
    }

    protected override List<Type> ListenModuleEvents()
    {
        return new List<Type>()
        {
            typeof(Ra2StartUpEvent),
            typeof(MatchedEvent),
            typeof(GameStartEvent),
            typeof(EconomyEvent),
            typeof(SelectBuildingEvent),
            typeof(ShowMessageEvent),
            typeof(SettleEvent),
            typeof(RestartGameEvent),
            typeof(SoloGameStartEvent),
            typeof(ReplayGameStartEvent),
            typeof(HealthEvent)
        };
    }

    protected override void ReceivedMessage(object __msg)
    {
    }

    protected override void ReceiveModuleEvent(ModuleEvent __me)
    {
        switch (__me)
        {
            case Ra2StartUpEvent e:
                {
                    _ra2Demo = e.Ra2Demo;
                    // MainPanel.Open();
                    MatchPanel.Open();
                }
                break;
            case MatchedEvent e:
                {
                    // 匹配成功
                    zUDebug.Log("[Ra2Processor] 匹配成功");
                    MatchPanel.Close();

                    // 获取对战人数
                    LoadingPanel.Open();
                    int playerNum = e.data.InitialState.Count - 1;
                    LoadingPanel.SetProgress(0.5f);
                    LoadingPanel.SetPlayerCount(playerNum);
                    
                    // 延迟2秒后调用SendReady
                    Tick.SetTimeout(() => {
                        NetworkManager.Instance.CurrentWebSocket.SendReady();
                    }, 1.0f);
                }
                break;
            case GameStartEvent e:
                {
                    zUDebug.Log("[Ra2Processor] 开始游戏");
                    LoadingPanel.Close();

                    MainPanel.Ra2Demo = _ra2Demo;
                    MainPanel.Open();
                    
                    RefreshEconomy();
                }
                break;
            case SoloGameStartEvent:
                {
                    zUDebug.Log("[Ra2Processor]  solo 模式开始游戏");
                    MatchPanel.Close();

                    MainPanel.Ra2Demo = _ra2Demo;
                    MainPanel.Open();

                    RefreshEconomy();

                    HealthPanel.Open();
                    
                    // 添加测试血条代码
                    TestHealthBars();
                }
                break;
            case ReplayGameStartEvent:
                {
                    zUDebug.Log("[Ra2Processor]  replay 模式开始游戏");
                    MatchPanel.Close();

                    RefreshEconomy();
                }
                break;
            case EconomyEvent e:
                    RefreshEconomy();
                break;
            case SelectBuildingEvent e:
                _ra2Demo.StartBuildingPlacement(e.BuildingType);
                // 显示确认和取消按钮
                MainPanel.ShowConfirm();
                break;
            case ShowMessageEvent e:
                MainPanel.ShowMessage(e.Message);
                break;
            case SettleEvent e:
                MainPanel.Close();
                SettlePanel.Open();
                SettlePanel.SetResult(e.IsVictory);
                break;
            case RestartGameEvent e:
                _ra2Demo.RestartGame();
                MainPanel.Close();
                SettlePanel.Close();
                MatchPanel.Open();
                break;
            case HealthEvent e:
                {
                    // 根据IsVisible属性决定是否显示血量面板
                    if (e.IsVisible)
                    {
                        HealthPanel.Open();
                        // 更新指定实体的血量显示
                        HealthPanel.UpdateHealth(e.Id, e.IsSelf, e.CurrentHealth, e.MaxHealth);
                        HealthPanel.ShowHealthBar(e.Id);
                    }
                    else
                    {
                        // 隐藏指定实体的血量条
                        HealthPanel.HideHealthBar(e.Id);
                        // 如果没有可见的血量条，则关闭面板
                        // 这里可以根据实际需求决定是否关闭整个面板
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 刷新经济显示
    /// </summary>
    private void RefreshEconomy()
    {
        EcsUtils.GetLocalPlayerEconomy(_ra2Demo.GetBattleGame(), out int money, out int power);
        
        MainPanel.SetMoney(money);
        MainPanel.SetPower(power);
    }

    /// <summary>
    /// 测试血量条显示功能
    /// </summary>
    private void TestHealthBars()
    {
        // 延迟一段时间后开始测试，确保游戏初始化完成
        Tick.SetTimeout(() => {
            zUDebug.Log("[Ra2Processor] 开始测试血量条显示");
            
            // 测试己方血条 (ID: 1001, 绿色)
            HealthPanel.UpdateHealth(1001, true, 80, 100); // 80%血量
            HealthPanel.ShowHealthBar(1001);
            
            // 测试敌方血条 (ID: 2001, 红色)
            HealthPanel.UpdateHealth(2001, false, 60, 100); // 60%血量
            HealthPanel.ShowHealthBar(2001);
            
            // 测试低血量的己方单位 (ID: 1002, 绿色)
            HealthPanel.UpdateHealth(1002, true, 30, 100); // 30%血量
            HealthPanel.ShowHealthBar(1002);
            
            // 测试满血的敌方单位 (ID: 2002, 红色)
            HealthPanel.UpdateHealth(2002, false, 100, 100); // 100%血量
            HealthPanel.ShowHealthBar(2002);
            
            // 测试受伤严重的敌方单位 (ID: 2003, 红色)
            HealthPanel.UpdateHealth(2003, false, 15, 100); // 15%血量
            HealthPanel.ShowHealthBar(2003);
            
            zUDebug.Log("[Ra2Processor] 血量条测试完成");
            
            // 3秒后隐藏部分血条进行测试
            Tick.SetTimeout(() => {
                zUDebug.Log("[Ra2Processor] 隐藏部分测试血条");
                HealthPanel.HideHealthBar(1002); // 隐藏低血量的己方单位
                HealthPanel.HideHealthBar(2003); // 隐藏受伤严重的敌方单位
                
                // 2秒后再次显示这些血条
                Tick.SetTimeout(() => {
                    zUDebug.Log("[Ra2Processor] 重新显示隐藏的血条");
                    HealthPanel.ShowHealthBar(1002);
                    HealthPanel.ShowHealthBar(2003);
                    
                    // 再过2秒更新其中一个血条的血量
                    Tick.SetTimeout(() => {
                        zUDebug.Log("[Ra2Processor] 更新血条血量测试");
                        // 更新己方单位1001的血量到50%
                        HealthPanel.UpdateHealth(1001, true, 50, 100);
                        
                        // 更新敌方单位2001的血量到90%
                        HealthPanel.UpdateHealth(2001, false, 90, 100);
                    }, 2.0f);
                    
                }, 2.0f);
                
            }, 3.0f);
            
        }, 1.0f); // 1秒延迟开始测试
    }
}