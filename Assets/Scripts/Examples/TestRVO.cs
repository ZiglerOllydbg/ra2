using UnityEngine;
using ZLockstep.Simulation;
using ZLockstep.Flow;
using zUnity;

public class TestRVO : MonoBehaviour
{
    [Header("测试配置")]
    [SerializeField] private bool autoUpdate = false; // 自动更新模式
    [SerializeField] private float autoUpdateInterval = 0.033f; // 自动更新间隔（秒）

    private zWorld _world;
    private FlowFieldExample _flowFieldExample;
    private bool _isInitialized = false;
    private float _lastAutoUpdateTime = 0f;
    
    // GUI样式
    private GUIStyle _buttonStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;

    private void Start()
    {
        Debug.Log("[TestRVO] 启动 - 使用GUI界面进行测试");
        InitializeGUIStyles();
    }

    /// <summary>
    /// 初始化GUI样式
    /// </summary>
    private void InitializeGUIStyles()
    {
        _buttonStyle = new GUIStyle();
        _labelStyle = new GUIStyle();
        _boxStyle = new GUIStyle();
    }

    /// <summary>
    /// 初始化流场系统
    /// </summary>
    private void InitializeWorld()
    {
        // 清理旧的世界
        if (_world != null)
        {
            _world.Shutdown();
        }

        // 创建新世界
        _world = new zWorld();
        _world.Init(frameRate: 30);
        
        _flowFieldExample = new FlowFieldExample();
        _flowFieldExample.Initialize(_world);
        
        _isInitialized = true;
        Debug.Log("[TestRVO] 系统初始化完成");
    }

    private void Update()
    {
        // 自动更新模式
        if (autoUpdate && _isInitialized && _world != null)
        {
            if (Time.time - _lastAutoUpdateTime >= autoUpdateInterval)
            {
                _world.Update();
                _lastAutoUpdateTime = Time.time;
            }
        }
    }

    private void OnGUI()
    {
        // 延迟初始化GUI样式（需要在OnGUI中）
        if (_buttonStyle.normal.background == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 12 };
        }

        GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));
        
        // ====== 标题 ======
        GUILayout.Label("流场系统测试面板", _labelStyle);
        GUILayout.Space(10);

        // ====== 初始化区域 ======
        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("1. 系统初始化", _labelStyle);
        
        if (!_isInitialized)
        {
            if (GUILayout.Button("初始化系统", _buttonStyle, GUILayout.Height(40)))
            {
                InitializeWorld();
            }
        }
        else
        {
            GUILayout.Label($"✓ 系统已初始化 (帧: {_world?.Tick ?? 0})");
            
            if (GUILayout.Button("重新初始化", _buttonStyle, GUILayout.Height(30)))
            {
                InitializeWorld();
            }
        }
        GUILayout.EndVertical();
        GUILayout.Space(10);

        // ====== 测试示例区域 ======
        if (_isInitialized)
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("2. 运行测试示例", _labelStyle);
            
            if (GUILayout.Button("示例1: 编队移动", _buttonStyle, GUILayout.Height(35)))
            {
                _flowFieldExample.Example1_SquadMovement();
                Debug.Log("[TestRVO] 已运行编队移动示例");
            }
            
            if (GUILayout.Button("示例2: 多目标导航", _buttonStyle, GUILayout.Height(35)))
            {
                _flowFieldExample.Example2_MultipleTargets();
                Debug.Log("[TestRVO] 已运行多目标导航示例");
            }
            
            if (GUILayout.Button("示例3: 动态障碍物", _buttonStyle, GUILayout.Height(35)))
            {
                _flowFieldExample.Example3_DynamicObstacles();
                Debug.Log("[TestRVO] 已运行动态障碍物示例");
            }
            
            if (GUILayout.Button("完整示例 (自动运行)", _buttonStyle, GUILayout.Height(35)))
            {
                _flowFieldExample.RunCompleteExample();
                Debug.Log("[TestRVO] 已运行完整示例");
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(10);

            // ====== 控制区域 ======
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("3. 帧更新控制", _labelStyle);
            
            // 自动更新开关
            bool newAutoUpdate = GUILayout.Toggle(autoUpdate, $" 自动更新 ({(int)(1f / autoUpdateInterval)}FPS)");
            if (newAutoUpdate != autoUpdate)
            {
                autoUpdate = newAutoUpdate;
                _lastAutoUpdateTime = Time.time;
                Debug.Log($"[TestRVO] 自动更新: {(autoUpdate ? "开启" : "关闭")}");
            }
            
            // 手动单步更新
            GUI.enabled = !autoUpdate;
            if (GUILayout.Button("单步更新 (1帧)", _buttonStyle, GUILayout.Height(35)))
            {
                _world.Update();
                LogCurrentState();
            }
            
            if (GUILayout.Button("快进 (10帧)", _buttonStyle, GUILayout.Height(35)))
            {
                for (int i = 0; i < 10; i++)
                {
                    _world.Update();
                }
                LogCurrentState();
            }
            GUI.enabled = true;
            
            GUILayout.EndVertical();
            GUILayout.Space(10);

            // ====== 动态障碍物测试 ======
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("4. 动态障碍物测试", _labelStyle);
            
            if (GUILayout.Button("建造建筑 (70,70)", _buttonStyle, GUILayout.Height(35)))
            {
                _flowFieldExample.BuildBuilding(70, 70, 15, 10);
                Debug.Log("[TestRVO] 已建造建筑");
            }
            
            if (GUILayout.Button("摧毁建筑 (70,70)", _buttonStyle, GUILayout.Height(35)))
            {
                _flowFieldExample.DestroyBuilding(70, 70, 15, 10);
                Debug.Log("[TestRVO] 已摧毁建筑");
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(10);

            // ====== 状态显示 ======
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("5. 当前状态", _labelStyle);
            
            var positions = _flowFieldExample.GetAllUnitPositions();
            GUILayout.Label($"当前帧数: {_world.Tick}");
            GUILayout.Label($"单位数量: {positions.Count}");
            
            if (positions.Count > 0)
            {
                GUILayout.Label($"首个单位位置: {positions[0]}");
            }
            
            GUILayout.EndVertical();
        }

        GUILayout.EndArea();
    }

    /// <summary>
    /// 输出当前状态
    /// </summary>
    private void LogCurrentState()
    {
        var positions = _flowFieldExample.GetAllUnitPositions();
        Debug.Log($"[TestRVO] 帧 {_world.Tick}: 单位数量={positions.Count}");
        
        if (positions.Count > 0)
        {
            // 输出第一个单位的位置作为示例
            Debug.Log($"  第一个单位位置: {positions[0]}");
        }
    }

    private void OnDestroy()
    {
        _world?.Shutdown();
        Debug.Log("[TestRVO] 清理完成");
    }
}