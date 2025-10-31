using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.View.Systems;
using ZLockstep.Flow;
using zUnity;
using Game.Examples;
using ZLockstep.View;

/// <summary>
/// 单机战斗Demo
/// 不使用GameWorldBridge，直接创建和管理BattleGame
/// 
/// 操作方式：
/// - 左键点击：在点击位置创建己方坦克（蓝色，阵营0）
/// - 右键点击：让所有选中的单位移动到目标位置
/// - Q键：在鼠标位置创建AI坦克（红色，阵营1）
/// - Space：切换自动更新模式
/// </summary>
public class StandaloneBattleDemo : MonoBehaviour
{
    [Header("游戏配置")]
    [SerializeField] private int logicFrameRate = 30;
    [SerializeField] private bool autoUpdate = true;

    [Header("单位配置")]
    [SerializeField] private zfloat tankRadius = new zfloat(0, 5000); // 0.5米
    [SerializeField] private zfloat tankMaxSpeed = new zfloat(3); // 3米/秒

    [Header("Unity资源")]
    [SerializeField] private Transform viewRoot;
    [SerializeField] private GameObject[] unitPrefabs = new GameObject[10];

    [Header("可视化")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private bool showObstacles = true;
    [SerializeField] private bool showHealthBars = true;
    [SerializeField] private bool showAttackRanges = false;
    [SerializeField] private LayerMask groundLayer = -1;

    // 核心：BattleGame实例
    private BattleGame _battleGame;
    private PresentationSystem _presentationSystem;
    private Camera _mainCamera;

    // 选中的单位列表
    private List<Entity> _selectedUnits = new List<Entity>();

    // GUI样式
    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _boxStyle;
    private Texture2D _backgroundTex;

    private void Awake()
    {
        _mainCamera = Camera.main;
        InitializeGame();
        InitializeUnityView();
    }

    /// <summary>
    /// 初始化游戏逻辑
    /// </summary>
    private void InitializeGame()
    {
        // 创建BattleGame（单机模式）
        _battleGame = new BattleGame(
            mode: ZLockstep.Sync.GameMode.Standalone,
            frameRate: logicFrameRate,
            localPlayerId: 0
        );

        _battleGame.Init();

        // 创建初始基地
        _battleGame.InitializeStartingUnits();

        Debug.Log("[StandaloneBattleDemo] 战斗游戏初始化完成");
    }

    /// <summary>
    /// 初始化Unity视图层
    /// </summary>
    private void InitializeUnityView()
    {
        if (viewRoot == null)
            viewRoot = transform;

        // 创建表现系统
        _presentationSystem = new PresentationSystem();
        _presentationSystem.EnableSmoothInterpolation = true;

        // 准备预制体字典
        var prefabDict = new Dictionary<int, GameObject>();
        for (int i = 0; i < unitPrefabs.Length; i++)
        {
            if (unitPrefabs[i] != null)
            {
                prefabDict[i] = unitPrefabs[i];
                Debug.Log($"[StandaloneBattleDemo] 注册预制体: Type{i} = {unitPrefabs[i].name}");
            }
        }

        // 初始化并注册表现系统
        _presentationSystem.Initialize(viewRoot, prefabDict);
        _presentationSystem.SetGame(_battleGame);
        _battleGame.World.SystemManager.RegisterSystem(_presentationSystem);

        Debug.Log("[StandaloneBattleDemo] 视图系统初始化完成");
    }

    private void FixedUpdate()
    {
        if (_battleGame != null && autoUpdate)
        {
            _battleGame.Update();
        }
    }

    private void Update()
    {
        HandleInput();

        // 插值更新
        if (_presentationSystem != null)
        {
            _presentationSystem.LerpUpdate(Time.deltaTime, 10f);
        }
    }

    /// <summary>
    /// 处理玩家输入
    /// </summary>
    private void HandleInput()
    {
        // 左键：创建己方坦克
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (TryGetGroundPosition(out Vector3 worldPos))
            {
                // 将 float 转换为 zfloat：先乘以 10000，转为 long，然后使用 CreateFloat
                zfloat x = zfloat.CreateFloat((long)(worldPos.x * zfloat.SCALE_10000));
                zfloat z = zfloat.CreateFloat((long)(worldPos.z * zfloat.SCALE_10000));
                CreateTank(campId: 0, position: new zVector2(x, z), prefabId: 2);
            }
        }

        // 右键：移动选中单位
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (TryGetGroundPosition(out Vector3 worldPos))
            {
                zfloat x = zfloat.CreateFloat((long)(worldPos.x * zfloat.SCALE_10000));
                zfloat z = zfloat.CreateFloat((long)(worldPos.z * zfloat.SCALE_10000));
                MoveSelectedUnits(new zVector2(x, z));
            }
        }

        // Q键：创建AI坦克
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (TryGetGroundPosition(out Vector3 worldPos))
            {
                zfloat x = zfloat.CreateFloat((long)(worldPos.x * zfloat.SCALE_10000));
                zfloat z = zfloat.CreateFloat((long)(worldPos.z * zfloat.SCALE_10000));
                CreateTank(campId: 1, position: new zVector2(x, z), prefabId: 3);
            }
        }

        // Space：切换自动更新
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            autoUpdate = !autoUpdate;
            Debug.Log($"[StandaloneBattleDemo] 自动更新: {autoUpdate}");
        }

        // M键：手动更新一帧
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (!autoUpdate)
            {
                _battleGame?.Update();
                Debug.Log($"[StandaloneBattleDemo] 手动更新帧: {_battleGame.World.Tick}");
            }
        }

        // A键：选中所有己方单位
        if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
        {
            SelectAllPlayerUnits();
        }

        // H键：切换生命值显示
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            showHealthBars = !showHealthBars;
            Debug.Log($"[StandaloneBattleDemo] 生命值显示: {showHealthBars}");
        }

        // R键：切换攻击范围显示
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            showAttackRanges = !showAttackRanges;
            Debug.Log($"[StandaloneBattleDemo] 攻击范围显示: {showAttackRanges}");
        }

        // E键：创建玩家防御塔（暂不实现，预留）
        // if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        // {
        //     if (TryGetGroundPosition(out Vector3 worldPos))
        //     {
        //         // TODO: 创建防御塔
        //     }
        // }
    }

    /// <summary>
    /// 创建坦克单位
    /// </summary>
    private void CreateTank(int campId, zVector2 position, int prefabId)
    {
        Entity entity = _battleGame.CreateTank(campId, position, prefabId, tankRadius, tankMaxSpeed);

        // 自动选中新创建的己方单位
        if (campId == 0)
        {
            _selectedUnits.Clear();
            _selectedUnits.Add(entity);
        }

        Debug.Log($"[StandaloneBattleDemo] 创建坦克: 阵营{campId}, 位置{position}");
    }

    /// <summary>
    /// 移动选中的单位
    /// </summary>
    private void MoveSelectedUnits(zVector2 targetPosition)
    {
        if (_selectedUnits.Count == 0)
        {
            // 如果没有选中单位，选中所有己方单位
            SelectAllPlayerUnits();
        }

        if (_selectedUnits.Count > 0)
        {
            _battleGame.NavSystem.SetMultipleTargets(_selectedUnits, targetPosition);
            Debug.Log($"[StandaloneBattleDemo] 移动{_selectedUnits.Count}个单位到{targetPosition}");
        }
    }

    /// <summary>
    /// 选中所有玩家单位
    /// </summary>
    private void SelectAllPlayerUnits()
    {
        _selectedUnits.Clear();

        var allEntities = _battleGame.World.ComponentManager.GetAllEntityIdsWith<CampComponent>();
        foreach (var entityId in allEntities)
        {
            Entity entity = new Entity(entityId);
            var camp = _battleGame.World.ComponentManager.GetComponent<CampComponent>(entity);

            // 只选中玩家阵营的单位
            if (camp.CampId == 0 && _battleGame.World.ComponentManager.HasComponent<FlowFieldNavigatorComponent>(entity))
            {
                _selectedUnits.Add(entity);
            }
        }

        Debug.Log($"[StandaloneBattleDemo] 选中{_selectedUnits.Count}个玩家单位");
    }

    /// <summary>
    /// 获取鼠标点击的地面位置
    /// </summary>
    private bool TryGetGroundPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (_mainCamera == null || Mouse.current == null)
            return false;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);

        // 尝试射线检测
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            worldPosition = hit.point;
            return true;
        }

        // 使用Y=0平面
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        _battleGame?.Shutdown();
        
        // 清理GUI纹理
        if (_backgroundTex != null)
        {
            Destroy(_backgroundTex);
        }
    }

    // ===== GUI显示 =====

    private void OnGUI()
    {
        // 初始化样式（只执行一次）
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            
            // 创建半透明背景
            _backgroundTex = MakeTex(2, 2, new Color(0, 0, 0, 0.7f));
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _backgroundTex;
        }
        
        // 创建一个更大的区域，并添加半透明背景
        GUILayout.BeginArea(new Rect(10, 10, 480, 420), _boxStyle);

        GUILayout.Label("=== 单机战斗Demo ===", _labelStyle);
        GUILayout.Space(3);

        if (_battleGame != null)
        {
            // 统计信息
            var (playerUnits, aiUnits, buildings) = GetUnitStatistics();
            
            GUIStyle normalStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            GUILayout.Label($"当前帧: {_battleGame.World.Tick}", normalStyle);
            GUILayout.Label($"玩家单位: {playerUnits} | AI单位: {aiUnits}", normalStyle);
            GUILayout.Label($"建筑数量: {buildings}", normalStyle);
            GUILayout.Label($"选中单位: {_selectedUnits.Count}", normalStyle);
            GUILayout.Label($"自动更新: {(autoUpdate ? "开启" : "关闭")}", normalStyle);
            GUILayout.Label($"活跃流场: {_battleGame.FlowFieldManager?.GetActiveFieldCount() ?? 0}", normalStyle);
        }

        GUILayout.Space(8);
        GUILayout.Label("=== 操作说明 ===", _labelStyle);
        
        GUIStyle instructionStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        GUILayout.Label("左键：创建己方坦克（蓝色）", instructionStyle);
        GUILayout.Label("右键：移动选中单位", instructionStyle);
        GUILayout.Label("Q键：创建AI坦克（红色）", instructionStyle);
        GUILayout.Label("E键：创建玩家防御塔", instructionStyle);
        GUILayout.Label("A键：选中所有己方单位", instructionStyle);
        GUILayout.Label("Space：切换自动更新", instructionStyle);
        GUILayout.Label("M键：手动更新一帧", instructionStyle);
        GUILayout.Label("H键：切换生命值显示", instructionStyle);
        GUILayout.Label("R键：切换攻击范围显示", instructionStyle);

        GUILayout.EndArea();
    }

    // 创建纯色纹理的辅助方法
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private (int playerUnits, int aiUnits, int buildings) GetUnitStatistics()
    {
        int playerUnits = 0;
        int aiUnits = 0;
        int buildings = 0;

        var camps = _battleGame.World.ComponentManager.GetAllEntityIdsWith<CampComponent>();
        foreach (var entityId in camps)
        {
            Entity entity = new Entity(entityId);
            var camp = _battleGame.World.ComponentManager.GetComponent<CampComponent>(entity);

            if (_battleGame.World.ComponentManager.HasComponent<BuildingComponent>(entity))
            {
                buildings++;
            }
            else if (camp.CampId == 0)
            {
                playerUnits++;
            }
            else
            {
                aiUnits++;
            }
        }

        return (playerUnits, aiUnits, buildings);
    }

    private int GetEntityCount()
    {
        int count = 0;
        var entities = _battleGame.World.ComponentManager.GetAllEntityIdsWith<TransformComponent>();
        foreach (var _ in entities)
        {
            count++;
        }
        return count;
    }

    // ===== Gizmos可视化 =====

    private void OnDrawGizmos()
    {
        if (_battleGame == null || _battleGame.MapManager == null)
            return;

        // 绘制地图网格
        if (showGrid)
        {
            DrawMapGrid();
        }

        // 绘制建筑
        DrawBuildings();

        // 绘制单位
        DrawUnits();

        // 绘制选中单位
        DrawSelectedUnits();
    }

    private void DrawMapGrid()
    {
        int width = _battleGame.MapManager.GetWidth();
        int height = _battleGame.MapManager.GetHeight();
        float gridSize = (float)_battleGame.MapManager.GetGridSize();

        // 绘制边界
        Gizmos.color = Color.yellow;
        float mapWidth = width * gridSize;
        float mapHeight = height * gridSize;

        Vector3 corner1 = new Vector3(0, 0, 0);
        Vector3 corner2 = new Vector3(mapWidth, 0, 0);
        Vector3 corner3 = new Vector3(mapWidth, 0, mapHeight);
        Vector3 corner4 = new Vector3(0, 0, mapHeight);

        Gizmos.DrawLine(corner1, corner2);
        Gizmos.DrawLine(corner2, corner3);
        Gizmos.DrawLine(corner3, corner4);
        Gizmos.DrawLine(corner4, corner1);

        // 绘制障碍物（地图边界）
        if (showObstacles)
        {
            Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.5f);
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (!_battleGame.MapManager.IsWalkable(i, j))
                    {
                        Vector3 pos = new Vector3(
                            i * gridSize + gridSize * 0.5f,
                            0.01f,
                            j * gridSize + gridSize * 0.5f
                        );
                        Gizmos.DrawCube(pos, new Vector3(gridSize * 0.95f, 0.1f, gridSize * 0.95f));
                    }
                }
            }
        }
    }

    private void DrawBuildings()
    {
        var buildings = _battleGame.World.ComponentManager.GetAllEntityIdsWith<BuildingComponent>();
        foreach (var entityId in buildings)
        {
            Entity entity = new Entity(entityId);
            var building = _battleGame.World.ComponentManager.GetComponent<BuildingComponent>(entity);
            var transform = _battleGame.World.ComponentManager.GetComponent<TransformComponent>(entity);
            var camp = _battleGame.World.ComponentManager.GetComponent<CampComponent>(entity);

            // 根据阵营设置颜色
            Gizmos.color = camp.CampId == 0 ? new Color(0.2f, 0.5f, 1f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f);

            // 绘制建筑占据的区域
            float gridSize = (float)_battleGame.MapManager.GetGridSize();
            Vector3 center = transform.Position.ToVector3();
            Vector3 size = new Vector3(building.Width * gridSize, 2f, building.Height * gridSize);

            Gizmos.DrawCube(center + Vector3.up, size);
            Gizmos.DrawWireCube(center + Vector3.up, size);

            // 绘制生命值条
            if (showHealthBars && _battleGame.World.ComponentManager.HasComponent<HealthComponent>(entity))
            {
                DrawHealthBar(entity, center + Vector3.up * 3f);
            }
        }
    }

    private void DrawUnits()
    {
        var units = _battleGame.World.ComponentManager.GetAllEntityIdsWith<UnitComponent>();
        foreach (var entityId in units)
        {
            Entity entity = new Entity(entityId);
            
            if (!_battleGame.World.ComponentManager.HasComponent<TransformComponent>(entity))
                continue;

            var transform = _battleGame.World.ComponentManager.GetComponent<TransformComponent>(entity);
            var camp = _battleGame.World.ComponentManager.GetComponent<CampComponent>(entity);
            Vector3 pos = transform.Position.ToVector3();

            // 绘制攻击范围
            if (showAttackRanges && _battleGame.World.ComponentManager.HasComponent<AttackComponent>(entity))
            {
                var attack = _battleGame.World.ComponentManager.GetComponent<AttackComponent>(entity);
                Gizmos.color = camp.CampId == 0 ? new Color(0.2f, 0.5f, 1f, 0.2f) : new Color(1f, 0.3f, 0.3f, 0.2f);
                Gizmos.DrawWireSphere(pos, (float)attack.Range);
            }

            // 绘制生命值条
            if (showHealthBars && _battleGame.World.ComponentManager.HasComponent<HealthComponent>(entity))
            {
                DrawHealthBar(entity, pos + Vector3.up * 2f);
            }
        }
    }

    private void DrawHealthBar(Entity entity, Vector3 position)
    {
        if (!_battleGame.World.ComponentManager.HasComponent<HealthComponent>(entity))
            return;

        var health = _battleGame.World.ComponentManager.GetComponent<HealthComponent>(entity);
        float healthPercent = (float)(health.CurrentHealth / health.MaxHealth);

        // 背景条（黑色）
        Gizmos.color = Color.black;
        Vector3 barSize = new Vector3(2f, 0.2f, 0.1f);
        Gizmos.DrawCube(position, barSize);

        // 生命值条（根据百分比变色）
        Gizmos.color = healthPercent > 0.5f ? Color.green : 
                       healthPercent > 0.25f ? Color.yellow : Color.red;
        Vector3 healthBarSize = new Vector3(2f * healthPercent, 0.2f, 0.1f);
        Vector3 healthBarPos = position - new Vector3((1f - healthPercent), 0, 0);
        Gizmos.DrawCube(healthBarPos, healthBarSize);

        // 边框
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(position, barSize);
    }

    private void DrawSelectedUnits()
    {
        Gizmos.color = Color.green;
        foreach (var entity in _selectedUnits)
        {
            if (_battleGame.World.ComponentManager.HasComponent<TransformComponent>(entity))
            {
                var transform = _battleGame.World.ComponentManager.GetComponent<TransformComponent>(entity);
                Vector3 pos = transform.Position.ToVector3();
                Gizmos.DrawWireSphere(pos, 1f);
            }
        }
    }
}

