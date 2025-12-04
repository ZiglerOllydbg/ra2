using UnityEngine;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using zUnity;
using ZLockstep.View;
using Game.Examples;

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

    private Ra2Demo _demo;
    private Vector3 _lastClickPosition;
    private float _gizmoDisplayTime = 2f;
    private float _lastClickTime;

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

    private void OnGUI()
    {
        // 仅在编辑器状态下显示调试UI
        if (!Application.isEditor)
            return;
            
        // 如果Ra2Demo不存在或游戏尚未准备好，则不显示调试UI
        if (_demo == null)
            return;

        var game = _demo.GetBattleGame();
        if (game == null)
            return;

        // 显示调试显示开关
        GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
        toggleStyle.fontSize = 16;
        toggleStyle.normal.textColor = Color.green;
        // 选中颜色
        toggleStyle.onNormal.textColor = Color.red;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;
        
        Rect toggleRect = new Rect(20, 130, 200, 150);
        GUILayout.BeginArea(toggleRect);
        showUnits = GUILayout.Toggle(showUnits, "显示单位", toggleStyle);
        showRVOAgents = GUILayout.Toggle(showRVOAgents, "显示RVO智能体", toggleStyle);
        
        showGrid = GUILayout.Toggle(showGrid, "显示网格", toggleStyle);
        showObstacles = GUILayout.Toggle(showObstacles, "显示障碍物", toggleStyle);
        showFlowField = GUILayout.Toggle(showFlowField, "显示流场", toggleStyle);

        // 显示流场数量
        if (showFlowField && game.FlowFieldManager != null)
        {
            int flowFieldCount = game.FlowFieldManager.GetActiveFieldCount();
            GUILayout.Label($"流场数量: {flowFieldCount}", labelStyle);
        }
        
        // 显示RVO agents数量
        if (showRVOAgents && game.RvoSimulator != null)
        {
            int rvoAgentCount = game.RvoSimulator.GetNumAgents();
            GUILayout.Label($"RVO智能体数量: {rvoAgentCount}", labelStyle);
        }
        
        GUILayout.EndArea();
    }
}