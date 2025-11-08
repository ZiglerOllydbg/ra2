using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZLockstep.View;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command;
using ZLockstep.Sync.Command.Commands;
using zUnity;
using Game.RA2.Client;
using Game.Examples;
using ZLockstep.Sync;
using ZLockstep.View.Systems;
using ZLockstep.Simulation.ECS.Systems;

/// <summary>
/// 测试脚本：点击地面创建单位（使用Command系统）
/// </summary>
public class Ra2Demo : MonoBehaviour
{
    [Header("游戏世界")]
    [SerializeField] private BattleGame _game;
    [SerializeField] private GameMode Mode;

    [Header("创建设置")]
    [SerializeField] private LayerMask groundLayer = -1; // 地面层
    [SerializeField] private int unitType = 1; // 单位类型：1=动员兵, 2=坦克, 3=矿车
    [SerializeField] private int prefabId = 1; // 预制体ID（对应unitPrefabs数组索引）

    [Header("小地图设置")]
    [SerializeField] private MiniMapController miniMapController; // 小地图控制器
    [SerializeField] private Rect miniMapRect = new Rect(20, -1, 200, 200); // 小地图在屏幕上的位置和大小（使用-1作为特殊值表示从底部计算）
    [SerializeField] private int miniMapMargin = 80; // 小地图边距

    private RTSControl _controls;
    private Camera _mainCamera;
    private RenderTexture _miniMapTexture;
    
    [SerializeField] public string ServerUrl = "ws://101.126.136.178:8080/ws";
    // 添加本地测试选项
    private bool useLocalServer = false;
    private const string LocalServerUrl = "ws://127.0.0.1:8080/ws";
    private const string RemoteServerUrl = "ws://101.126.136.178:8080/ws";

    private WebSocketNetworkAdaptor _networkAdaptor; // 保存网络适配器引用
    private WebSocketClient _client; // 添加WebSocket客户端引用
    
    // 添加状态标志
    private bool isConnected = false;
    private bool isMatched = false;
    private bool isReady = false;
    private bool isPaused = false;
    
    // 添加房间类型选择
    private RoomType selectedRoomType = RoomType.DUO;

    // 添加用于下拉列表显示的变量
    private bool showRoomTypeDropdown = false;
    private string[] roomTypeOptions = { "单人(SOLO)", "双人(DUO)", "三人(TRIO)", "四人(QUAD)", "八人(OCTO)" };
    
    // 添加对RtsCameraController的引用
    private RtsCameraController _rtsCameraController;
    
    // 添加选中单位相关字段
    private int selectedEntityId = -1; // 选中的单位实体ID，-1表示未选中任何单位
    
    // 添加多选支持相关字段
    private List<int> selectedEntityIds = new List<int>(); // 多选单位列表
    private bool isSelecting = false; // 是否正在框选
    private bool isClickOnUnit = false; // 鼠标按下时是否点击在单位上
    private Vector2 selectionStartPoint; // 框选起始点
    private Vector2 selectionEndPoint; // 框选结束点
    private const float dragThreshold = 5f; // 拖拽阈值，像素单位

    [Header("Unity资源")]
    [SerializeField] private Transform viewRoot;
    [SerializeField] private GameObject[] unitPrefabs = new GameObject[10];
    private PresentationSystem _presentationSystem;

    // GM相关
    private bool _isConsoleVisible = false;
    private string _inputFieldGM = "";
    private Vector2 _scrollPosition;
    private readonly List<string> _logHistory = new();

    private void Awake()
    {
        _mainCamera = Camera.main;
        _controls = new RTSControl();

        // 如果没有分配 worldBridge，尝试自动查找
        _game = new BattleGame(Mode, 20, 0);
        _game.Init();

        // 初始化小地图系统
        InitializeMiniMap();

        // 加载保存的房间类型选择和本地服务器选项
        LoadRoomTypeSelection();
        LoadLocalServerOption();

        InitializeUnityView();
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
    
    /// <summary>
    /// 加载保存的房间类型选择
    /// </summary>
    private void LoadRoomTypeSelection()
    {
        if (PlayerPrefs.HasKey("SelectedRoomType"))
        {
            selectedRoomType = (RoomType)PlayerPrefs.GetInt("SelectedRoomType");
        }
        else
        {
            selectedRoomType = RoomType.DUO; // 默认值
        }
    }
    
    /// <summary>
    /// 保存房间类型选择
    /// </summary>
    private void SaveRoomTypeSelection()
    {
        PlayerPrefs.SetInt("SelectedRoomType", (int)selectedRoomType);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 加载本地服务器选项
    /// </summary>
    private void LoadLocalServerOption()
    {
        useLocalServer = PlayerPrefs.GetInt("UseLocalServer", 0) == 1;
    }
    
    /// <summary>
    /// 保存本地服务器选项
    /// </summary>
    private void SaveLocalServerOption()
    {
        PlayerPrefs.SetInt("UseLocalServer", useLocalServer ? 1 : 0);
        PlayerPrefs.Save();
    }

    void Start()
    {

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
            // 检测是否点击到了单位
            isClickOnUnit = CheckIfClickOnUnit(ray);
            
            // 如果点击在单位上，直接选择单位
            if (isClickOnUnit)
            {
                DetectAndSelectUnit(ray);
            }
            else
            {
                // 如果点击在空白区域，开始框选
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
    /// 在指定位置创建单位（通过Command）
    /// </summary>
    private void CreateUnitAtPosition(Vector3 position)
    {
        // 转换为逻辑层坐标
        zVector3 logicPosition = position.ToZVector3();

        // 创建CreateUnitCommand
        var createCommand = new CreateUnitCommand(
            playerId: 0,
            unitType: unitType,
            position: logicPosition,
            prefabId: prefabId
        )
        {
            Source = CommandSource.Local
        };

        // 提交命令到游戏世界
        _game.SubmitCommand(createCommand);

        Debug.Log($"[Test] 提交创建单位命令: 类型={unitType}, 位置={position}");
    }

    /// <summary>
    /// 检测并选择单位
    /// </summary>
    private void DetectAndSelectUnit(Ray ray)
    {
        if (_game == null || _game.World == null)
            return;

        // 射线检测单位
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // 查找对应的逻辑实体
                var entities = _game.World.ComponentManager
                    .GetAllEntityIdsWith<TransformComponent>();
                
                foreach (var entityId in entities)
                {
                    var entity = new Entity(entityId);
                    var transform = _game.World.ComponentManager
                        .GetComponent<TransformComponent>(entity);
                    
                    // 比较位置来确定是否是同一个单位（简化实现）
                    Vector3 logicPosition = transform.Position.ToVector3();
                    if (Vector3.Distance(logicPosition, hit.point) < 1.0f)
                    {
                        // 检查是否按住Ctrl键进行多选
                        if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
                        {
                            // 如果已经选中则取消选中，否则添加到选中列表
                            if (selectedEntityIds.Contains(entityId))
                            {
                                selectedEntityIds.Remove(entityId);
                                Debug.Log($"[Test] 取消选中单位: EntityId={entityId}");
                            }
                            else
                            {
                                selectedEntityIds.Add(entityId);
                                Debug.Log($"[Test] 添加选中单位: EntityId={entityId}");
                            }
                        }
                        else
                        {
                            // 单选模式：清空之前选择并选中当前单位
                            selectedEntityIds.Clear();
                            selectedEntityIds.Add(entityId);
                            Debug.Log($"[Test] 选中单位: EntityId={entityId}");
                        }
                        
                        // 兼容旧的单选变量
                        selectedEntityId = selectedEntityIds.Count > 0 ? selectedEntityIds[0] : -1;
                        return;
                    }
                }
        }
    }
    
    /// <summary>
    /// 检查是否点击在单位上
    /// </summary>
    /// <param name="ray">射线</param>
    /// <returns>是否点击在单位上</returns>
    private bool CheckIfClickOnUnit(Ray ray)
    {
        if (_game == null || _game.World == null)
            return false;

        // 射线检测单位
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // 查找对应的逻辑实体
            var entities = _game.World.ComponentManager
                .GetAllEntityIdsWith<TransformComponent>();
            
            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                var transform = _game.World.ComponentManager
                    .GetComponent<TransformComponent>(entity);
                
                // 比较位置来确定是否是同一个单位（简化实现）
                Vector3 logicPosition = transform.Position.ToVector3();
                if (Vector3.Distance(logicPosition, hit.point) < 1.0f)
                {
                    return true;
                }
            }
        }
        
        return false;
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
        // 如果鼠标按下时点击在单位上，则不执行框选逻辑
        if (isClickOnUnit)
        {
            isSelecting = false;
            isClickOnUnit = false;
            return;
        }

        if (!isSelecting) return;

        isSelecting = false;
        isClickOnUnit = false;

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

        // 检查是否按住Ctrl键进行多选
        bool isMultiSelect = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

        // 如果不是多选模式，清空之前的选择
        if (!isMultiSelect)
        {
            selectedEntityIds.Clear();
        }

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

                // 将世界坐标转换为屏幕坐标
                Vector3 worldPosition = transform.Position.ToVector3();
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPosition);

                // 注意：屏幕坐标的Y轴是从下往上增长的
                // selectionRect也使用相同的坐标系统，所以不需要转换

                // 检查单位是否在框选区域内
                if (selectionRect.Contains(screenPos))
                {
                    // 如果已经选中则跳过，否则添加到选中列表
                    if (!selectedEntityIds.Contains(entityId))
                    {
                        selectedEntityIds.Add(entityId);
                        Debug.Log($"[Test] 框选添加单位: EntityId={entityId}");
                    }
                }
            }

            // 兼容旧的单选变量
            selectedEntityId = selectedEntityIds.Count > 0 ? selectedEntityIds[0] : -1;
        }
    }
    
    private void FixedUpdate()
    {
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
        
        // 右键点击发送移动命令
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            SendMoveCommandForSelectedUnit();
        }

        _networkAdaptor?.OnDispatchMessageQueue();
        

        // 表现系统：插值更新
        if (_presentationSystem != null)
        {
            _presentationSystem.LerpUpdate(Time.deltaTime, 10f);
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
                continue;
            }

            // 判断拥有LocalPlayerComponent就是当前玩家的单位
            if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
            {
                Debug.Log($"[Test] 选中的单位 {entityId} 不属于当前玩家");
                continue;
            }
            
            validEntityIds.Add(entityId);
        }
        
        // 更新选中单位列表，移除无效单位
        selectedEntityIds = new List<int>(validEntityIds);
        
        // 兼容旧的单选变量
        selectedEntityId = selectedEntityIds.Count > 0 ? selectedEntityIds[0] : -1;

        if (validEntityIds.Count == 0)
        {
            Debug.Log("[Test] 没有有效的选中单位");
            return;
        }

        zfloat x = zfloat.CreateFloat((long)(worldPosition.x * zfloat.SCALE_10000));
        zfloat z = zfloat.CreateFloat((long)(worldPosition.z * zfloat.SCALE_10000));

        // 创建移动命令
        var moveCommand = new EntityMoveCommand(
            playerId: 0,
            entityIds: validEntityIds.ToArray(),
            targetPosition: new zVector2(x, z)
        )
        {
            Source = CommandSource.Local
        };

        _game.SubmitCommand(moveCommand);
        Debug.Log($"[Test] 发送移动命令: {validEntityIds.Count}个单位 → {worldPosition}");
    }

    #region 调试辅助

    // 在编辑器中显示点击位置
    private Vector3 _lastClickPosition;
    private float _gizmoDisplayTime = 2f;
    private float _lastClickTime;

    private void OnDrawGizmos()
    {
        // 显示最后点击位置
        if (Time.time - _lastClickTime < _gizmoDisplayTime)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_lastClickPosition, 0.5f);
            Gizmos.DrawLine(_lastClickPosition, _lastClickPosition + Vector3.up * 2f);
        }

        // 显示所有已创建的单位（从逻辑层读取）
        if (_game != null && _game.World != null)
        {
            var transforms = _game.World.ComponentManager
                .GetAllEntityIdsWith<TransformComponent>();

            foreach (var entityId in transforms)
            {
                var entity = new Entity(entityId);
                var transform = _game.World.ComponentManager
                    .GetComponent<TransformComponent>(entity);

                // 绘制单位位置
                Vector3 pos = transform.Position.ToVector3();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);

                // 如果这是选中的单位，用不同颜色标记
                if (selectedEntityIds.Contains(entityId))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(pos, 1.0f);
                }

                // 如果有移动命令，绘制目标位置
                if (_game.World.ComponentManager
                    .HasComponent<MoveCommandComponent>(entity))
                {
                    var moveCmd = _game.World.ComponentManager
                        .GetComponent<MoveCommandComponent>(entity);
                    
                    Vector3 targetPos = moveCmd.TargetPosition.ToVector3();
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(targetPos, 0.3f);
                    Gizmos.DrawLine(pos, targetPos);
                }
            }
        }
    }

    #endregion

    private void OnGUI()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fixedHeight = 60;
        buttonStyle.fixedWidth = 200;

        // 绘制框选区域
        DrawSelectionBox();

        // 绘制小地图
        DrawMiniMap();

        // 绘制左上角的房间类型选择和本地测试选项
        if (!isConnected) 
        {
            Rect roomTypeRect = new Rect(20, 20, 250, 800);
            GUILayout.BeginArea(roomTypeRect);
            // 添加本地测试选项
            bool previousValue = useLocalServer;
            useLocalServer = GUILayout.Toggle(useLocalServer, "使用本地服务器");
            // 如果值发生变化，保存设置
            if (useLocalServer != previousValue)
            {
                SaveLocalServerOption();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("房间类型:", buttonStyle);
            
            // 自定义下拉列表实现
            if (GUILayout.Button(roomTypeOptions[(int)selectedRoomType - 1], buttonStyle))
            {
                showRoomTypeDropdown = !showRoomTypeDropdown;
            }
            
            if (showRoomTypeDropdown)
            {
                for (int i = 0; i < roomTypeOptions.Length; i++)
                {
                    if (GUILayout.Button(roomTypeOptions[i], buttonStyle))
                    {
                        selectedRoomType = (RoomType)(i + 1);
                        showRoomTypeDropdown = false;
                        SaveRoomTypeSelection(); // 保存选择
                    }
                }
            }
            GUILayout.EndArea();
        }

        // 绘制中央的匹配/准备按钮
        if (!isConnected || (isConnected && !isMatched) || (isMatched && !isReady))
        {
            // 将匹配和准备按钮定位在屏幕中央
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float buttonWidth = 200;
            float buttonHeight = 100;
            
            Rect buttonRect = new Rect(
                (screenWidth - buttonWidth) / 2,
                (screenHeight - buttonHeight) / 2,
                buttonWidth,
                buttonHeight
            );

            GUILayout.BeginArea(buttonRect);
            
            if (!isConnected)
            {
                GUILayout.Space(10);
                if (GUILayout.Button("匹配", buttonStyle))
                {
                    ConnectToServer();
                }
            }
            else if (isConnected && !isMatched)
            {
                GUILayout.Label("匹配中...", buttonStyle);
            }
            else if (isMatched && !isReady)
            {
                GUILayout.Label("等待中", buttonStyle);
            }
            
            GUILayout.EndArea();
        }
        else if (isReady)
        {
            // 将暂停/继续按钮定位在左上角
            Rect pauseButtonRect = new Rect(20, 20, 300, 200);
            GUILayout.BeginArea(pauseButtonRect);
            
            if (!isPaused)
            {
                if (GUILayout.Button("暂停", buttonStyle))
                {
                    // worldBridge.PauseGame();
                    isPaused = true;
                }
            }
            else
            {
                if (GUILayout.Button("继续", buttonStyle))
                {
                    // worldBridge.ResumeGame();
                    isPaused = false;
                }
            }
            
            GUILayout.EndArea();
        }
        
        // 显示选中的单位信息
        if (isReady && selectedEntityIds.Count > 0)
        {
            Rect infoRect = new Rect(Screen.width - 250, 20, 230, 60);
            GUILayout.BeginArea(infoRect, GUI.skin.box);
            
            if (selectedEntityIds.Count == 1)
            {
                GUILayout.Label($"选中单位: {selectedEntityIds[0]}", new GUIStyle(GUI.skin.label) { fontSize = 20 });
            }
            else
            {
                GUILayout.Label($"选中单位: {selectedEntityIds.Count}个", new GUIStyle(GUI.skin.label) { fontSize = 20 });
            }
            
            GUILayout.EndArea();
        }

        // 绘制游戏结束界面
        if (_presentationSystem.IsGameOver)
        {
            DrawGameOverUI();
        }
        
        DrawGMConsole();
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
        // TODO: 实现重新开始游戏逻辑
        // 可能需要重新加载场景或重置游戏状态
        zUDebug.Log("[Ra2Demo] 重新开始游戏");
    }
    
    /// <summary>
    /// 绘制小地图
    /// </summary>
    private void DrawMiniMap()
    {
        if (_miniMapTexture != null)
        {
            // 计算小地图的实际位置（支持从底部对齐）
            Rect actualMiniMapRect = miniMapRect;
            if (miniMapRect.y == -1) // 特殊值表示从底部计算位置
            {
                actualMiniMapRect = new Rect(miniMapMargin, 
                                           Screen.height - miniMapRect.height - miniMapMargin, 
                                           miniMapRect.width, 
                                           miniMapRect.height);
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
            DrawCameraViewIndicator();
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
        if (!isReady || _mainCamera == null || miniMapController == null)
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
    private void DrawCameraViewIndicator()
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
        Rect actualMiniMapRect = miniMapRect;
        if (miniMapRect.y == -1) // 特殊值表示从底部计算位置
        {
            actualMiniMapRect = new Rect(miniMapMargin, 
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
        GUI.DrawTexture(new Rect(drawX - boxSize/2, drawY - boxSize/2, boxSize, 2), Texture2D.whiteTexture); // 上边框
        GUI.DrawTexture(new Rect(drawX - boxSize/2, drawY - boxSize/2, 2, boxSize), Texture2D.whiteTexture); // 左边框
        GUI.DrawTexture(new Rect(drawX + boxSize/2 - 2, drawY - boxSize/2, 2, boxSize), Texture2D.whiteTexture); // 右边框
        GUI.DrawTexture(new Rect(drawX - boxSize/2, drawY + boxSize/2 - 2, boxSize, 2), Texture2D.whiteTexture); // 下边框
        
        // 在中心绘制一个小点
        GUI.DrawTexture(new Rect(drawX - 3, drawY - 3, 6, 6), Texture2D.whiteTexture);
        
        GUI.color = oldColor;
    }
    
    /// <summary>
    /// 连接到服务器
    /// </summary>
    private void ConnectToServer()
    {
        // 根据选项决定使用哪个服务器地址
        string serverUrl = useLocalServer ? LocalServerUrl : RemoteServerUrl;
        _client = new WebSocketClient(serverUrl, "Player1");
        
        // 注册事件处理
        _client.OnConnected += OnConnected;
        _client.OnMatchSuccess += OnMatchSuccess;
        _client.OnGameStart += OnGameStart;

        // 连接网络适配器和客户端
        _networkAdaptor = new WebSocketNetworkAdaptor(_game, _client);
        
        // 连接服务器
        zUDebug.Log($"[WebSocketNetworkAdaptor] 正在连接服务器: {serverUrl}");
        _client.Connect();
        isConnected = true;
    }
    
    /// <summary>
    /// 连接成功事件处理
    /// </summary>
    private void OnConnected(string message)
    {
        zUDebug.Log("[Ra2Demo] 连接成功: " + message);
        // 使用选定的房间类型发送匹配请求
        _client.SendMatchRequest(selectedRoomType);
    }
    
    /// <summary>
    /// 匹配成功事件处理
    /// </summary>
    private void OnMatchSuccess(MatchSuccessData data)
    {
        isMatched = true;
        
        // data.Data为输入数据列表
        zUDebug.Log($"[Ra2Demo] 匹配成功：房间ID={data.RoomId}, 阵营ID={data.CampId}, data={data}");

        GlobalInfoComponent globalInfoComponent = new GlobalInfoComponent(data.CampId);
        _game.World.ComponentManager.AddGlobalComponent(globalInfoComponent);
        
        // 处理创世阶段 - 初始化游戏世界
        if (data.InitialState != null)
        {
            _game.InitializeWorldFromMatchData(data.InitialState);
        }

        // 发送准备就绪消息
        _client.SendReady();

        
    }
    
    /// <summary>
    /// 游戏开始事件处理
    /// </summary>
    private void OnGameStart()
    {
        isReady = true;
        
        // 在游戏正式启动时，发送一个初始帧确认（帧0）
        // 这样可以启动帧同步逻辑
        if (_game != null && _game.FrameSyncManager != null)
        {
            // 确认第0帧（空帧），启动帧同步逻辑
            _game.FrameSyncManager.ConfirmFrame(0, new System.Collections.Generic.List<ICommand>());
        }

        zUDebug.Log("[Ra2Demo] 游戏开始，帧同步已启动");
        
        // 获取我方战车工厂位置，相机移动到此位置
        MoveCameraToOurFactory();
        
        // 调整相机位置
        Vector3 adjustedPosition = new(RTSCameraTargetController.Instance.CameraTarget.position.x, -50, RTSCameraTargetController.Instance.CameraTarget.position.z);
        RTSCameraTargetController.Instance.CameraTarget.position = adjustedPosition;

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
        foreach (var log in _logHistory)
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
            _logHistory.Add($"[GM] {_inputFieldGM}");
            Event.current.Use(); // 防止重复处理
        }
        GUILayout.FlexibleSpace();
        // 执行按钮
        if (GUILayout.Button("Execute", GUILayout.Width(60)))
        {
            _game.SubmitCommand(new GMCommand(_inputFieldGM));
            _logHistory.Add($"[GM] {_inputFieldGM}");
        }
        GUILayout.EndHorizontal();
        GUI.FocusControl("InputField");
    }

    /// <summary>
    /// 移动相机到我方工厂位置
    /// </summary>
    private void MoveCameraToOurFactory()
    {
        if (_game == null || _game.World == null)
            return;

        // 获取所有建筑实体
        var buildingEntities = _game.World.ComponentManager
            .GetAllEntityIdsWith<BuildingComponent>();

        foreach (var entityId in buildingEntities)
        {
            var entity = new Entity(entityId);
            
            // 检查实体是否同时拥有阵营组件和建筑组件
            if (_game.World.ComponentManager.HasComponent<CampComponent>(entity) &&
                _game.World.ComponentManager.HasComponent<BuildingComponent>(entity))
            {
                var building = _game.World.ComponentManager.GetComponent<BuildingComponent>(entity);
                
                // 查找我方阵营ID为1的建筑（工厂）
                // 根据代码分析，建筑类型1代表工厂（tankFactory）
                if (building.BuildingType == 1 && _game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
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
            }
        }
        
        zUDebug.Log("[Ra2Demo] 未找到我方工厂");
    }
}