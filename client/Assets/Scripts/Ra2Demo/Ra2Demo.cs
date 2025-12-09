using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZLockstep.View;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command;
using ZLockstep.Sync.Command.Commands;
using zUnity;
using Game.Examples;
using ZLockstep.Sync;
using ZLockstep.View.Systems;
using ZFrame;

/// <summary>
/// 测试脚本：点击地面创建单位（使用Command系统）
/// </summary>
public class Ra2Demo : MonoBehaviour
{
    [Header("游戏世界")]
    [SerializeField] private BattleGame _game;
    [SerializeField] public GameMode Mode;


    [Header("创建设置")]
    [SerializeField] private LayerMask groundLayer = -1; // 地面层


    [Header("Unity资源")]
    [SerializeField] private Transform viewRoot;
    [SerializeField] public GameObject[] unitPrefabs = new GameObject[10];
    private PresentationSystem _presentationSystem;

    // 添加建筑预览渲染器引用
    private BuildingPreviewRenderer buildingPreviewRenderer;


    [Header("小地图设置")]
    private MiniMapController miniMapController; // 小地图控制器
    private Rect miniMapRect = new Rect(-1, 0, 200, 200); // 小地图在屏幕上的位置和大小（使用-1作为特殊值表示从右侧/底部计算）
    private int miniMapMargin = 10; // 小地图边距
    private RenderTexture _miniMapTexture;

    private RTSControl _controls;
    public Camera _mainCamera;
    // 添加多选支持相关字段
    private List<int> selectedEntityIds = new List<int>(); // 多选单位列表
    private bool isSelecting = false; // 是否正在框选
    private bool isClickOnUnit = false; // 鼠标按下时是否点击在单位上
    private Vector2 selectionStartPoint; // 框选起始点
    private Vector2 selectionEndPoint; // 框选结束点
    private const float dragThreshold = 5f; // 拖拽阈值，像素单位

    /**************************************
     * UI部分
     *************************************/

    // 添加工厂生产相关字段
    private int selectedFactoryEntityId = -1; // 选中的工厂实体ID
    private bool showFactoryUI = false; // 是否显示工厂UI
    private bool showProductionList = false; // 是否显示生产建筑列表

    // 添加建造功能相关字段
    private BuildingType buildingToBuild = BuildingType.None; // 要建造的建筑类型: 3=采矿场, 4=电厂, 5=坦克工厂

    // GM相关
    private bool _isConsoleVisible = false;
    private string _inputFieldGM = "";
    private Vector2 _scrollPosition;


    private void Awake()
    {
        _mainCamera = Camera.main;
        _controls = new RTSControl();

        // 初始化小地图系统
        InitializeMiniMap();
    }

    /// <summary>
    /// 初始化Unity视图层
    /// </summary>
    public void InitializeUnityView()
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
        _presentationSystem.SetGame(_game);
        _game.World.SystemManager.RegisterSystem(_presentationSystem);

        Debug.Log("[StandaloneBattleDemo] 视图系统初始化完成");
    }

    /// <summary>
    /// 初始化小地图系统
    /// </summary>
    private void InitializeMiniMap()
    {
        if (miniMapController == null)
        {
            // 尝试查找场景中的小地图控制器
            miniMapController = FindObjectOfType<MiniMapController>();

            // 如果找不到，创建一个新的
            if (miniMapController == null)
            {
                GameObject miniMapObject = new GameObject("MiniMapController");
                miniMapController = miniMapObject.AddComponent<MiniMapController>();
            }
        }

        // 获取小地图渲染纹理
        _miniMapTexture = miniMapController.GetMiniMapTexture();
    }


    private Frame frame;

    private void Start()
    {
        frame = new Frame();

        NetworkManager.Instance.SetRa2Demo(this);

        DiscoverTools.Discover(typeof(Main).Assembly);
        Frame.DispatchEvent(new Ra2StartUpEvent(this));

        // 初始化建筑预览渲染器
        InitializeBuildingPreviewRenderer();
    }

    /// <summary>
    /// 初始化建筑预览渲染器
    /// </summary>
    private void InitializeBuildingPreviewRenderer()
    {
        buildingPreviewRenderer = gameObject.AddComponent<BuildingPreviewRenderer>();
        buildingPreviewRenderer.Initialize(this);
    }

    public BattleGame GetBattleGame()
    {
        return _game;
    }

    private void OnEnable()
    {
        _controls.Create.Enable();
        _controls.Create.createUnit.performed += OnCreateUnit;
    }

    private void OnDisable()
    {
        _controls.Create.Disable();
        _controls.Create.createUnit.performed -= OnCreateUnit;

    }

    /// <summary>
    /// 响应创建单位的输入（使用Command系统）
    /// </summary>
    private void OnCreateUnit(InputAction.CallbackContext context)
    {
        if (_game == null || _game.World == null)
        {
            Debug.LogWarning("[Test] GameWorldBridge 未初始化！");
            return;
        }

        // 获取鼠标/触摸位置
        Vector2 screenPosition = Pointer.current.position.ReadValue();

        // 射线检测获取点击位置
        if (TryGetGroundPosition(screenPosition, out Vector3 worldPosition))
        {
            // CreateUnitAtPosition(worldPosition);
        }

        // 检测点击到的gameobject对象，并设置给相机目标RTSCameraTargetController.Instance
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            // 检测是否点击到建筑
            if (!TryHandleBuildingClick(ray, hit))
            {
                // 如果没有点击到建筑，则开始框选
                StartSelectionBox(screenPosition);
            }
        }
    }

    /// <summary>
    /// 通过射线检测获取地面点击位置
    /// </summary>
    private bool TryGetGroundPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (_mainCamera == null)
        {
            Debug.LogWarning("[Test] 找不到主相机！");
            return false;
        }

        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

        // 尝试射线检测地面
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            worldPosition = hit.point;
            return true;
        }

        // 如果没有地面碰撞体，使用 Y=0 平面
        if (TryRaycastPlane(ray, Vector3.zero, Vector3.up, out Vector3 planeHit))
        {
            worldPosition = planeHit;
            return true;
        }

        Debug.LogWarning("[Test] 无法获取点击位置！请确保有地面碰撞体或使用默认平面。");
        return false;
    }

    /// <summary>
    /// 尝试处理建筑点击
    /// </summary>
    /// <param name="ray">射线</param>
    /// <returns>是否点击到建筑</returns>
    private bool TryHandleBuildingClick(Ray ray, RaycastHit hit)
    {
        // 移除点击工厂自动打开UI的功能
        return false;
    }

    /// <summary>
    /// 射线与平面相交检测
    /// </summary>
    private bool TryRaycastPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;

        float denominator = Vector3.Dot(planeNormal, ray.direction);
        if (Mathf.Abs(denominator) < 0.0001f)
            return false; // 射线与平面平行

        float t = Vector3.Dot(planePoint - ray.origin, planeNormal) / denominator;
        if (t < 0)
            return false; // 射线朝反方向

        hitPoint = ray.origin + ray.direction * t;
        return true;
    }

    /// <summary>
    /// 开始框选
    /// </summary>
    /// <param name="screenPosition">屏幕位置</param>
    private void StartSelectionBox(Vector2 screenPosition)
    {
        isSelecting = true;
        isClickOnUnit = false; // 重置单位点击状态
        selectionStartPoint = screenPosition;
        selectionEndPoint = screenPosition;
    }

    /// <summary>
    /// 更新框选
    /// </summary>
    /// <param name="screenPosition">屏幕位置</param>
    private void UpdateSelectionBox(Vector2 screenPosition)
    {
        if (isSelecting)
        {
            selectionEndPoint = screenPosition;

            // 计算拖拽距离
            float dragDistance = Vector2.Distance(selectionStartPoint, selectionEndPoint);

            // 只有当拖拽距离超过阈值时才真正激活框选
            if (dragDistance > dragThreshold)
            {
                // 激活框选可视化
                isSelecting = true;
            }
        }
    }

    /// <summary>
    /// 结束框选
    /// </summary>
    private void EndSelectionBox()
    {
        // 移除isClickOnUnit检查，只保留框选逻辑
        if (!isSelecting) return;

        isSelecting = false;

        // 计算拖拽距离
        float dragDistance = Vector2.Distance(selectionStartPoint, selectionEndPoint);

        // 只有当拖拽距离超过阈值时才执行框选
        if (dragDistance <= dragThreshold)
        {
            return;
        }

        // 计算框选区域 (统一使用屏幕坐标系统)
        float x = Mathf.Min(selectionStartPoint.x, selectionEndPoint.x);
        float y = Mathf.Min(selectionStartPoint.y, selectionEndPoint.y);
        float width = Mathf.Abs(selectionStartPoint.x - selectionEndPoint.x);
        float height = Mathf.Abs(selectionStartPoint.y - selectionEndPoint.y);

        Rect selectionRect = new Rect(x, y, width, height);

        // 清空之前的选择
        ClearAllOutlines();
        // selectedEntityIds.Clear();

        // 查找框选区域内的单位
        if (_game != null && _game.World != null)
        {
            var entities = _game.World.ComponentManager
                .GetAllEntityIdsWith<TransformComponent>();

            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                var transform = _game.World.ComponentManager
                    .GetComponent<TransformComponent>(entity);

                // 检查实体是否包含LocalPlayerComponent（只有本地玩家单位才能被选择）
                if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
                {
                    continue; // 跳过非本地玩家单位
                }

                if (!_game.World.ComponentManager.HasComponent<UnitComponent>(entity))
                {
                    continue; // 跳过非单位实体
                }

                // 将世界坐标转换为屏幕坐标
                Vector3 worldPosition = transform.Position.ToVector3();
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPosition);

                // 注意：屏幕坐标的Y轴是从下往上增长的
                // selectionRect也使用相同的坐标系统，所以不需要转换

                // 检查单位是否在框选区域内
                if (selectionRect.Contains(screenPos))
                {
                    // 添加到选中列表
                    selectedEntityIds.Add(entityId);
                    Debug.Log($"[Test] 框选添加单位: EntityId={entityId}");
                }
            }

        }

        // 为所有新选中的单位启用OutlineComponent
        foreach (int entityId in selectedEntityIds)
        {
            EnableOutlineForEntity(entityId);
        }
    }

    private int BuildingTypeToPrefabId(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.Smelter:
                return 3;
            case BuildingType.PowerPlant:
                return 4;
            case BuildingType.Factory:
                return 5;
            default:
                return -1;
        }
    }

    /// <summary>
    /// 更新建筑预览位置
    /// </summary>
    public void UpdateBuildingPreview()
    {
        // 委托给建筑预览渲染器处理
        if (buildingPreviewRenderer != null)
        {
            buildingPreviewRenderer.UpdateBuildingPreview(buildingToBuild, true);
        }
    }

    /// <summary>
    /// 放置建筑
    /// </summary>
    /// <summary>
    /// 放置建筑
    /// </summary>
    public void PlaceBuilding()
    {
        if (buildingPreviewRenderer != null)
        {
            buildingPreviewRenderer.PlaceBuilding(out bool wasPlaced);
            if (wasPlaced)
            {
                buildingToBuild = BuildingType.None;
            }
        }
    }

    /// <summary>
    /// 取消建筑建造
    /// </summary>
    private void CancelBuilding()
    {
        if (buildingPreviewRenderer != null)
        {
            buildingPreviewRenderer.CancelBuilding();
            buildingToBuild = BuildingType.None;
        }
    }

    private void FixedUpdate()
    {
        // 每帧开始先处理网络消息
        NetworkManager.Instance.CurrentWebSocket?.DispatchMessageQueue();

        if (_game != null)
        {
            _game.Update();
        }
    }

    /// <summary>
    /// 测试：右键点击地面让选中的单位移动（可选）
    /// </summary>
    private void Update()
    {
        // 通过按 "~" 键呼出/关闭控制台
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            _isConsoleVisible = !_isConsoleVisible;
        }

        // 如果控制台可见，确保输入字段获得焦点
        if (_isConsoleVisible)
        {
            GUI.FocusControl("InputField");
        }

        InputAction();

        // 表现系统：插值更新
        if (_presentationSystem != null)
        {
            _presentationSystem.LerpUpdate(Time.deltaTime, 10f);
        }

        // 更新建筑预览
        UpdateBuildingPreview();
    }

    private void InputAction()
    {
        // 更新框选状态
        if (isSelecting && Mouse.current != null)
        {
            // 如果鼠标左键仍然按下，更新框选区域
            if (Mouse.current.leftButton.isPressed)
            {
                Vector2 currentMousePosition = Mouse.current.position.ReadValue();
                UpdateSelectionBox(currentMousePosition);
            }
            else
            {
                // 鼠标左键释放，结束框选
                EndSelectionBox();
            }
        }
        else if (!isSelecting && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            // 确保在鼠标释放时也结束框选（额外的安全检查）
            EndSelectionBox();
        }

        // 左键点击放置建筑或选择单位
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 如果处于建造模式，放置建筑
            if (buildingToBuild != BuildingType.None)
            {
                PlaceBuilding();
            }
        }

        // 右键点击发送移动命令
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            // 如果在建造模式下，取消建造
            if (buildingToBuild != BuildingType.None)
            {
                CancelBuilding();
            }
            else
            {
                SendMoveCommandForSelectedUnit();
            }
        }
    }


    /// <summary>
    /// 发送移动命令给选中的单位
    /// </summary>
    private void SendMoveCommandForSelectedUnit()
    {
        // 检查是否有选中的单位
        if (selectedEntityIds.Count == 0)
        {
            Debug.Log("[Test] 没有选中的单位");
            return;
        }

        if (_game == null || _game.World == null)
            return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (!TryGetGroundPosition(screenPosition, out Vector3 worldPosition))
            return;

        // 检查选中的单位是否仍然有效且属于当前玩家
        List<int> validEntityIds = new List<int>();
        foreach (int entityId in selectedEntityIds)
        {
            var entity = new Entity(entityId);
            if (!_game.World.ComponentManager.HasComponent<UnitComponent>(entity))
            {
                Debug.Log($"[Test] 选中的单位 {entityId} 已不存在");
                // 禁用该单位的OutlineComponent
                DisableOutlineForEntity(entityId);
                continue;
            }

            // 判断拥有LocalPlayerComponent就是当前玩家的单位
            if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
            {
                Debug.Log($"[Test] 选中的单位 {entityId} 不属于当前玩家");
                // 禁用该单位的OutlineComponent
                DisableOutlineForEntity(entityId);
                continue;
            }

            validEntityIds.Add(entityId);
        }

        // 更新选中单位列表，移除无效单位
        selectedEntityIds = new List<int>(validEntityIds);

        if (validEntityIds.Count == 0)
        {
            Debug.Log("[Test] 没有有效的选中单位");
            return;
        }

        zfloat x = zfloat.CreateFloat((long)(worldPosition.x * zfloat.SCALE_10000));
        zfloat z = zfloat.CreateFloat((long)(worldPosition.z * zfloat.SCALE_10000));

        // 创建移动命令
        var moveCommand = new EntityMoveCommand(
            campId: 0,
            entityIds: validEntityIds.ToArray(),
            targetPosition: new zVector2(x, z)
        )
        {
            Source = CommandSource.Local
        };

        _game.SubmitCommand(moveCommand);
        // Debug.Log($"[Test] 发送移动命令: {validEntityIds.Count}个单位 → {worldPosition}");
    }

    private void OnGUI()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fixedHeight = 60;
        buttonStyle.fixedWidth = 200;

        // 绘制生产按钮（左下角）
        if (NetworkManager.Instance.IsReady)
        {
            GUIStyle produceButtonStyle = new GUIStyle(GUI.skin.button);
            produceButtonStyle.fontSize = 24;
            produceButtonStyle.fixedHeight = 80;
            produceButtonStyle.fixedWidth = 80;

            Rect productionButtonRect = new(20, Screen.height - 100, 80, 80);
            if (GUI.Button(productionButtonRect, "生产", produceButtonStyle))
            {
                showProductionList = !showProductionList;
            }

            // 绘制生产建筑列表
            if (showProductionList)
            {
                DrawProductionBuildingList();
            }
        }

        // 绘制工厂生产UI
        if (showFactoryUI && selectedFactoryEntityId != -1)
        {
            DrawFactoryProductionUI();
        }

        // 绘制框选区域
        DrawSelectionBox();

        // 绘制小地图
        DrawMiniMap();

        // 绘制游戏结束界面
        if (_presentationSystem != null && _presentationSystem.IsGameOver)
        {
            DrawGameOverUI();
        }

        DrawGMConsole();
    }

    /// <summary>
    /// 开始建筑放置模式
    /// </summary>
    /// <param name="buildingType">建筑类型 (3=采矿场, 4=电厂, 5=坦克工厂)</param>
    public void StartBuildingPlacement(BuildingType buildingType)
    {
        buildingToBuild = buildingType;

        Debug.Log($"[Test] 开始放置建筑: {GetBuildingName(buildingType)}");
    }

    /// <summary>
    /// 获取建筑名称
    /// </summary>
    /// <param name="buildingType">建筑类型</param>
    /// <returns>建筑名称</returns>
    private string GetBuildingName(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.Smelter: return "采矿场";
            case BuildingType.PowerPlant: return "电厂";
            case BuildingType.Factory: return "坦克工厂";
            default: return $"建筑{buildingType}";
        }
    }

    /// <summary>
    /// 绘制工厂生产UI
    /// </summary>
    private void DrawFactoryProductionUI()
    {
        if (_game == null || _game.World == null)
            return;

        var entity = new Entity(selectedFactoryEntityId);
        if (!_game.World.ComponentManager.HasComponent<ProduceComponent>(entity))
            return;

        var produceComponent = _game.World.ComponentManager.GetComponent<ProduceComponent>(entity);

        // 创建一个半透明背景
        GUIStyle bgStyle = new GUIStyle();
        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
        bgTexture.Apply();
        bgStyle.normal.background = bgTexture;

        // 绘制背景
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", bgStyle);

        // 绘制工厂UI面板
        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.fontSize = 24;

        Rect panelRect = new Rect((Screen.width - 500) / 2, (Screen.height - 400) / 2, 500, 400);
        GUI.Box(panelRect, "", panelStyle);

        // 显示标题
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(panelRect.x, panelRect.y + 10, panelRect.width, 30), "工厂生产", titleStyle);

        // 创建内容区域（为标题预留空间）
        Rect contentRect = new Rect(panelRect.x, panelRect.y + 50, panelRect.width, panelRect.height - 60);
        GUILayout.BeginArea(contentRect, panelStyle);

        // 增大标签字体
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 20;

        // 增大按钮字体
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 20;

        // 显示支持生产的单位类型
        foreach (var unitType in produceComponent.SupportedUnitTypes)
        {
            int produceNumber = produceComponent.ProduceNumbers.ContainsKey(unitType) ?
                produceComponent.ProduceNumbers[unitType] : 0;
            int progress = produceComponent.ProduceProgress.ContainsKey(unitType) ?
                produceComponent.ProduceProgress[unitType] : 0;

            // 显示单位类型名称和数量
            string unitTypeName = GetUnitTypeName(unitType);
            GUILayout.Label($"{unitTypeName}: {produceNumber}/99", labelStyle);

            // 绘制进度条 (放在数量下面)
            Rect progressRect = GUILayoutUtility.GetLastRect();
            progressRect.y += progressRect.height + 5;
            progressRect.width = 300;
            progressRect.height = 20;

            // 背景
            GUI.color = Color.gray;
            GUI.DrawTexture(new Rect(progressRect.x, progressRect.y, progressRect.width, progressRect.height), Texture2D.whiteTexture);

            // 进度
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(progressRect.x, progressRect.y, progressRect.width * progress / 100f, progressRect.height), Texture2D.whiteTexture);

            // 进度文本
            GUI.color = Color.white;
            GUIStyle progressLabelStyle = new GUIStyle(labelStyle);
            progressLabelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(progressRect.x, progressRect.y, progressRect.width, progressRect.height), $"进度: {progress}%", progressLabelStyle);

            GUILayout.Space(progressRect.height + 5);

            // 增加和减少按钮
            GUILayout.BeginHorizontal();
            GUILayout.Space(100); // 左边空白

            // 减少按钮
            if (GUILayout.Button("-", buttonStyle, GUILayout.Width(50), GUILayout.Height(30)) && produceNumber > 0)
            {
                SendProduceCommand(selectedFactoryEntityId, unitType, -1);
            }

            GUILayout.Space(20); // 中间空白

            // 增加按钮
            if (GUILayout.Button("+", buttonStyle, GUILayout.Width(50), GUILayout.Height(30)) && produceNumber < 99)
            {
                SendProduceCommand(selectedFactoryEntityId, unitType, 1);
            }

            GUILayout.Space(100); // 右边空白
            GUILayout.EndHorizontal();

            GUILayout.Space(20); // 单位类型之间的间隔
        }

        // 关闭按钮
        GUIStyle closeStyle = new GUIStyle(GUI.skin.button);
        closeStyle.fontSize = 20;
        if (GUILayout.Button("关闭", closeStyle, GUILayout.Height(40)))
        {
            showFactoryUI = false;
            selectedFactoryEntityId = -1;
        }

        GUILayout.EndArea();
    }

    /// <summary>
    /// 获取单位类型名称
    /// </summary>
    /// <param name="unitType">单位类型</param>
    /// <returns>单位类型名称</returns>
    private string GetUnitTypeName(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.Infantry: return "动员兵()";
            case UnitType.Tank: return "坦克(300$)";
            case UnitType.Harvester: return "矿车";
            default: return $"单位{unitType}";
        }
    }

    /// <summary>
    /// 发送生产命令
    /// </summary>
    /// <param name="factoryEntityId">工厂实体ID</param>
    /// <param name="unitType">单位类型</param>
    /// <param name="changeValue">变化值</param>
    private void SendProduceCommand(int factoryEntityId, UnitType unitType, int changeValue)
    {
        if (_game == null || factoryEntityId == -1)
            return;

        var produceCommand = new ProduceCommand(
            campId: 0,
            entityId: factoryEntityId,
            unitType: unitType,
            changeValue: changeValue
        )
        {
            Source = CommandSource.Local
        };

        _game.SubmitCommand(produceCommand);
        Debug.Log($"[Test] 发送生产命令: 工厂{factoryEntityId} 单位类型{unitType} 变化值{changeValue}");
    }

    /// <summary>
    /// 绘制游戏结束界面
    /// </summary>
    private void DrawGameOverUI()
    {
        // 创建一个半透明背景
        GUIStyle bgStyle = new GUIStyle();
        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
        bgTexture.Apply();
        bgStyle.normal.background = bgTexture;

        // 绘制背景
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", bgStyle);

        // 显示结果文本
        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = 48;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = _presentationSystem.IsVictory ? Color.green : Color.red;

        string resultText = "";
        string infoText = "";

        // 根据胜利阵营ID判断游戏结果
        if (_presentationSystem.WinningCampId == -1)
        {
            // 平局
            resultText = "平局!";
            infoText = "所有阵营都被击败";
            textStyle.normal.textColor = Color.yellow; // 平局用黄色显示
        }
        else
        {
            // 胜负结果
            resultText = _presentationSystem.IsVictory ? "胜利!" : "失败!";
            infoText = _presentationSystem.IsVictory ?
                $"你击败了阵营 {_presentationSystem.WinningCampId}" :
                $"你被阵营 {_presentationSystem.WinningCampId} 击败";
        }

        GUI.Label(new Rect(0, Screen.height / 2 - 50, Screen.width, 100), resultText, textStyle);

        // 显示胜利方信息
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 24;
        infoStyle.alignment = TextAnchor.MiddleCenter;
        infoStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(0, Screen.height / 2 + 50, Screen.width, 50), infoText, infoStyle);

        // 显示重新开始按钮
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fixedHeight = 60;
        buttonStyle.fixedWidth = 200;

        Rect buttonRect = new Rect((Screen.width - 200) / 2, Screen.height / 2 + 150, 200, 60);
        if (GUI.Button(buttonRect, "重新开始", buttonStyle))
        {
            RestartGame();
        }
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    private void RestartGame()
    {

        // 清空选中单位列表
        ClearAllOutlines(); // 清除所有单位的描边

        // 销毁所有视图对象
        if (_presentationSystem != null)
        {
            _presentationSystem.DestroyAllViews();
        }

        // 销毁旧的游戏实例和相关组件
        if (_game != null)
        {
            // 注意：C#中没有显式的销毁方法，我们只需要解除引用
            _game = null;
        }

        zUDebug.Log("[Ra2Demo] 重新开始游戏");
    }

    /// <summary>
    /// 绘制小地图
    /// </summary>
    private void DrawMiniMap()
    {
        if (_miniMapTexture != null)
        {
            // 计算小地图的实际位置（支持从底部和右侧对齐）
            Rect actualMiniMapRect = miniMapRect;
            if (miniMapRect.y == -1 || miniMapRect.x == -1) // 特殊值表示从底部计算位置
            {
                float xPos = miniMapRect.x == -1 ? Screen.width - miniMapRect.width - miniMapMargin : miniMapMargin;
                float yPos = miniMapRect.y == -1 ? Screen.height - miniMapRect.height - miniMapMargin : miniMapMargin;
                actualMiniMapRect = new Rect(xPos, yPos, miniMapRect.width, miniMapRect.height);
            }

            // 检测小地图点击事件
            HandleMiniMapClick(actualMiniMapRect);

            // 创建一个小地图窗口样式
            GUIStyle miniMapStyle = new GUIStyle(GUI.skin.box);

            // 绘制小地图背景
            GUI.Box(actualMiniMapRect, "", miniMapStyle);

            // 绘制小地图标题
            GUIStyle miniMapTitleStyle = new GUIStyle(GUI.skin.label);
            miniMapTitleStyle.fontStyle = FontStyle.Bold;
            miniMapTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(actualMiniMapRect.x, actualMiniMapRect.y + 5, actualMiniMapRect.width, 20),
                      "小地图", miniMapTitleStyle);

            // 绘制小地图内容（留出标题空间）
            Rect textureRect = new Rect(actualMiniMapRect.x, actualMiniMapRect.y + 25,
                                       actualMiniMapRect.width, actualMiniMapRect.height - 30);
            GUI.DrawTexture(textureRect, _miniMapTexture, ScaleMode.StretchToFill, true);

            // 可选：在小地图上绘制一个表示主相机视角的框
            DrawCameraViewIndicator(actualMiniMapRect);
        }
    }

    /// <summary>
    /// 绘制框选区域
    /// </summary>
    private void DrawSelectionBox()
    {
        // 只有当真正激活框选时才绘制（拖拽距离超过阈值且未点击在单位上）
        if (isSelecting && !isClickOnUnit)
        {
            // 计算拖拽距离
            float dragDistance = Vector2.Distance(selectionStartPoint, selectionEndPoint);

            // 只有当拖拽距离超过阈值时才绘制框选区域
            if (dragDistance <= dragThreshold)
            {
                return;
            }

            // 创建框选区域的样式
            GUIStyle selectionStyle = new GUIStyle();
            selectionStyle.normal.background = Texture2D.whiteTexture;
            Color selectionColor = new Color(0.5f, 0.7f, 1.0f, 0.3f); // 半透明蓝色
            selectionStyle.normal.textColor = selectionColor;

            // 计算框选区域 (转换为GUI坐标系统)
            float x = Mathf.Min(selectionStartPoint.x, selectionEndPoint.x);
            // GUI系统的Y轴是从上往下增长的，需要转换坐标
            float guiStartY = Screen.height - selectionStartPoint.y;
            float guiEndY = Screen.height - selectionEndPoint.y;
            float y = Mathf.Min(guiStartY, guiEndY);
            float width = Mathf.Abs(selectionStartPoint.x - selectionEndPoint.x);
            float height = Mathf.Abs(selectionStartPoint.y - selectionEndPoint.y);

            Rect selectionRect = new Rect(x, y, width, height);

            // 绘制半透明的框选区域
            Color oldColor = GUI.color;
            GUI.color = selectionColor;
            GUI.Box(selectionRect, "", selectionStyle);
            GUI.color = oldColor;
        }
    }

    /// <summary>
    /// 处理小地图点击事件
    /// </summary>
    /// <param name="miniMapRect">小地图在屏幕上的矩形区域</param>
    private void HandleMiniMapClick(Rect miniMapRect)
    {
        // 只有在游戏开始后才响应点击
        if (!NetworkManager.Instance.IsReady || _mainCamera == null || miniMapController == null)
            return;

        // 检查鼠标是否点击在小地图区域
        if (Event.current != null && Event.current.type == EventType.MouseDown &&
            Event.current.button == 0) // 左键点击
        {
            Vector2 mousePos = Event.current.mousePosition;

            // 计算小地图纹理区域（排除标题栏）
            Rect textureRect = new Rect(miniMapRect.x, miniMapRect.y + 25,
                                      miniMapRect.width, miniMapRect.height - 30);

            // 检查点击是否在小地图纹理区域
            if (textureRect.Contains(mousePos))
            {
                // 将鼠标位置转换为相对于纹理区域的位置
                float localX = mousePos.x - textureRect.x;
                float localY = mousePos.y - textureRect.y;

                // 将点击位置映射到游戏世界坐标
                // 小地图纹理是256x256，对应游戏世界0-256坐标
                float worldX = localX / textureRect.width * 256f;
                float worldZ = localY / textureRect.height * 256f;

                // 注意：纹理坐标Y轴向下为正，而世界坐标Z轴向上为正，需要翻转
                worldZ = 256f - worldZ;

                if (RTSCameraTargetController.Instance != null && RTSCameraTargetController.Instance.CameraTarget != null)
                {
                    Vector3 targetPos = new(worldX, -50, worldZ);
                    RTSCameraTargetController.Instance.CameraTarget.position = targetPos;
                }

                // 使用事件，防止其他UI元素重复处理
                Event.current.Use();
            }
        }
    }

    /// <summary>
    /// 在小地图上绘制主相机视角指示器
    /// </summary>
    private void DrawCameraViewIndicator(Rect actualMiniMapRect)
    {
        if (_mainCamera == null || miniMapController == null)
            return;

        // 获取地图边界
        Vector4 mapBounds = miniMapController.GetMapBounds();
        float mapWidth = mapBounds.z - mapBounds.x;  // 256
        float mapHeight = mapBounds.w - mapBounds.y; // 256

        // 计算主相机在世界坐标系中的视野范围
        Vector3 cameraPosition = _mainCamera.transform.position;

        // 将世界坐标转换为小地图纹理坐标 (0-256范围)
        float cameraX = Mathf.Clamp((cameraPosition.x - mapBounds.x) / mapWidth * 256f, 0, 256);
        float cameraY = Mathf.Clamp((cameraPosition.z - mapBounds.y) / mapHeight * 256f, 0, 256);

        // 计算小地图在屏幕上的实际位置
        if (miniMapRect.y == -1) // 特殊值表示从底部计算位置
        {
            float xPos = miniMapRect.x == -1 ? Screen.width - miniMapRect.width - miniMapMargin : miniMapMargin;
            if (miniMapRect.x == -1) // 特殊值表示从右侧计算位置
            {
                xPos = Screen.width - miniMapRect.width - miniMapMargin;
            }

            actualMiniMapRect = new Rect(xPos,
                                       Screen.height - miniMapRect.height - miniMapMargin,
                                       miniMapRect.width,
                                       miniMapRect.height);
        }

        // 计算相机位置在屏幕上的实际绘制位置
        // 需要考虑小地图纹理在屏幕上的绘制区域和标题栏高度(25)
        float drawX = actualMiniMapRect.x + (cameraX / 256f) * actualMiniMapRect.width;
        float drawY = actualMiniMapRect.y + (1.0f - cameraY / 256f) * (actualMiniMapRect.height - 25);

        // 绘制相机视角框（红色边框）
        Color oldColor = GUI.color;
        GUI.color = Color.red;

        // 绘制一个固定大小的框作为示例（可以根据需要调整大小）
        float boxSize = 30f;
        GUI.DrawTexture(new Rect(drawX - boxSize / 2, drawY - boxSize / 2, boxSize, 2), Texture2D.whiteTexture); // 上边框
        GUI.DrawTexture(new Rect(drawX - boxSize / 2, drawY - boxSize / 2, 2, boxSize), Texture2D.whiteTexture); // 左边框
        GUI.DrawTexture(new Rect(drawX + boxSize / 2 - 2, drawY - boxSize / 2, 2, boxSize), Texture2D.whiteTexture); // 右边框
        GUI.DrawTexture(new Rect(drawX - boxSize / 2, drawY + boxSize / 2 - 2, boxSize, 2), Texture2D.whiteTexture); // 下边框

        // 在中心绘制一个小点
        GUI.DrawTexture(new Rect(drawX - 3, drawY - 3, 6, 6), Texture2D.whiteTexture);

        GUI.color = oldColor;
    }


    /// <summary>
    /// 绘制GM控制台
    /// </summary>
    private void DrawGMConsole()
    {
        if (!_isConsoleVisible) return;

        // 设置更暗的背景色
        Color backgroundColor = new Color(0f, 0f, 0f, 0.95f);
        Color oldBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;

        // 简易的IMGUI控制台界面
        GUILayout.Window(0, new Rect(0, 0, Screen.width, Screen.height * 0.3f), DrawGMConsoleWindow, "GM Console");

        GUI.backgroundColor = oldBackgroundColor;
    }

    private void DrawGMConsoleWindow(int windowID)
    {
        // 日志显示区域（可滚动）
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        var logHistory = _game.World.GMManager.LogHistory;
        foreach (var log in logHistory)
        {
            GUILayout.Label(log);
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        // 输入框
        GUI.SetNextControlName("InputField");
        _inputFieldGM = GUILayout.TextField(_inputFieldGM, GUILayout.ExpandWidth(true));
        // 确保输入框获得焦点
        if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
        {
            _game.SubmitCommand(new GMCommand(_inputFieldGM));
            Event.current.Use(); // 防止重复处理
        }
        GUILayout.FlexibleSpace();
        // 执行按钮
        if (GUILayout.Button("Execute", GUILayout.Width(60)))
        {
            _game.SubmitCommand(new GMCommand(_inputFieldGM));
        }
        GUILayout.EndHorizontal();
        GUI.FocusControl("InputField");
    }

    /// <summary>
    /// 移动相机到我方工厂位置
    /// </summary>
    public void MoveCameraToOurFactory()
    {
        if (_game == null || _game.World == null)
            return;

        // 直接查找本地玩家的主基地
        var (mainBaseComponent, entity) = _game.World.ComponentManager.GetComponentWithCondition<MainBaseComponent>(
            e => _game.World.ComponentManager.HasComponent<LocalPlayerComponent>(e));

        // 如果找到了本地玩家的主基地
        if (entity.Id != -1)
        {
            // 确保实体有位置组件
            if (_game.World.ComponentManager.HasComponent<TransformComponent>(entity))
            {
                var transform = _game.World.ComponentManager.GetComponent<TransformComponent>(entity);
                Vector3 factoryPosition = transform.Position.ToVector3();

                // 将相机移动到工厂位置
                if (RTSCameraTargetController.Instance != null && RTSCameraTargetController.Instance.CameraTarget != null)
                {
                    RTSCameraTargetController.Instance.CameraTarget.position = factoryPosition;
                    zUDebug.Log($"[Ra2Demo] 相机已移动到我方工厂位置: {factoryPosition}");
                }
                return;
            }
        }

        zUDebug.Log("[Ra2Demo] 未找到我方工厂");
    }

    /// <summary>
    /// 为指定实体启用OutlineComponent
    /// </summary>
    /// <param name="entityId">实体ID</param>
    private void EnableOutlineForEntity(int entityId)
    {
        if (_game == null || _game.World == null || _presentationSystem == null)
            return;

        var entity = new Entity(entityId);
        if (_game.World.ComponentManager.HasComponent<ViewComponent>(entity))
        {
            var viewComponent = _game.World.ComponentManager.GetComponent<ViewComponent>(entity);
            if (viewComponent != null && viewComponent.GameObject != null)
            {
                var outlineComponent = viewComponent.GameObject.GetComponent<OutlineComponent>();
                if (outlineComponent != null)
                {
                    outlineComponent.enabled = true;
                }
            }
        }
    }

    /// <summary>
    /// 为指定实体禁用OutlineComponent
    /// </summary>
    /// <param name="entityId">实体ID</param>
    private void DisableOutlineForEntity(int entityId)
    {
        if (_game == null || _game.World == null || _presentationSystem == null)
            return;

        var entity = new Entity(entityId);
        if (_game.World.ComponentManager.HasComponent<ViewComponent>(entity))
        {
            var viewComponent = _game.World.ComponentManager.GetComponent<ViewComponent>(entity);
            if (viewComponent != null && viewComponent.GameObject != null)
            {
                var outlineComponent = viewComponent.GameObject.GetComponent<OutlineComponent>();
                if (outlineComponent != null)
                {
                    outlineComponent.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// 清除所有单位的OutlineComponent
    /// </summary>
    private void ClearAllOutlines()
    {
        if (_game == null || _game.World == null || _presentationSystem == null)
            return;

        // 禁用之前选中单位的OutlineComponent
        foreach (int entityId in selectedEntityIds)
        {
            DisableOutlineForEntity(entityId);
        }
        selectedEntityIds.Clear();
    }

    /// <summary>
    /// 绘制生产建筑列表
    /// </summary>
    private void DrawProductionBuildingList()
    {
        if (_game == null || _game.World == null)
            return;

        // 获取所有具有生产功能且属于本地玩家的建筑实体
        List<int> productionBuildings = GetLocalPlayerProductionBuildings();

        if (productionBuildings.Count == 0)
            return;

        // 创建一个半透明背景
        GUIStyle bgStyle = new GUIStyle();
        Texture2D bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
        bgTexture.Apply();
        bgStyle.normal.background = bgTexture;

        // 绘制背景
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", bgStyle);

        // 绘制面板
        GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.fontSize = 24;

        Rect panelRect = new Rect((Screen.width - 300) / 2, (Screen.height - 200) / 2, 300, 200);
        GUI.Box(panelRect, "", panelStyle);

        // 显示标题
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(panelRect.x, panelRect.y + 10, panelRect.width, 30), "选择生产建筑", titleStyle);

        // 创建内容区域（为标题预留空间）
        Rect contentRect = new Rect(panelRect.x, panelRect.y + 50, panelRect.width, panelRect.height - 60);
        GUILayout.BeginArea(contentRect);

        // 增大按钮字体
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 20;

        // 显示所有生产建筑
        foreach (int entityId in productionBuildings)
        {
            var entity = new Entity(entityId);
            if (_game.World.ComponentManager.HasComponent<BuildingComponent>(entity))
            {
                var building = _game.World.ComponentManager.GetComponent<BuildingComponent>(entity);
                string buildingName = GetBuildingTypeName(building.BuildingType);
                if (GUILayout.Button($"{buildingName} (ID: {entityId})", buttonStyle, GUILayout.Height(40)))
                {
                    // 选中该建筑并打开生产界面
                    selectedFactoryEntityId = entityId;
                    showFactoryUI = true;
                    showProductionList = false;
                }
            }
        }

        // 关闭按钮
        GUIStyle closeStyle = new GUIStyle(GUI.skin.button);
        closeStyle.fontSize = 20;
        if (GUILayout.Button("关闭", closeStyle, GUILayout.Height(40)))
        {
            showProductionList = false;
        }

        GUILayout.EndArea();
    }

    /// <summary>
    /// 获取所有本地玩家的生产建筑
    /// </summary>
    /// <summary>
    /// 获取所有本地玩家的生产建筑
    /// </summary>
    /// <returns>生产建筑实体ID列表</returns>
    private List<int> GetLocalPlayerProductionBuildings()
    {
        List<int> result = new List<int>();

        if (_game == null || _game.World == null)
            return result;

        // 使用GetComponentsWithCondition一次性获取符合条件的实体
        var components = _game.World.ComponentManager
            .GetComponentsWithCondition<ProduceComponent>(entity =>
                _game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity) &&
                !_game.World.ComponentManager.HasComponent<BuildingConstructionComponent>(entity) &&
                _game.World.ComponentManager.HasComponent<BuildingComponent>(entity));

        foreach (var (_, entity) in components)
        {
            result.Add(entity.Id);
        }

        return result;
    }

    /// <summary>
    /// 获取建筑类型名称
    /// </summary>
    /// <param name="buildingType">建筑类型</param>
    /// <returns>建筑类型名称</returns>
    private string GetBuildingTypeName(int buildingType)
    {
        switch (buildingType)
        {
            case 1: return "战车工厂";
            case 2: return "兵营";
            case 3: return "矿场";
            default: return $"建筑{buildingType}";
        }
    }

    public void SetBattleGame(BattleGame game)
    {
        _game = game;
    }


}