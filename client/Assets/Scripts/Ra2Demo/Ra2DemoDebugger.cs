using UnityEngine;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using zUnity;
using ZLockstep.View;
using Game.Examples;
using ZLockstep.Simulation;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;
using ZFrame;

/// <summary>
/// 调试可视化类，负责处理Ra2Demo中的OnGUI和OnDrawGizmos调试功能
/// </summary>
public class Ra2DemoDebugger : MonoBehaviour
{
    [Header("可视化设置")]
    public bool showUnits = true;          // 显示单位
    public bool showRVOAgents = true;      // 显示RVO智能体
    public bool showGrid = true;           // 显示网格
    public bool showObstacles = true;      // 显示障碍物
    public bool showFlowField = true;      // 显示流场方向
    public bool showSimulationInfo = true; // 显示仿真信息（包括zTime帧率）

    private Ra2Demo _demo;
    private Vector3 _lastClickPosition;
    private float _gizmoDisplayTime = 2f;
    private float _lastClickTime;
    
    // zTime FPS计算相关变量
    private float _zTimeAccumulatedTime = 0f;
    private int _zTimeAccumulatedTicks = 0;
    private float _zTimeFps = 0f;

    private void Awake()
    {
        _demo = GetComponent<Ra2Demo>();
    }

    private void OnDrawGizmos()
    {
        // 只在编辑器模式下或者运行时附加了该脚本的情况下工作
        if (!Application.isPlaying && _demo == null)
            return;

        var game = _demo?.GetBattleGame();
        
        // 显示最后点击位置
        if (Time.time - _lastClickTime < _gizmoDisplayTime)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_lastClickPosition, 0.5f);
            Gizmos.DrawLine(_lastClickPosition, _lastClickPosition + Vector3.up * 2f);
        }

        // 显示所有已创建的单位（从逻辑层读取）
        if (showUnits)
        {
            DrawUnits(game);
        }
        
        // 绘制RVO agents
        if (showRVOAgents)
        {
            DrawRVOAgents(game);
        }
        
        // 绘制网格、障碍物和流场
        if (game != null && game.MapManager != null)
        {
            // 1. 绘制网格和障碍物
            if (showGrid || showObstacles)
            {
                DrawGridAndObstacles(game);
            }

            // 2. 绘制流场
            if (showFlowField && game.FlowFieldManager != null)
            {
                DrawFlowFields(game);
            }
        }
    }

    /// <summary>
    /// 绘制所有单位
    /// </summary>
    private void DrawUnits(BattleGame game)
    {
        if (game != null && game.World != null)
        {
            var transforms = game.World.ComponentManager
                .GetAllEntityIdsWith<TransformComponent>();

            foreach (var entityId in transforms)
            {
                var entity = new Entity(entityId);
                var transform = game.World.ComponentManager
                    .GetComponent<TransformComponent>(entity);

                // 绘制单位位置
                Vector3 pos = transform.Position.ToVector3();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);

                // 如果这是选中的单位，用不同颜色标记
                // 注意：由于访问私有变量限制，这里简化处理
                // 实际应用中可能需要通过其他方式传递选中单位信息

                // 如果有移动命令，绘制目标位置
                if (game.World.ComponentManager
                    .HasComponent<MoveCommandComponent>(entity))
                {
                    var moveCmd = game.World.ComponentManager
                        .GetComponent<MoveCommandComponent>(entity);
                    
                    Vector3 targetPos = moveCmd.TargetPosition.ToVector3();
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(targetPos, 0.3f);
                    Gizmos.DrawLine(pos, targetPos);
                }
            }
        }
    }

    /// <summary>
    /// 绘制RVO agents用于调试
    /// </summary>
    private void DrawRVOAgents(BattleGame game)
    {
        if (game == null || game.RvoSimulator == null)
            return;

        var agents = game.RvoSimulator.GetAgents();
        if (agents == null)
            return;

        foreach (var agent in agents)
        {
            // 获取agent位置
            Vector3 pos = new Vector3((float)agent.position.x, 0.1f, (float)agent.position.y);
            
            // 绘制agent半径（白色）
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(pos, (float)agent.radius);
            
            // 绘制agent位置（红色球体）
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pos, 0.1f);
            
            // 绘制agent速度（蓝色箭头）
            if (agent.velocity.sqrMagnitude > zfloat.Epsilon)
            {
                Vector3 velocity = new Vector3((float)agent.velocity.x, 0, (float)agent.velocity.y);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(pos, pos + velocity);
                // 绘制箭头头部
                DrawArrowHead(pos + velocity, velocity.normalized, 0.3f);
            }
            
            // 绘制agent ID
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(pos + Vector3.up * 0.5f, $"ID: {agent.id}");
            #endif
        }
    }

    /// <summary>
    /// 绘制箭头头部
    /// </summary>
    /// <param name="position">箭头尖端位置</param>
    /// <param name="direction">箭头方向</param>
    /// <param name="size">箭头大小</param>
    private void DrawArrowHead(Vector3 position, Vector3 direction, float size)
    {
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 30, 0) * new Vector3(0, 0, 1);
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 30, 0) * new Vector3(0, 0, 1);
        Gizmos.DrawLine(position, position + right * size);
        Gizmos.DrawLine(position, position + left * size);
    }

    /// <summary>
    /// 绘制网格和障碍物
    /// </summary>
    private void DrawGridAndObstacles(BattleGame game)
    {
        if (game == null || game.MapManager == null)
            return;

        int width = game.MapManager.GetWidth();
        int height = game.MapManager.GetHeight();
        float gridSize = (float)game.MapManager.GetGridSize();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool walkable = game.MapManager.IsWalkable(x, y);
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
    private void DrawFlowFields(BattleGame game)
    {
        if (game == null || game.FlowFieldManager == null)
            return;

        // 获取活跃的流场
        var activeFields = game.FlowFieldManager.GetActiveFields();

        if (activeFields == null)
            return;

        float gridSize = (float)game.MapManager.GetGridSize();
        int flowFieldDisplayInterval = 2; // 间隔显示，避免太密集

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
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPos, 1f);
            Gizmos.DrawSphere(targetPos, 0.3f);

            // 绘制流场箭头（间隔显示，避免太密集）
            for (int y = 0; y < field.height; y += flowFieldDisplayInterval)
            {
                for (int x = 0; x < field.width; x += flowFieldDisplayInterval)
                {
                    if (!game.MapManager.IsWalkable(x, y))
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
                    DrawArrow(worldPos, dir * 0.8f);
                }
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

    private void Update()
    {
        // 处理快捷键输入
        HandleDebugUIHotkey();
    }

    private void OnGUI()
    {
        // 仅在编辑器状态下显示调试 UI
        if (!Application.isEditor)
            return;
            
        // 如果 Ra2Demo 不存在或游戏尚未准备好，则不显示调试 UI
        if (_demo == null)
            return;

        var game = _demo.GetBattleGame();
        if (game == null)
            return;

        // 根据开关状态决定是否绘制
        int type = _DebugType % 3;
        if (type == 0)
        {
            DrawHelpInfo();
        }
        else if (type == 1)
        {
            DrawDebugUI(game);
        }
        else if (type == 2)
        {
            DrawUnitStatisticsPanel(game);
        }
    }

    /// <summary>
    /// 处理调试 UI 的快捷键 (` 键)
    /// </summary>
    private void HandleDebugUIHotkey()
    {
        // 只在按键按下的瞬间触发 (从 false 变为 true) 
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            _showDebugUI = !_showDebugUI;
            _DebugType += 1;
            Debug.Log($"[Ra2DemoDebugger] Debug UI 显示状态：{_showDebugUI}");
        }
    }

    private bool _showDebugUI = true; // ` 开关控制
    private int _DebugType = 0;

    /// <summary>
    /// 绘制调试 UI 面板
    /// </summary>
    private void DrawDebugUI(BattleGame game)
    {
        Rect toggleRect = new Rect(20, 500, 200, 350);
        // 显示调试显示开关
        GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
        toggleStyle.fontSize = 16;
        toggleStyle.normal.textColor = Color.green;
        // 选中颜色
        toggleStyle.onNormal.textColor = Color.red;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.yellow;
        
        GUILayout.BeginArea(toggleRect);
        
        DrawDebugToggles(toggleStyle);
        
        DrawDebugInfoLabels(game, labelStyle);
        
        GUILayout.EndArea();
    }

    /// <summary>
    /// 绘制单位统计信息面板（独立面板，type==2 时显示）
    /// </summary>
    private void DrawUnitStatisticsPanel(BattleGame game)
    {
        Rect statsRect = new Rect(20, 500, 300, 400);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.normal.textColor = Color.cyan;
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 20;
        titleStyle.normal.textColor = Color.yellow;
        titleStyle.fontStyle = FontStyle.Bold;
        
        GUILayout.BeginArea(statsRect);
        
        GUILayout.Label("=== 单位统计面板 ===", titleStyle);
        GUILayout.Space(15);
        
        DrawUnitStatistics(game, labelStyle);
        
        GUILayout.EndArea();
    }

    /// <summary>
    /// 绘制调试开关控件
    /// </summary>
    private void DrawDebugToggles(GUIStyle toggleStyle)
    {
        showUnits = GUILayout.Toggle(showUnits, "显示单位", toggleStyle);
        showRVOAgents = GUILayout.Toggle(showRVOAgents, "显示 RVO 智能体", toggleStyle);
        
        showGrid = GUILayout.Toggle(showGrid, "显示网格", toggleStyle);
        showObstacles = GUILayout.Toggle(showObstacles, "显示障碍物", toggleStyle);
        showFlowField = GUILayout.Toggle(showFlowField, "显示流场", toggleStyle);
        showSimulationInfo = GUILayout.Toggle(showSimulationInfo, "显示仿真信息", toggleStyle);
    }

    /// <summary>
    /// 绘制调试信息标签（流场数量、RVO 智能体数量、仿真信息等）
    /// </summary>
    private void DrawDebugInfoLabels(BattleGame game, GUIStyle labelStyle)
    {
        // 显示流场数量
        if (showFlowField && game.FlowFieldManager != null)
        {
            int flowFieldCount = game.FlowFieldManager.GetActiveFieldCount();
            GUILayout.Label($"流场数量：{flowFieldCount}", labelStyle);
        }
        
        // 显示 RVO agents 数量
        if (showRVOAgents && game.RvoSimulator != null)
        {
            int rvoAgentCount = game.RvoSimulator.GetNumAgents();
            GUILayout.Label($"RVO 智能体数量：{rvoAgentCount}", labelStyle);
        }

        // 显示仿真信息（包括 zTime 帧率）
        if (showSimulationInfo && game.World != null && game.World.TimeManager != null)
        {
            DrawSimulationInfo(game.World.TimeManager, labelStyle);
        }
    }

    /// <summary>
    /// 绘制仿真信息（zTime Tick、时间、DeltaTim e、FPS 等）
    /// </summary>
    private void DrawSimulationInfo(ZLockstep.Simulation.TimeManager timeManager, GUIStyle labelStyle)
    {
        // 计算 zTime 的 FPS
        _zTimeAccumulatedTime += (float)timeManager.DeltaTime;
        _zTimeAccumulatedTicks++;
        
        if (_zTimeAccumulatedTime >= 0.5f) // 每 0.5 秒更新一次 FPS 显示
        {
            _zTimeFps = _zTimeAccumulatedTicks / _zTimeAccumulatedTime;
            _zTimeAccumulatedTime = 0f;
            _zTimeAccumulatedTicks = 0;
        }
        
        GUILayout.Label($"zTime Tick: {timeManager.Tick}", labelStyle);
        GUILayout.Label($"zTime 时间：{(float)timeManager.Time:F2}s", labelStyle);
        GUILayout.Label($"zTime DeltaTime: {(float)timeManager.DeltaTime:F3}s", labelStyle);
        GUILayout.Label($"zTime FPS: {_zTimeFps:F1}", labelStyle);
    }

    /// <summary>
    /// 绘制单位统计信息（每种 Unit 类型的数量）
    /// </summary>
    private void DrawUnitStatistics(BattleGame game, GUIStyle labelStyle)
    {
        if (game == null || game.World == null)
            return;

        // 统计每种 UnitType 的数量
        Dictionary<UnitType, int> unitTypeCounts = new Dictionary<UnitType, int>();

        var entities = game.World.ComponentManager.GetAllEntityIdsWith<TransformComponent>();

        foreach (var entityId in entities)
        {
            var entity = new Entity(entityId);
            
            // 检查是否有 UnitComponent
            if (!game.World.ComponentManager.HasComponent<UnitComponent>(entity))
                continue;

            var unitComponent = game.World.ComponentManager.GetComponent<UnitComponent>(entity);
            UnitType unitType = unitComponent.UnitType;

            if (unitTypeCounts.ContainsKey(unitType))
            {
                unitTypeCounts[unitType]++;
            }
            else
            {
                unitTypeCounts[unitType] = 1;
            }
        }

        // 绘制统计信息
        GUILayout.Space(10);
        GUILayout.Label("=== 单位统计 ===", labelStyle);
        
        foreach (var kvp in unitTypeCounts)
        {
            string unitName = GetUnitTypeName(kvp.Key);
            GUILayout.Label($"{unitName}: {kvp.Value}", labelStyle);
        }

        // 显示总数
        int totalCount = unitTypeCounts.Values.Sum();
        GUILayout.Label($"单位总数：{totalCount}", labelStyle);
    }

    /// <summary>
    /// 获取 UnitType 的中文名称（从 ConfUnit 配置表读取）
    /// </summary>
    private string GetUnitTypeName(UnitType unitType)
    {
        if (unitType == UnitType.None)
            return "无效单位";
        
        // 根据 UnitType 查找对应的 ConfUnitID
        // 需要遍历配置表找到匹配的 Type
        var allUnits = ConfigManager.GetAll<ConfUnit>();
        foreach (var confUnit in allUnits)
        {
            // 匹配单位类型
            if ((int)unitType == confUnit.Type)
            {
                return confUnit.Name;
            }
        }
        
        // 如果配置表中找不到，返回默认名称
        switch (unitType)
        {
            case UnitType.Infantry:
                return "动员兵";
            case UnitType.badgerTank:
                return "獾式坦克";
            case UnitType.grizzlyTank:
                return "灰熊坦克";
            case UnitType.Harvester:
                return "矿车";
            case UnitType.Projectile:
                return "弹丸";
            default:
                return $"未知类型 ({unitType})";
        }
    }

    /// <summary>
    /// 绘制帮助信息面板
    /// </summary>
    private void DrawHelpInfo()
    {
        Rect helpRect = new Rect(20, 500, 400, 350);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.normal.textColor = Color.yellow;
        titleStyle.fontStyle = FontStyle.Bold;
        
        GUILayout.BeginArea(helpRect);
        
        GUILayout.Label("=== 调试控制台帮助 ===", titleStyle);
        GUILayout.Space(10);
        
        GUILayout.Label("快捷键说明:", labelStyle);
        GUILayout.Label("` 键 - 切换调试 UI 显示/隐藏", labelStyle);
        GUILayout.Space(5);
        
        GUILayout.Label("调试类型切换:", labelStyle);
        GUILayout.Label("按 ` 键循环切换以下模式:", labelStyle);
        GUILayout.Label("  Type 0 - 帮助信息", labelStyle);
        GUILayout.Label("  Type 1 - 调试 UI (开关控制)", labelStyle);
        GUILayout.Label("  Type 2 - 单位统计面板", labelStyle);
        GUILayout.Space(5);
        
        GUILayout.EndArea();
    }

}
