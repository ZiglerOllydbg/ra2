using UnityEngine;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Flow;
using zUnity;

public class TestRVO : MonoBehaviour
{
    [Header("测试配置")]
    [SerializeField] private bool autoUpdate = false; // 自动更新模式
    [SerializeField] private float autoUpdateInterval = 0.033f; // 自动更新间隔（秒）

    [Header("可视化配置")]
    [SerializeField] private bool showGrid = true; // 显示网格
    [SerializeField] private bool showObstacles = true; // 显示障碍物
    [SerializeField] private bool showUnits = true; // 显示单位
    [SerializeField] private bool showFlowField = true; // 显示流场
    [SerializeField] private bool showTargets = true; // 显示目标点
    [SerializeField] private float unitSize = 0.5f; // 单位显示大小
    [SerializeField] private float flowFieldArrowSize = 0.4f; // 流场箭头大小
    [SerializeField] private int flowFieldDisplayInterval = 2; // 流场显示间隔（每N格显示一个箭头）

    private zWorld _world;
    private FlowFieldExample _flowFieldExample;
    private SimpleMapManager _mapManager;
    private FlowFieldManager _flowFieldManager;
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
        
        // 创建FlowFieldExample（它会自己创建管理器）
        _flowFieldExample = new FlowFieldExample();
        _flowFieldExample.Initialize(_world);
        
        // 通过反射获取内部的管理器引用（用于可视化）
        var exampleType = _flowFieldExample.GetType();
        _mapManager = exampleType.GetField("mapManager", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_flowFieldExample) as SimpleMapManager;
        _flowFieldManager = exampleType.GetField("flowFieldManager", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_flowFieldExample) as FlowFieldManager;
        
        _isInitialized = true;
        Debug.Log("[TestRVO] 系统初始化完成");
        Debug.Log($"[TestRVO] 地图大小: {_mapManager?.GetWidth()}x{_mapManager?.GetHeight()}");
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
            
            // 统计卡住单位
            int stuckCount = 0;
            var navigators = _world.ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
            foreach (var entityId in navigators)
            {
                Entity entity = new Entity(entityId);
                var nav = _world.ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
                if (nav.StuckFrames > 20)
                {
                    stuckCount++;
                }
            }
            
            if (stuckCount > 0)
            {
                GUILayout.Label($"⚠️ 卡住单位: {stuckCount}", new GUIStyle(_labelStyle) { normal = { textColor = Color.red } });
            }
            
            if (positions.Count > 0)
            {
                GUILayout.Label($"首个单位位置: {positions[0]}");
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(10);

            // ====== 可视化控制 ======
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("6. 可视化选项", _labelStyle);
            
            showGrid = GUILayout.Toggle(showGrid, " 显示网格");
            showObstacles = GUILayout.Toggle(showObstacles, " 显示障碍物");
            showUnits = GUILayout.Toggle(showUnits, " 显示单位");
            showFlowField = GUILayout.Toggle(showFlowField, " 显示流场方向");
            showTargets = GUILayout.Toggle(showTargets, " 显示目标点");
            
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

    // ===============================================
    // 可视化绘制
    // ===============================================

    private void OnDrawGizmos()
    {
        if (!_isInitialized || _mapManager == null || _world == null)
            return;

        // 1. 绘制网格和障碍物
        if (showGrid || showObstacles)
        {
            DrawGridAndObstacles();
        }

        // 2. 绘制流场
        if (showFlowField && _flowFieldManager != null)
        {
            DrawFlowFields();
        }

        // 3. 绘制单位
        if (showUnits)
        {
            DrawUnits();
        }

        // 4. 绘制目标点
        if (showTargets)
        {
            DrawTargets();
        }
    }

    /// <summary>
    /// 绘制网格和障碍物
    /// </summary>
    private void DrawGridAndObstacles()
    {
        int width = _mapManager.GetWidth();
        int height = _mapManager.GetHeight();
        float gridSize = (float)_mapManager.GetGridSize();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool walkable = _mapManager.IsWalkable(x, y);
                Vector3 worldPos = new Vector3(
                    x * gridSize + gridSize * 0.5f,
                    0.01f,
                    y * gridSize + gridSize * 0.5f
                );

                // 绘制障碍物
                if (!walkable && showObstacles)
                {
                    Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.7f);
                    Gizmos.DrawCube(worldPos, new Vector3(gridSize * 0.95f, 0.2f, gridSize * 0.95f));
                }
                // 绘制网格线
                else if (showGrid)
                {
                    Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                    // 只绘制底部和左边线，避免重复
                    Vector3 bottomLeft = worldPos - new Vector3(gridSize * 0.5f, 0, gridSize * 0.5f);
                    Vector3 bottomRight = bottomLeft + new Vector3(gridSize, 0, 0);
                    Vector3 topLeft = bottomLeft + new Vector3(0, 0, gridSize);
                    
                    Gizmos.DrawLine(bottomLeft, bottomRight);
                    Gizmos.DrawLine(bottomLeft, topLeft);
                }
            }
        }

        // 绘制地图边界
        if (showGrid)
        {
            Gizmos.color = Color.yellow;
            float mapWidth = width * gridSize;
            float mapHeight = height * gridSize;
            
            Vector3 corner1 = new Vector3(0, 0.02f, 0);
            Vector3 corner2 = new Vector3(mapWidth, 0.02f, 0);
            Vector3 corner3 = new Vector3(mapWidth, 0.02f, mapHeight);
            Vector3 corner4 = new Vector3(0, 0.02f, mapHeight);
            
            Gizmos.DrawLine(corner1, corner2);
            Gizmos.DrawLine(corner2, corner3);
            Gizmos.DrawLine(corner3, corner4);
            Gizmos.DrawLine(corner4, corner1);
        }
    }

    /// <summary>
    /// 绘制流场方向
    /// </summary>
    private void DrawFlowFields()
    {
        if (_flowFieldManager == null)
            return;

        // 获取活跃的流场
        var activeFields = _flowFieldManager.GetType()
            .GetField("flowFields", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_flowFieldManager) as System.Collections.Generic.Dictionary<int, FlowField>;

        if (activeFields == null)
            return;

        float gridSize = (float)_mapManager.GetGridSize();

        foreach (var field in activeFields.Values)
        {
            if (field.referenceCount <= 0)
                continue;

            // 绘制目标点
            Vector3 targetPos = new Vector3(
                field.targetGridX * gridSize + gridSize * 0.5f,
                0.5f,
                field.targetGridY * gridSize + gridSize * 0.5f
            );
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPos, 1f);
            Gizmos.DrawSphere(targetPos, 0.3f);

            // 绘制流场箭头（间隔显示，避免太密集）
            for (int y = 0; y < field.height; y += flowFieldDisplayInterval)
            {
                for (int x = 0; x < field.width; x += flowFieldDisplayInterval)
                {
                    if (!_mapManager.IsWalkable(x, y))
                        continue;

                    int idx = field.GetIndex(x, y);
                    zVector2 direction = field.directions[idx];
                    
                    if (direction.sqrMagnitude < new zfloat(0, 100)) // 0.01
                        continue;

                    Vector3 worldPos = new Vector3(
                        x * gridSize + gridSize * 0.5f,
                        0.3f,
                        y * gridSize + gridSize * 0.5f
                    );

                    Vector3 dir = new Vector3((float)direction.x, 0, (float)direction.y);
                    
                    // 根据到目标的距离设置颜色
                    float cost = (float)field.costs[idx];
                    float maxCost = field.width + field.height;
                    float t = 1f - (cost / maxCost);
                    Gizmos.color = Color.Lerp(Color.red, Color.green, t);
                    
                    // 绘制方向箭头
                    DrawArrow(worldPos, dir * flowFieldArrowSize);
                }
            }
        }
    }

    /// <summary>
    /// 绘制单位
    /// </summary>
    private void DrawUnits()
    {
        var entities = _world.ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
        
        foreach (var entityId in entities)
        {
            Entity entity = new Entity(entityId);
            
            // 获取位置
            if (!_world.ComponentManager.HasComponent<TransformComponent>(entity))
                continue;
                
            var transform = _world.ComponentManager.GetComponent<TransformComponent>(entity);
            Vector3 pos = new Vector3(
                (float)transform.Position.x,
                0.5f,
                (float)transform.Position.z
            );

            // 获取导航状态
            var navigator = _world.ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            
            // 根据状态设置颜色
            if (navigator.HasReachedTarget)
            {
                Gizmos.color = Color.green; // 已到达
            }
            else if (!navigator.IsEnabled)
            {
                Gizmos.color = Color.gray; // 未启用
            }
            else if (navigator.StuckFrames > 20)
            {
                Gizmos.color = Color.red; // 卡住状态（警告）
            }
            else if (navigator.StuckFrames > 10)
            {
                Gizmos.color = Color.yellow; // 可能卡住
            }
            else
            {
                Gizmos.color = Color.cyan; // 正常移动中
            }

            // 绘制单位（球体）
            Gizmos.DrawSphere(pos, unitSize * 0.5f);
            
            // 如果卡住，绘制警告标记
            if (navigator.StuckFrames > 10)
            {
                Gizmos.color = Color.red;
                // 绘制X标记
                float size = unitSize * 1.5f;
                Gizmos.DrawLine(pos + new Vector3(-size, 0, -size), pos + new Vector3(size, 0, size));
                Gizmos.DrawLine(pos + new Vector3(-size, 0, size), pos + new Vector3(size, 0, -size));
                
                // 在上方显示卡住帧数
                Vector3 textPos = pos + Vector3.up * 1.5f;
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(textPos, $"卡住: {navigator.StuckFrames}帧");
                #endif
            }
            
            // 绘制单位半径（线框）
            Color radiusColor = navigator.StuckFrames > 20 ? Color.red : 
                               navigator.StuckFrames > 10 ? Color.yellow : 
                               new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            Gizmos.color = radiusColor;
            Gizmos.DrawWireSphere(pos, (float)navigator.Radius);

            // 绘制速度方向
            if (_world.ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                var velocity = _world.ComponentManager.GetComponent<VelocityComponent>(entity);
                Vector3 vel = new Vector3(
                    (float)velocity.Value.x,
                    0,
                    (float)velocity.Value.z
                );
                
                if (vel.magnitude > 0.01f)
                {
                    Gizmos.color = Color.blue;
                    DrawArrow(pos, vel * 0.5f);
                }
            }
        }
    }

    /// <summary>
    /// 绘制目标点
    /// </summary>
    private void DrawTargets()
    {
        var entities = _world.ComponentManager.GetAllEntityIdsWith<MoveTargetComponent>();
        
        foreach (var entityId in entities)
        {
            Entity entity = new Entity(entityId);
            var moveTarget = _world.ComponentManager.GetComponent<MoveTargetComponent>(entity);
            
            if (!moveTarget.HasTarget)
                continue;

            Vector3 targetPos = new Vector3(
                (float)moveTarget.TargetPosition.x,
                0.1f,
                (float)moveTarget.TargetPosition.y
            );

            // 绘制目标标记
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPos, 0.5f);
            Gizmos.DrawLine(targetPos + Vector3.up * 0.5f, targetPos + Vector3.up * 1.5f);
            
            // 绘制从单位到目标的连线
            if (_world.ComponentManager.HasComponent<TransformComponent>(entity))
            {
                var transform = _world.ComponentManager.GetComponent<TransformComponent>(entity);
                Vector3 unitPos = new Vector3(
                    (float)transform.Position.x,
                    0.3f,
                    (float)transform.Position.z
                );
                
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawLine(unitPos, targetPos);
            }
        }
    }

    /// <summary>
    /// 绘制箭头（辅助方法）
    /// </summary>
    private void DrawArrow(Vector3 start, Vector3 direction)
    {
        if (direction.magnitude < 0.001f)
            return;

        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // 箭头头部
        Vector3 right = Quaternion.Euler(0, 30, 0) * -direction * 0.3f;
        Vector3 left = Quaternion.Euler(0, -30, 0) * -direction * 0.3f;
        
        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);
    }
}