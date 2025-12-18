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
using System;

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

    private RTSControl _controls;
    public Camera _mainCamera;
    // 添加多选支持相关字段
    private List<int> selectedEntityIds = new List<int>(); // 多选单位列表
    private bool isSelecting = false; // 是否正在框选
    private bool isClickOnUnit = false; // 鼠标按下时是否点击在单位上
    private Vector2 selectionStartPoint; // 框选起始点
    private Vector2 selectionEndPoint; // 框选结束点
    private const float dragThreshold = 5f; // 拖拽阈值，像素单位
    private Vector3 m_CameraInitialPosition = Vector3.zero; // 相机初始位置

    /**************************************
     * UI部分
     *************************************/

    // 添加建造功能相关字段
    private BuildingType buildingToBuild = BuildingType.None; // 要建造的建筑类型: 3=采矿场, 4=电厂, 5=坦克工厂

    // GM相关
    private bool _isConsoleVisible = false;
    private string _inputFieldGM = "";
    private Vector2 _scrollPosition;

    // 选择模式/（相机）移动模式
    public bool IsSelectMode { get; set; } = true;


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
    }

    /// <summary>
    /// 获取小地图渲染纹理
    /// </summary>
    /// <returns>小地图渲染纹理</returns>
    public RenderTexture GetMiniMapTexture()
    {
        return miniMapController.GetMiniMapTexture();
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
        _controls.Camera.Enable();
        _controls.Camera.Pan.performed += OnCameraPan;
        _controls.Camera.Zoom.performed += OnCameraZoom;
    }

    private void OnDisable()
    {
        _controls.Create.Disable();
        _controls.Create.createUnit.performed -= OnCreateUnit;

        _controls.Camera.Disable();
        _controls.Camera.Pan.performed -= OnCameraPan;
        _controls.Camera.Zoom.performed -= OnCameraZoom;

    }

    private void OnCameraZoom(InputAction.CallbackContext context)
    {
        // 获取鼠标/触摸位置
        Vector2 zoom = _controls.Camera.Zoom.ReadValue<Vector2>();

        zUDebug.Log($"[StandaloneBattleDemo] OnCameraZoom zoom: {zoom}");

    }

    private void OnCameraPan(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Vector2 panInput  = _controls.Camera.Pan.ReadValue<Vector2>();
            // _mainCamera.transform.Translate(panInput.x, 0, panInput.y);

            zUDebug.Log($"[StandaloneBattleDemo] OnCameraPan panInput: {panInput}");
        }
        else if (context.canceled)
        {
            Vector2 panInput  = _controls.Camera.Pan.ReadValue<Vector2>();

            zUDebug.Log($"[StandaloneBattleDemo] OnCameraPan end, panInput : {panInput}");
        }
    }

    /// <summary>
    /// 响应创建单位的输入（使用Command系统）
    /// </summary>
    private void OnCreateUnit(InputAction.CallbackContext context)
    {
        if (_game == null || _game.World == null)
        {
            return;
        }

        // 获取鼠标/触摸位置
        Vector2 screenPosition = Pointer.current.position.ReadValue();

        // 检测点击到的gameobject对象，并设置给相机目标RTSCameraTargetController.Instance
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            // 如果没有点击到建筑，则开始框选
            StartSelectionBox(screenPosition);
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
        if (IsSelectMode)
        {
            isSelecting = true;
            isClickOnUnit = false; // 重置单位点击状态
            selectionStartPoint = screenPosition;
            selectionEndPoint = screenPosition;
        }
        else
        {
            // 相机移动模式：记录初始鼠标位置和相机位置
            isSelecting = true;
            selectionStartPoint = screenPosition;
            selectionEndPoint = screenPosition;
            
            // 记录相机初始位置
            if (RTSCameraTargetController.Instance != null && RTSCameraTargetController.Instance.CameraTarget != null)
            {
                m_CameraInitialPosition = RTSCameraTargetController.Instance.CameraTarget.position;
            }
        }
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

            if (IsSelectMode)
            {
                // 选择模式：计算拖拽距离
                float dragDistance = Vector2.Distance(selectionStartPoint, selectionEndPoint);

                // 只有当拖拽距离超过阈值时才真正激活框选
                if (dragDistance > dragThreshold)
                {
                    // 激活框选可视化
                    isSelecting = true;
                }
            }
            else
            {
                // 相机移动模式：根据鼠标移动偏移量移动相机
                Vector2 delta = selectionEndPoint - selectionStartPoint;
                
                // 转换为世界坐标偏移量（考虑相机高度和视野）
                if (RTSCameraTargetController.Instance != null && _mainCamera != null)
                {
                    // 获取相机到地面的距离
                    float cameraHeight = RTSCameraTargetController.Instance.CameraTarget.position.y;
                    
                    // 降低移动速度，使操作更加精确
                    float moveSpeed = cameraHeight * 0.002f;
                    
                    // 计算世界坐标偏移量
                    Vector3 worldDelta = new Vector3(-delta.x * moveSpeed, 0, -delta.y * moveSpeed);
                    
                    // 转换为相对于相机朝向的移动方向
                    Vector3 forward = _mainCamera.transform.forward;
                    Vector3 right = _mainCamera.transform.right;
                    forward.y = 0;
                    right.y = 0;
                    forward.Normalize();
                    right.Normalize();
                    
                    Vector3 relativeDelta = forward * worldDelta.z + right * worldDelta.x;
                    
                    // 更新相机位置
                    RTSCameraTargetController.Instance.CameraTarget.position = m_CameraInitialPosition - relativeDelta;
                }
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

        // 仅在选择模式下执行框选逻辑
        if (IsSelectMode)
        {
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
        // 相机移动模式不需要额外处理，只需重置isSelecting状态即可
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

        // 绘制框选区域
        DrawSelectionBox();

        // 绘制小地图
        // DrawMiniMap();

        DrawGMConsole();
    }

    /// <summary>
    /// 开始建筑放置模式
    /// </summary>
    /// <param name="buildingType">建筑类型 (3=采矿场, 4=电厂, 5=坦克工厂)</param>
    public void StartBuildingPlacement(BuildingType buildingType)
    {
        buildingToBuild = buildingType;
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
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
    /// 绘制框选区域
    /// </summary>
    private void DrawSelectionBox()
    {
        // 只有当真正激活框选时才绘制（拖拽距离超过阈值且未点击在单位上）
        if (isSelecting && !isClickOnUnit && IsSelectMode)
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
    /// 清除所有单位的OutlineComponent
    /// </summary>
    public void ClearAllOutlines()
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
    /// 获取当前选中的单位ID列表
    /// </summary>
    /// <returns>选中的单位ID列表的副本</returns>
    public List<int> GetSelectedEntityIds()
    {
        return new List<int>(selectedEntityIds);
    }

    /// <summary>
    /// 设置当前选中的单位ID列表
    /// </summary>
    /// <param name="entityIds">要设置为选中状态的单位ID列表</param>
    public void SetSelectedEntityIds(List<int> entityIds)
    {
        // 清除当前选择
        ClearAllOutlines();
        
        // 更新选择列表
        selectedEntityIds.Clear();
        if (entityIds != null)
        {
            selectedEntityIds.AddRange(entityIds);
        }
    }

    /// <summary>
    /// 为指定实体启用OutlineComponent
    /// </summary>
    /// <param name="entityId">实体ID</param>
    /// <returns>如果成功启用轮廓返回true，否则返回false</returns>
    public bool EnableOutlineForEntity(int entityId)
    {
        if (_game == null || _game.World == null || _presentationSystem == null)
            return false;

        var entity = new Entity(entityId);
        // 检查实体是否存在（通过检查是否有TransformComponent组件来判断实体是否存在）
        if (!_game.World.ComponentManager.HasComponent<TransformComponent>(entity))
            return false;

        if (_game.World.ComponentManager.HasComponent<ViewComponent>(entity))
        {
            var viewComponent = _game.World.ComponentManager.GetComponent<ViewComponent>(entity);
            if (viewComponent != null && viewComponent.GameObject != null)
            {
                var outlineComponent = viewComponent.GameObject.GetComponent<OutlineComponent>();
                if (outlineComponent != null)
                {
                    outlineComponent.enabled = true;
                    return true; // 成功启用轮廓
                }
            }
        }
        return false; // 启用轮廓失败
    }

    /// <summary>
    /// 为指定实体禁用OutlineComponent
    /// </summary>
    /// <param name="entityId">实体ID</param>
    public void DisableOutlineForEntity(int entityId)
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

    public void SetBattleGame(BattleGame game)
    {
        _game = game;
    }


}