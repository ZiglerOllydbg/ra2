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
using Unity.VisualScripting;
using UnityEngine.EventSystems;

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

    public RTSControl _controls;
    public Camera _mainCamera;
    // 添加多选支持相关字段
    private List<int> selectedEntityIds = new List<int>(); // 多选单位列表
    private bool isSelecting = false; // 是否正在框选
    private bool isClickOnUnit = false; // 鼠标按下时是否点击在单位上
    private Vector2 selectionStartPoint; // 框选起始点
    private Vector2 selectionEndPoint; // 框选结束点
    private const float dragThreshold = 5f; // 拖拽阈值，像素单位
    private Vector3 m_CameraInitialPosition = Vector3.zero; // 相机初始位置

    // 拖拽检测相关字段
    private bool isPressing = false; // 是否正在按下状态
    private Vector2 pressStartPosition; // 按下时的起始位置
    private Vector2 currentPosition; // 当前位置
    private const float DRAG_THRESHOLD = 5f; // 拖拽阈值，像素单位
    
    // 单位移动模式相关字段
    private bool isUnitMoveMode = false; // 是否处于单位移动模式
    private const float UNIT_SELECTION_RADIUS = 5f; // 单位选择半径（米）
    
    // 虚线绘制相关字段
    private Vector2 dragStartScreenPos; // 拖拽起始屏幕位置（用于绘制虚线）
    private Vector2 dragCurrentScreenPos; // 拖拽当前屏幕位置（用于绘制虚线）
    private bool shouldDrawDragLine = false; // 是否应该绘制拖拽虚线
    
    // 选择框资源管理字段
    private GameObject selectionBoxInstance = null; // 选择框资源实例
    private const string SELECTION_BOX_PREFAB_PATH = "Prefabs/Ring"; // 选择框预制体路径（需要根据实际资源调整）

    /**************************************
     * UI 部分
     *************************************/

         // 相机移动相关的常量
    private const float JOYSTICK_CAMERA_MOVE_SPEED = 0.02f;  // 虚拟摇杆相机移动速度
    private const float CAMERA_MOVE_ZONE_WIDTH_RATIO = 0.33f;  // 相机移动区域宽度比例（左侧 1/3 屏幕）
    
    // 相机移动状态字段
    private bool isCameraMoving = false; // 是否正在移动相机

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
        // 初始化命令映射
        CommandMapper.Initialize();

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
        _controls.Create.Press.performed += OnPress;
        _controls.Create.Drag.performed += OnDrag;
        _controls.Create.Release.performed += OnRelease;
        _controls.Create.Move.performed += OnMove;
        _controls.Create.Move.canceled += OnMove;
    }

    private void OnDisable()
    {
        _controls.Create.Disable();
        _controls.Create.Press.performed -= OnPress;
        _controls.Create.Drag.performed -= OnDrag;
        _controls.Create.Release.performed -= OnRelease;
        _controls.Create.Move.performed -= OnMove;
        _controls.Create.Move.canceled -= OnMove;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        // 更新输入向量值
        Vector2 inputVector = context.ReadValue<Vector2>();
        _currentInputVector = inputVector;
        
        // 检查输入动作的阶段
        if (context.performed)
        {
            // 开始移动时，设置移动标志
            _isMoving = true;
        }
        else if (context.canceled)
        {
            // 输入取消时，取消移动标志
            _isMoving = false;
        }
        // 关键修改：无论是否触发了performed或canceled事件，只要输入值为零就停止移动
        else if (inputVector == Vector2.zero)
        {
            _isMoving = false;
        }
        // 如果输入值不为零，说明有移动输入，设置移动标志
        else if (inputVector != Vector2.zero)
        {
            _isMoving = true;
        }
        
        zUDebug.Log($"[StandaloneBattleDemo] OnMove, value={inputVector}, phase={context.phase}, isMoving={_isMoving}");
    }
    
    private Vector2 _currentInputVector = Vector2.zero;
    private bool _isMoving = false;
    
    private void UpdateCameraMovement()
    {
        // 在Update中处理相机移动，仅当处于移动状态时
        if (_isMoving && RTSCameraTargetController.Instance != null && _mainCamera != null && _currentInputVector != Vector2.zero)
        {
            // 获取相机到地面的距离
            float cameraHeight = RTSCameraTargetController.Instance.CameraTarget.position.y;
            
            // 计算移动速度，与相机高度成比例
            float moveSpeed = cameraHeight * JOYSTICK_CAMERA_MOVE_SPEED;
            
            // 计算世界坐标偏移量
            Vector3 worldDelta = new Vector3(-_currentInputVector.x * moveSpeed, 0, -_currentInputVector.y * moveSpeed);
            
            // 转换为相对于相机朝向的移动方向
            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            
            Vector3 relativeDelta = forward * worldDelta.z + right * worldDelta.x;
            
            // 更新相机位置
            RTSCameraTargetController.Instance.CameraTarget.position += relativeDelta;
        }
    }

    private void OnPress(InputAction.CallbackContext context)
    {
        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }

        zUDebug.Log($"[StandaloneBattleDemo] OnPress, performed={context.performed}");
        
        // 记录按下状态和起始位置，支持鼠标和触摸
        isPressing = true;
        pressStartPosition = GetCurrentInputPosition();
        
        // 记录拖拽起始屏幕位置（用于绘制虚线）
        dragStartScreenPos = pressStartPosition;
        shouldDrawDragLine = false; // 初始不绘制虚线

        zUDebug.Log($"[StandaloneBattleDemo] OnPress - pressStartPosition: {pressStartPosition}");

        // 尝试获取点击位置的世界坐标
        Vector3 worldPosition = Vector3.zero;
        bool hasGroundPosition = TryGetGroundPosition(pressStartPosition, out worldPosition);

        // 检测 5 米范围内是否有单位（只检测本地玩家的单位）
        bool hasUnitInRange = false;
        if (hasGroundPosition && _game != null && _game.World != null)
        {
            // 清空之前的选择
            ClearAllOutlines();
            
            var entities = _game.World.ComponentManager
                .GetAllEntityIdsWith<TransformComponent>();

            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                
                // 检查实体是否包含 LocalPlayerComponent（只有本地玩家单位才能被选择）
                if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
                {
                    continue;
                }

                if (!_game.World.ComponentManager.HasComponent<UnitComponent>(entity))
                {
                    continue; // 跳过非单位实体
                }

                var transform = _game.World.ComponentManager
                    .GetComponent<TransformComponent>(entity);

                // 计算与点击位置的距离
                Vector3 unitWorldPosition = transform.Position.ToVector3();
                float distance = Vector3.Distance(unitWorldPosition, worldPosition);

                if (distance <= UNIT_SELECTION_RADIUS)
                {
                    hasUnitInRange = true;
                    
                    // 添加到选中列表
                    selectedEntityIds.Add(entityId);
                    
                    // 启用轮廓显示
                    EnableOutlineForEntity(entityId);
                    
                    zUDebug.Log($"[StandaloneBattleDemo] 检测到单位在范围内：EntityId={entityId}, Distance={distance:F2}m");
                }
            }
        }

        // 根据是否有单位在范围内决定模式
        if (hasUnitInRange)
        {
            // 进入单位移动模式
            isUnitMoveMode = true;
            isCameraMoving = false;
            isSelecting = false;
            
            // 创建并显示选择框资源实例，传入世界坐标位置
            // CreateAndShowSelectionBox(worldPosition);
            
            zUDebug.Log($"[StandaloneBattleDemo] >>> 进入单位移动模式，点击位置：{worldPosition}, 选中单位数：{selectedEntityIds.Count}");
        }
        else
        {
            // 进入相机移动模式（不再判断 1/4 屏幕范围）
            isUnitMoveMode = false;
            isCameraMoving = true;
            isSelecting = false;
            
            // 记录相机初始位置
            if (RTSCameraTargetController.Instance != null && RTSCameraTargetController.Instance.CameraTarget != null)
            {
                m_CameraInitialPosition = RTSCameraTargetController.Instance.CameraTarget.position;
            }
            
            zUDebug.Log($"[StandaloneBattleDemo] >>> 进入相机移动模式，起始位置：{pressStartPosition}, 相机初始位置：{m_CameraInitialPosition}");
        }
    }

    private void OnDrag(InputAction.CallbackContext context)
    {
        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }

        currentPosition = context.ReadValue<Vector2>();
        
        // 更新当前屏幕位置（用于绘制虚线）
        dragCurrentScreenPos = currentPosition;

        // 仅在按下状态下处理拖拽
        if (isPressing)
        {
            float dragDistance = Vector2.Distance(pressStartPosition, currentPosition);
            
            // 如果是单位移动模式
            if (isUnitMoveMode)
            {
                zUDebug.Log($"[StandaloneBattleDemo] OnDrag [单位移动模式] - 忽略拖拽，等待释放");
                
                // 启用虚线绘制标志
                shouldDrawDragLine = true;
            }
            // 如果是相机移动模式
            else if (isCameraMoving)
            {
                // 详细记录相机移动信息
                zUDebug.Log($"[StandaloneBattleDemo] OnDrag [相机移动] - CurrentPos: {currentPosition}, StartPos: {pressStartPosition}, Distance: {dragDistance:F2}");
                // 直接移动相机，不需要阈值判断
                MoveCameraByDrag(currentPosition);
            }
            else
            {
                // 选择模式：只有当拖拽距离超过阈值时才认为是有效框选
                if (dragDistance > DRAG_THRESHOLD)
                {
                    zUDebug.Log($"[StandaloneBattleDemo] OnDrag [框选] - CurrentPos: {currentPosition}, StartPos: {pressStartPosition}, Distance: {dragDistance:F2}");
                    UpdateSelectionBox(currentPosition);
                }
            }
        }
    }

    /// <summary>
    /// 根据拖拽偏移移动相机
    /// </summary>
    /// <param name="currentScreenPos">当前屏幕位置</param>
    private void MoveCameraByDrag(Vector2 currentScreenPos)
    {
        if (RTSCameraTargetController.Instance != null && _mainCamera != null)
        {
            // 计算从起始位置的偏移量（屏幕像素）
            Vector2 screenDelta = currentScreenPos - pressStartPosition;
            
            // 获取相机到地面的距离
            float cameraHeight = RTSCameraTargetController.Instance.CameraTarget.position.y;
            
            // 将屏幕像素偏移转换为世界坐标偏移（考虑相机高度）
            // 使用简单的比例关系：屏幕像素 -> 世界单位
            float worldUnitsPerPixel = 0.1f; // 调整转换系数
            Vector3 worldDelta = new Vector3(-screenDelta.x * worldUnitsPerPixel, 0, -screenDelta.y * worldUnitsPerPixel);
            
            // 直接叠加到初始位置
            Vector3 newPosition = m_CameraInitialPosition + worldDelta;
            RTSCameraTargetController.Instance.CameraTarget.position = newPosition;
            
            // 调试日志
            zUDebug.Log($"[StandaloneBattleDemo] 相机移动 - ScreenDelta: {screenDelta}, Height: {cameraHeight:F2}, WorldDelta: {worldDelta}, NewPos: {newPosition}");
        }
    }

    private void OnRelease(InputAction.CallbackContext context)
    {
        zUDebug.Log($"[StandaloneBattleDemo] OnRelease, performed={context.performed}");
        currentPosition = GetCurrentInputPosition();
        
        // 重置虚线绘制标志
        shouldDrawDragLine = false;

        // 检查是否之前处于按下状态（即发生了拖拽）
        if (isPressing)
        {
            float totalDragDistance = Vector2.Distance(pressStartPosition, currentPosition);
            
            // 如果是单位移动模式
            if (isUnitMoveMode)
            {
                zUDebug.Log($"[StandaloneBattleDemo] >>> 单位移动模式结束 - StartPos: {pressStartPosition}, EndPos: {currentPosition}");
                
                // 隐藏选择框实例
                // HideSelectionBox();
                
                // 获取目标位置并发送移动命令
                if (TryGetGroundPosition(currentPosition, out Vector3 targetWorldPosition))
                {
                    zUDebug.Log($"[StandaloneBattleDemo] >>> 目标位置：{targetWorldPosition}");
                    
                    // 使用当前选中的单位列表发送移动命令
                    if (selectedEntityIds.Count > 0)
                    {
                        SendMoveCommand(selectedEntityIds, targetWorldPosition);
                    }
                    else
                    {
                        zUDebug.LogWarning("[StandaloneBattleDemo] >>> 没有找到可移动的单位");
                    }
                }
                
                // 清空选中单位列表
                ClearAllOutlines();
                
                // 重置单位移动模式
                isUnitMoveMode = false;
            }
            // 如果是相机移动模式
            else if (isCameraMoving)
            {
                zUDebug.Log($"[StandaloneBattleDemo] >>> 相机移动结束 - StartPos: {pressStartPosition}, EndPos: {currentPosition}, TotalDistance: {totalDragDistance:F2}, FinalCameraPos: {RTSCameraTargetController.Instance?.CameraTarget?.position}");
                // 重置相机移动状态
                isCameraMoving = false;
            }
            else
            {
                // 选择模式：判断是框选还是点击
                if (totalDragDistance > DRAG_THRESHOLD)
                {
                    zUDebug.Log($"[StandaloneBattleDemo] >>> 框选结束 - TotalDistance: {totalDragDistance:F2}");
                    EndSelectionBox();
                }
                else
                {
                    zUDebug.Log($"[StandaloneBattleDemo] >>> 点击检测 (非拖拽) - Distance: {totalDragDistance:F2}");
                    SendMoveCommandForSelectedUnit(currentPosition);
                }
            }
        }
        
        // 重置拖拽状态
        isPressing = false;
        zUDebug.Log($"[StandaloneBattleDemo] OnRelease - 重置拖拽状态，isPressing=false");
    }

    private Vector2 GetCurrentInputPosition()
    {
        // 优先级获取：先尝试鼠标，再尝试触摸[1,2](@ref)
        if (Mouse.current != null && Mouse.current.position.IsActuated())
        {
            return Mouse.current.position.ReadValue();
        }
        
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.IsActuated())
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                return touch.position.ReadValue();
            }
        }
        
        // 备用方案：如果上述都失败，使用最后已知位置
        return currentPosition;
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

    public void OnConfirmOk()
    {
        zUDebug.Log("OnConfirmOk");
        
        PlaceBuilding();
    }

    public void OnConfirmCancel()
    {
        zUDebug.Log("OnConfirmCancel");
        
        CancelBuilding();
    }

    /// <summary>
    /// 更新建筑预览位置
    /// </summary>
    public void UpdateBuildingPreview()
    {
        // 委托给建筑预览渲染器处理
        if (buildingPreviewRenderer != null)
        {
            Vector2 screenCenterPos = new(Screen.width / 2f, Screen.height / 2f);
            buildingPreviewRenderer.UpdateBuildingPreview(buildingToBuild, true, screenCenterPos);
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

        // 表现系统：插值更新
        if (_presentationSystem != null)
        {
            _presentationSystem.LerpUpdate(Time.deltaTime, 10f);
        }

        // 更新建筑预览
        UpdateBuildingPreview();

        UpdateCameraMovement();
    }

    /// <summary>
    /// 发送移动命令给指定的单位
    /// </summary>
    private void SendMoveCommand(List<int> entityIds, Vector3 targetWorldPosition)
    {
        if (entityIds.Count == 0)
        {
            Debug.Log("[Test] 没有要移动的单位");
            return;
        }

        if (_game == null || _game.World == null)
            return;

        // 验证单位是否有效
        List<int> validEntityIds = new List<int>();
        foreach (int entityId in entityIds)
        {
            var entity = new Entity(entityId);
            if (!_game.World.ComponentManager.HasComponent<UnitComponent>(entity))
            {
                Debug.Log($"[Test] 单位 {entityId} 已不存在");
                DisableOutlineForEntity(entityId);
                continue;
            }

            // 判断拥有 LocalPlayerComponent 就是当前玩家的单位
            if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
            {
                Debug.Log($"[Test] 单位 {entityId} 不属于当前玩家");
                DisableOutlineForEntity(entityId);
                continue;
            }

            validEntityIds.Add(entityId);
        }

        if (validEntityIds.Count == 0)
        {
            Debug.Log("[Test] 没有有效的选中单位");
            return;
        }

        zfloat x = zfloat.CreateFloat((long)(targetWorldPosition.x * zfloat.SCALE_10000));
        zfloat z = zfloat.CreateFloat((long)(targetWorldPosition.z * zfloat.SCALE_10000));

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
        Debug.Log($"[Test] 发送移动命令：{validEntityIds.Count}个单位 → {targetWorldPosition}");
    }

    /// <summary>
    /// 发送移动命令给选中的单位（保留原有方法以兼容框选后的移动）
    /// </summary>
    private void SendMoveCommandForSelectedUnit(Vector2 screenPosition)
    {
        // 检查是否有选中的单位
        if (selectedEntityIds.Count == 0)
        {
            Debug.Log("[Test] 没有选中的单位");
            return;
        }

        if (_game == null || _game.World == null)
            return;

        if (!TryGetGroundPosition(screenPosition, out Vector3 worldPosition))
            return;

        // 使用新的 SendMoveCommand 方法
        SendMoveCommand(selectedEntityIds, worldPosition);
    }

    private void OnGUI()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fixedHeight = 60;
        buttonStyle.fixedWidth = 200;

        // 绘制框选区域
        DrawSelectionBox();
        
        // 绘制拖拽虚线（单位移动模式）
        DrawDragLine();

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
    /// 绘制拖拽虚线（单位移动模式下从起点到目标点）
    /// </summary>
    private void DrawDragLine()
    {
        // 只有在单位移动模式且需要绘制虚线时才绘制
        if (!shouldDrawDragLine || !isUnitMoveMode)
        {
            return;
        }

        // 计算线段长度和角度
        Vector2 delta = dragCurrentScreenPos - dragStartScreenPos;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // 保存当前 GUI 颜色和矩阵
        Color oldColor = GUI.color;
        Matrix4x4 oldMatrix = GUI.matrix;

        // 虚线参数
        float dashLength = 10f; // 每段虚线长度
        float gapLength = 5f;   // 虚线间隔
        float totalDashGap = dashLength + gapLength;
        int dashCount = Mathf.FloorToInt(length / totalDashGap);

        // 计算方向向量
        Vector2 direction = delta.normalized;

        // 绘制多段绿色虚线
        for (int i = 0; i < dashCount; i++)
        {
            float startDist = i * totalDashGap;
            
            // 计算当前虚线段的起始位置
            Vector2 startPos = dragStartScreenPos + direction * startDist;
            
            // 计算当前虚线段的结束位置
            Vector2 endPos = startPos + direction * dashLength;

            // 转换为 GUI 坐标（Y 轴翻转）
            float guiStartX = startPos.x;
            float guiStartY = Screen.height - startPos.y;
            float guiEndXLocal = endPos.x;
            float guiEndYLocal = Screen.height - endPos.y;

            // 绘制当前虚线段（使用绿色）
            Color lineColor = new Color(0.0f, 1.0f, 0.0f, 0.8f); // 绿色虚线
            DrawLine(new Vector2(guiStartX, guiStartY), new Vector2(guiEndXLocal, guiEndYLocal), lineColor, 2f);
        }

        // 恢复 GUI 状态
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;

        // 绘制目标点圆圈（绿色）
        float circleRadius = 8f;
        float guiEndX = dragCurrentScreenPos.x;
        float guiEndY = Screen.height - dragCurrentScreenPos.y;
        
        Rect targetCircleRect = new Rect(
            guiEndX - circleRadius, 
            guiEndY - circleRadius, 
            circleRadius * 2, 
            circleRadius * 2
        );
        
        Color circleColor = new Color(0.0f, 1.0f, 0.0f, 0.8f); // 绿色圆圈
        GUI.color = circleColor;
        GUI.DrawTexture(targetCircleRect, Texture2D.whiteTexture);
        
        // 恢复颜色
        GUI.color = oldColor;
    }

    /// <summary>
    /// 绘制两点之间的线段
    /// </summary>
    private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // 旋转矩形来匹配线的方向
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, start);
        
        Rect rect = new Rect(start.x, start.y, length, thickness);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        
        GUI.matrix = oldMatrix;
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
                    // 调整目标距离
                    factoryPosition.y = 0;
                    RTSCameraTargetController.Instance.CameraTarget.position = factoryPosition;
                    zUDebug.Log($"[Ra2Demo] 相机已移动到我方工厂位置: {factoryPosition}");
                }
                return;
            }
        }

        // 调整相机位置
        Vector3 adjustedPosition = new(RTSCameraTargetController.Instance.CameraTarget.position.x, 0, RTSCameraTargetController.Instance.CameraTarget.position.z);
        RTSCameraTargetController.Instance.CameraTarget.position = adjustedPosition;

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
                    outlineComponent.ShowCircle();
                    // outlineComponent.enabled = true;
                    return true; // 成功启用轮廓
                }
            }
        }
        return false; // 启用轮廓失败
    }

    /// <summary>
    /// 为指定实体禁用 OutlineComponent
    /// </summary>
    /// <param name="entityId">实体 ID</param>
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
                    // outlineComponent.enabled = false;
                    outlineComponent.HideCircle();
                }
            }
        }
    }

    /// <summary>
    /// 创建并显示选择框资源实例
    /// </summary>
    /// <param name="worldPosition">世界坐标位置（只更新 X 和 Z 轴）</param>
    private void CreateAndShowSelectionBox(Vector3 worldPosition)
    {
        worldPosition.y = 0.1f;

        // 如果已有实例，先隐藏它
        if (selectionBoxInstance != null)
        {
            selectionBoxInstance.transform.position = worldPosition;
            
            selectionBoxInstance.SetActive(true);
            zUDebug.Log($"[StandaloneBattleDemo] 选择框已重新激活并更新位置：{worldPosition}");
            return;
        }

        // 实例化选择框预制体
        selectionBoxInstance = ResourceCache.InstantiatePrefab(SELECTION_BOX_PREFAB_PATH);
        
        if (selectionBoxInstance != null)
        {
            selectionBoxInstance.transform.position = worldPosition;
            
            zUDebug.Log($"[StandaloneBattleDemo] 选择框创建成功：{selectionBoxInstance.name}, 位置：{worldPosition}");
        }
        else
        {
            zUDebug.LogWarning($"[StandaloneBattleDemo] 选择框创建失败，请检查资源路径：{SELECTION_BOX_PREFAB_PATH}");
        }
    }

    /// <summary>
    /// 隐藏选择框资源实例
    /// </summary>
    private void HideSelectionBox()
    {
        if (selectionBoxInstance != null)
        {
            selectionBoxInstance.SetActive(false);
            zUDebug.Log("[StandaloneBattleDemo] 选择框已隐藏");
        }
    }

    public void SetBattleGame(BattleGame game)
    {
        _game = game;
    }



}