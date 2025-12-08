using System;
using System.Collections.Generic;
using ZFrame;

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
                    MatchPanel.HideMatchingGroup();
                }
                break;
            case GameStartEvent e:
                {
                    // 匹配成功
                    zUDebug.Log("[Ra2Processor] 开始游戏");
                    MainPanel.Open();
                }
                break;
        }
    }
}
