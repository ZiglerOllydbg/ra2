using System;
using System.Collections.Generic;
using ZFrame;
using ZLib; // 添加这个using语句以使用Tick

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
            typeof(EconomyEvent)
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
                    }, 2.0f);
                }
                break;
            case GameStartEvent e:
                {
                    // 匹配成功
                    zUDebug.Log("[Ra2Processor] 开始游戏");
                    LoadingPanel.Close();
                    MainPanel.Open();

                    RefreshEconomy();
                }
                break;
            case EconomyEvent e:
                    RefreshEconomy();
                break;
        }
    }

    /// <summary>
    /// 刷新经济显示
    /// </summary>
    private void RefreshEconomy()
    {
        Utils.GetLocalPlayerEconomy(_ra2Demo.GetBattleGame(), out int money, out int power);
        
        MainPanel.SetMoney(money);
        MainPanel.SetPower(power);
    }
}