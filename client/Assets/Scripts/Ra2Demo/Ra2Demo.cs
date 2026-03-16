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
using ZLib;
using PostHogUnity;
using WeChatWASM;
using Unity.Collections;

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


    [Header("Unity 资源")]
    [SerializeField] private Transform viewRoot;
    [SerializeField] public GameObject[] unitPrefabs = new GameObject[10];
    private PresentationSystem _presentationSystem;


    [Header("小地图设置")]
    private MiniMapController miniMapController; // 小地图控制器

    public RTSControl _controls;
    public Camera _mainCamera;
    
    /// <summary>
    /// 设置最大选择数量
    /// </summary>
    /// <param name="count">最大选择数量，0 表示不限制</param>
    public void SetMaxSelectionCount(int count)
    {
        maxSelectionCount = Mathf.Max(0, count);
        zUDebug.Log($"[Ra2Demo] 最大选择数量已设置为：{maxSelectionCount} (0 表示不限制)");
    }
    
    /// <summary>
    /// 获取当前最大选择数量
    /// </summary>
    /// <returns>最大选择数量，0 表示不限制</returns>
    public int GetMaxSelectionCount()
    {
        return maxSelectionCount;
    }
    
    /// <summary>
    /// 设置单位选择半径
    /// </summary>
    /// <param name="radius">选择半径（米）</param>
    public void SetUnitSelectionRadius(float radius)
    {
        unitSelectionRadius = radius;
        zUDebug.Log($"[Ra2Demo] 单位选择半径已设置为：{radius}m");
    }
    
    /// <summary>
    /// 获取当前单位选择半径
    /// </summary>
    /// <returns>选择半径（米）</returns>
    public float GetUnitSelectionRadius()
    {
        return unitSelectionRadius;
    }

    /**************************************
     * UI 部分
     *************************************/

         // 相机移动相关的常量
    private const float JOYSTICK_CAMERA_MOVE_SPEED = 0.02f;  // 虚拟摇杆相机移动速度
    private const float CAMERA_MOVE_ZONE_WIDTH_RATIO = 0.33f;  // 相机移动区域宽度比例（左侧 1/3 屏幕）
    
    // 选择模式/（相机）移动模式
    public bool IsSelectMode { get; set; } = true;


    private void Awake()
    {
        // FIX ME: 临时解决内存泄漏
        NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;

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

        zUDebug.Log("[StandaloneBattleDemo] 视图系统初始化完成");
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
        PostHog.Setup(
            new PostHogConfig
            {
                ApiKey = "phc_MCpygzl60lEcEwRKwIo2H7D5iVu6vtB7kHrtJCMgvEw",
                Host = "https://us.i.posthog.com",
                LogLevel = PostHogLogLevel.Debug, // Set to Warning or Error in production
            }
        );

        // Capture a simple event
        PostHog.Capture("app_started");

        // Capture an event with properties
        PostHog.Capture(
            "level_started",
            new Dictionary<string, object> { { "level_id", 1 }, { "difficulty", "normal" } }
        );

        frame = new Frame();

        NetworkManager.Instance.SetRa2Demo(this);

        DiscoverTools.Discover(typeof(Main).Assembly);
        Frame.DispatchEvent(new Ra2StartUpEvent(this));
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
    }

    private void OnDisable()
    {
        _controls.Create.Disable();
        _controls.Create.Press.performed -= OnPress;
        _controls.Create.Drag.performed -= OnDrag;
        _controls.Create.Release.performed -= OnRelease;
    }

    // 操作状态枚举
    private enum InputActionState
    {
        None,
        PressStarted,      // 按下开始
        Dragging,          // 拖拽中
        ReleasePerformed   // 释放执行
    }

    // 输入操作状态
    private Vector2 pressStartPosition; // 按下时的起始位置
    private Vector2 currentPosition; // 当前位置
    private Vector2 dragStartScreenPos; // 拖拽起始屏幕位置（用于绘制虚线）
    private Vector2 dragCurrentScreenPos; // 拖拽当前屏幕位置（用于绘制虚线）
    
    // 输入事件队列 - 解决同一帧内多个输入事件的顺序处理问题
    private System.Collections.Generic.Queue<InputEvent> inputEventQueue = new System.Collections.Generic.Queue<InputEvent>();
    
    // 输入事件结构
    private struct InputEvent
    {
        public InputActionState State;
        public Vector2 Position;
        public float Timestamp;
    }
    
    // 单位移动模式相关字段
    private bool isUnitMoveMode = false; // 是否处于单位移动模式
    private float unitSelectionRadius = 5f; // 单位选择半径（米）
    private float tapSelectRadius = 5f; // 单击时点击的半径（米）
    
    // 相机移动相关字段
    private Vector3 m_CameraInitialPosition = Vector3.zero; // 相机初始位置
    private bool isCameraMoving = false; // 是否正在移动相机
    
    // 选择相关字段
    private List<int> selectedEntityIds = new List<int>(); // 多选单位列表
    [Header("选择限制")]
    [Tooltip("最大可选择单位数量，0 表示不限制")]
    [SerializeField] private int maxSelectionCount = 0; // 默认为 0，不限制数量
    
    // 虚线绘制相关字段
    private bool shouldDrawDragLine = false; // 是否应该绘制拖拽虚线
    
    // 选择框资源管理字段
    private GameObject selectionBoxInstance = null; // 选择框资源实例
    private const string SELECTION_BOX_PREFAB_PATH = "Prefabs/Ring"; // 选择框预制体路径（需要根据实际资源调整）
    
    // 淡出效果相关字段
    private bool isFadingOut = false; // 是否正在淡出
    private float fadeOutStartTime = 0f; // 淡出开始时间
    private const float FADE_OUT_DURATION = 0.3f; // 淡出持续时间（秒）
    
    private void OnPress(InputAction.CallbackContext context)
    {
        // 将事件加入队列而非直接设置状态
        inputEventQueue.Enqueue(new InputEvent
        {
            State = InputActionState.PressStarted,
            Position = GetCurrentInputPosition(),
            Timestamp = Time.time
        });
        
        zUDebug.Log($"[StandaloneBattleDemo] OnPress - 加入队列");
    }

    private void OnDrag(InputAction.CallbackContext context)
    {
        // 将事件加入队列而非直接设置状态
        inputEventQueue.Enqueue(new InputEvent
        {
            State = InputActionState.Dragging,
            Position = context.ReadValue<Vector2>(),
            Timestamp = Time.time
        });
    }

    private void OnRelease(InputAction.CallbackContext context)
    {
        zUDebug.Log($"[StandaloneBattleDemo] OnRelease - 加入队列");
        
        // 将事件加入队列而非直接设置状态
        inputEventQueue.Enqueue(new InputEvent
        {
            State = InputActionState.ReleasePerformed,
            Position = GetCurrentInputPosition(),
            Timestamp = Time.time
        });
    }

    /// <summary>
    /// 在 Update中处理输入逻辑 - 按队列顺序处理所有事件
    /// </summary>
    private void ProcessInputLogic()
    {
        // 按顺序处理队列中的所有事件
        while (inputEventQueue.Count > 0)
        {
            var inputEvent = inputEventQueue.Dequeue();
            
            // zUDebug.Log($"[StandaloneBattleDemo] ProcessInputLogic - 处理事件：{inputEvent.State}, 位置：{inputEvent.Position}");
            
            switch (inputEvent.State)
            {
                case InputActionState.PressStarted:
                    pressStartPosition = inputEvent.Position;
                    currentPosition = inputEvent.Position;
                    dragStartScreenPos = inputEvent.Position;
                    shouldDrawDragLine = false;
                    HandlePressLogic();
                    break;
                    
                case InputActionState.Dragging:
                    currentPosition = inputEvent.Position;
                    dragCurrentScreenPos = inputEvent.Position;
                    shouldDrawDragLine = true;
                    HandleDragLogic();
                    break;
                    
                case InputActionState.ReleasePerformed:
                    currentPosition = inputEvent.Position;
                    shouldDrawDragLine = false;
                    HandleReleaseLogic();
                    break;
            }
        }
    }

    /// <summary>
    /// 处理按下逻辑
    /// </summary>
    private void HandlePressLogic()
    {
        zUDebug.Log($"[StandaloneBattleDemo] HandlePressLogic - 尝试处理点击");

        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }

        // 尝试获取点击位置的世界坐标
        Vector3 worldPosition = Vector3.zero;
        bool hasGroundPosition = TryGetGroundPosition(pressStartPosition, out worldPosition);

        bool hasUnitInRange = false;
        if (hasGroundPosition && _game != null && _game.World != null)
        {
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

                if (distance <= tapSelectRadius)
                {
                    hasUnitInRange = true;
                    break;
                }
            }
        }

        if (hasUnitInRange)
        {
            // 清空之前的选择
            ClearAllOutlines();
            
            // 收集所有范围内的单位及其距离
            List<(int entityId, float distance)> unitsInRange = new List<(int, float)>();
            
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

                if (distance <= unitSelectionRadius)
                {
                    unitsInRange.Add((entityId, distance));
                }
            }
            
            // 按距离排序，选择最近的单位
            unitsInRange.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            // 根据最大选择数量限制，选择最近的单位
            int selectCount = maxSelectionCount > 0 
                ? Mathf.Min(unitsInRange.Count, maxSelectionCount) 
                : unitsInRange.Count;
            
            for (int i = 0; i < selectCount; i++)
            {
                int entityId = unitsInRange[i].entityId;
                
                // 添加到选中列表
                selectedEntityIds.Add(entityId);
                
                // 启用轮廓显示
                EnableOutlineForEntity(entityId);
                
                zUDebug.Log($"[StandaloneBattleDemo] 选中单位：EntityId={entityId}, Distance={unitsInRange[i].distance:F2}m");
            }
            
            zUDebug.Log($"[StandaloneBattleDemo] 共检测到 {unitsInRange.Count} 个单位在范围内，实际选择 {selectCount} 个单位");
        }

        // 根据是否有单位在范围内决定模式
        if (hasUnitInRange && selectedEntityIds.Count > 0)
        {
            // 进入单位移动模式
            isUnitMoveMode = true;
            isCameraMoving = false;
            
            // 创建并显示选择框资源实例，传入世界坐标位置
            CreateAndShowSelectionBox(worldPosition);
            
            zUDebug.Log($"[StandaloneBattleDemo] >>> 进入单位移动模式，点击位置：{worldPosition}, 选中单位数：{selectedEntityIds.Count}");
        }
        else
        {
            // 进入相机移动模式（不再判断 1/4 屏幕范围）
            isUnitMoveMode = false;
            isCameraMoving = true;
            
            // 记录相机初始位置
            if (RTSCameraTargetController.Instance != null && RTSCameraTargetController.Instance.CameraTarget != null)
            {
                m_CameraInitialPosition = RTSCameraTargetController.Instance.CameraTarget.position;
            }
            
            zUDebug.Log($"[StandaloneBattleDemo] >>> 进入相机移动模式，起始位置：{pressStartPosition}, 相机初始位置：{m_CameraInitialPosition}");
        }
    }

    /// <summary>
    /// 处理拖拽逻辑
    /// </summary>
    private void HandleDragLogic()
    {
        if (EventSystem.current.IsPointerOverGameObject()) {
            return;
        }

        // 如果是单位移动模式
        if (isUnitMoveMode)
        {
            // 启用虚线绘制标志
            shouldDrawDragLine = true;
            // zUDebug.Log($"[StandaloneBattleDemo] HandleDragLogic [单位移动模式] - 忽略拖拽，等待释放");
        }
        // 如果是相机移动模式
        else if (isCameraMoving)
        {
            // 直接移动相机，不需要阈值判断
            MoveCameraByDrag(currentPosition);
        }
    }

    /// <summary>
    /// 处理释放逻辑
    /// </summary>
    private void HandleReleaseLogic()
    {
        zUDebug.Log($"[StandaloneBattleDemo] HandleReleaseLogic - StartPos: {pressStartPosition}, EndPos: {currentPosition}");

        // 检查是否之前处于按下状态（即发生了拖拽）
        float totalDragDistance = Vector2.Distance(pressStartPosition, currentPosition);
        
        // 如果是单位移动模式
        if (isUnitMoveMode)
        {
            zUDebug.Log($"[StandaloneBattleDemo] >>> 单位移动模式结束 - StartPos: {pressStartPosition}, EndPos: {currentPosition}");
            
            // 隐藏选择框实例
            HideSelectionBox();
            
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

                String entityIds = "";
                foreach (var entityId in selectedEntityIds)
                {
                    entityIds += entityId + ",";
                }

                // TODO PostHog 记录移动，包括 currentPosition 和 targetWorldPosition
                PostHog.Capture("move_to_target", new Dictionary<string, object>()
                {
                    {"entityIds", entityIds},
                    {"pressStartPosition", pressStartPosition},
                    {"targetPosition", currentPosition},
                });
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
            // zUDebug.Log($"[StandaloneBattleDemo] 相机移动 - ScreenDelta: {screenDelta}, Height: {cameraHeight:F2}, WorldDelta: {worldDelta}, NewPos: {newPosition}");
        }
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
    /// LateUpdate - 在每帧的 LateUpdate 中更新血量条位置
    /// </summary>
    private void LateUpdate()
    {
        // 处理输入逻辑（在 Update中执行，而非输入回调中）
        ProcessInputLogic();
        // 触发 HealthPanel 的 LateUpdate 事件
        Frame.DispatchEvent(new HealthPanelLateUpdateEvent());
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
    /// 表现系统：插值更新
    /// </summary>
    private void Update()
    {
        // 表现系统：插值更新
        if (_presentationSystem != null)
        {
            _presentationSystem.LerpUpdate(Time.deltaTime, 10f);
        }

        // 更新淡出效果
        UpdateFadeOut();
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

    private void OnGUI()
    {
        // 绘制拖拽虚线（单位移动模式）
        DrawDragLine();
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
            // 如果unitSelectionRadius==15，设置size*3，否则默认尺寸
            if (unitSelectionRadius == 15)
            {
                selectionBoxInstance.transform.localScale = new Vector3(3, 1, 3);
            }
            else
            {
                selectionBoxInstance.transform.localScale = new Vector3(1, 1, 1);
            }
            
            // 恢复透明度为完全不透明
            var renderer = selectionBoxInstance.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Material material = renderer.material;
                Color color = material.color;
                material.color = new Color(color.r, color.g, color.b, 0.5f);
            }
            
            selectionBoxInstance.SetActive(true);
            zUDebug.Log($"[StandaloneBattleDemo] 选择框已重新激活并更新位置：{worldPosition}");
        }
        else
        {
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
                return;
            }
        }

        // 设置淡出定时器：0.5 秒后开始淡出
        fadeOutStartTime = Time.time + 0.3f;
        isFadingOut = true;
    }

    /// <summary>
    /// 淡出选择框（在 Update 中调用）
    /// </summary>
    private void UpdateFadeOut()
    {
        if (!isFadingOut || selectionBoxInstance == null)
            return;

        // 检查是否到了开始淡出的时间
        if (Time.time < fadeOutStartTime)
            return;

        // 获取 Renderer 组件
        var renderer = selectionBoxInstance.GetComponentInChildren<Renderer>();

        if (renderer != null)
        {
            float elapsed = Time.time - fadeOutStartTime;
            
            if (elapsed >= FADE_OUT_DURATION)
            {
                zUDebug.Log($"[StandaloneBattleDemo] 选择框淡出完成：{elapsed} / {FADE_OUT_DURATION}");
                // 淡出完成，隐藏对象
                HideSelectionBox();
                isFadingOut = false;
                
                // 恢复原始颜色（下次使用时正常显示）
                Material resetMaterial = selectionBoxInstance.GetComponentInChildren<Renderer>()?.material;
                if (resetMaterial != null)
                {
                    Color originalColor = resetMaterial.color;
                    resetMaterial.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
                }
            }
            else
            {
                zUDebug.Log($"[StandaloneBattleDemo] 选择框淡出中：{elapsed} / {FADE_OUT_DURATION}");
                // 计算透明度（线性插值）
                float t = elapsed / FADE_OUT_DURATION;
                Material material = renderer.material;
                Color originalColor = material.color;
                
                // 设置新的透明度
                material.color = new Color(
                    originalColor.r,
                    originalColor.g,
                    originalColor.b,
                    Mathf.Lerp(0.5f, 0f, t)
                );
            }
        }
        else
        {
            // 如果没有 Renderer，直接隐藏
            HideSelectionBox();
            isFadingOut = false;
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