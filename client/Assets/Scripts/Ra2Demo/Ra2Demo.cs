using UnityEngine;
using UnityEngine.InputSystem;
using ZLockstep.View;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Sync.Command;
using ZLockstep.Sync.Command.Commands;
using zUnity;
using Game.RA2.Client;

/// <summary>
/// 测试脚本：点击地面创建单位（使用Command系统）
/// </summary>
public class Ra2Demo : MonoBehaviour
{
    [Header("游戏世界")]
    [SerializeField] private GameWorldBridge worldBridge;

    [Header("创建设置")]
    [SerializeField] private LayerMask groundLayer = -1; // 地面层
    [SerializeField] private int unitType = 1; // 单位类型：1=动员兵, 2=坦克, 3=矿车
    [SerializeField] private int prefabId = 1; // 预制体ID（对应unitPrefabs数组索引）

    private RTSControl _controls;
    private Camera _mainCamera;

    [SerializeField] public string ServerUrl = "ws://101.126.136.178:8080/ws";

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
    
    private void Awake()
    {
        _mainCamera = Camera.main;
        _controls = new RTSControl();

        // 如果没有分配 worldBridge，尝试自动查找
        if (worldBridge == null)
        {
            worldBridge = FindObjectOfType<GameWorldBridge>();
            if (worldBridge == null)
            {
                Debug.LogError("[Test] 找不到 GameWorldBridge！请在场景中添加或手动分配。");
            }
        }
        
        // 加载保存的房间类型选择
        LoadRoomTypeSelection();
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

    void Start()
    {
        // 不再在这里初始化WebSocketNetworkAdaptor
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
        if (worldBridge == null || worldBridge.LogicWorld == null)
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
        worldBridge.SubmitCommand(createCommand);

        Debug.Log($"[Test] 提交创建单位命令: 类型={unitType}, 位置={position}");
    }

    /// <summary>
    /// 测试：右键点击地面让所有单位移动（可选）
    /// </summary>
    private void Update()
    {
        // 右键点击发送移动命令
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            SendMoveCommand();
        }

        _networkAdaptor?.OnDispatchMessageQueue();
    }

    /// <summary>
    /// 发送移动命令给所有单位
    /// </summary>
    private void SendMoveCommand()
    {
        if (worldBridge == null || worldBridge.LogicWorld == null)
            return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (!TryGetGroundPosition(screenPosition, out Vector3 worldPosition))
            return;

        // 获取所有属于该玩家的单位
        var allUnits = worldBridge.LogicWorld.ComponentManager
            .GetAllEntityIdsWith<UnitComponent>();

        var playerUnits = new System.Collections.Generic.List<int>();
        foreach (var entityId in allUnits)
        {
            var entity = new Entity(entityId);
            var unit = worldBridge.LogicWorld.ComponentManager.GetComponent<UnitComponent>(entity);
            if (unit.PlayerId == worldBridge.Game.GetLocalPlayerId())
            {
                playerUnits.Add(entityId);
            }
        }

        if (playerUnits.Count > 0)
        {
            // 创建移动命令
            var moveCommand = new MoveCommand(
                playerId: 0,
                entityIds: playerUnits.ToArray(),
                targetPosition: worldPosition.ToZVector3()
            )
            {
                Source = CommandSource.Local
            };

            worldBridge.SubmitCommand(moveCommand);
            Debug.Log($"[Test] 发送移动命令: {playerUnits.Count}个单位 → {worldPosition}");
        }
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
        if (worldBridge != null && worldBridge.LogicWorld != null)
        {
            var transforms = worldBridge.LogicWorld.ComponentManager
                .GetAllEntityIdsWith<TransformComponent>();

            foreach (var entityId in transforms)
            {
                var entity = new Entity(entityId);
                var transform = worldBridge.LogicWorld.ComponentManager
                    .GetComponent<TransformComponent>(entity);

                // 绘制单位位置
                Vector3 pos = transform.Position.ToVector3();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);

                // 如果有移动命令，绘制目标位置
                if (worldBridge.LogicWorld.ComponentManager
                    .HasComponent<MoveCommandComponent>(entity))
                {
                    var moveCmd = worldBridge.LogicWorld.ComponentManager
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

        // 绘制左上角的房间类型选择
        if (!isConnected) 
        {
            Rect roomTypeRect = new Rect(20, 20, 250, 800);
            GUILayout.BeginArea(roomTypeRect);
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
                    worldBridge.PauseGame();
                    isPaused = true;
                }
            }
            else
            {
                if (GUILayout.Button("继续", buttonStyle))
                {
                    worldBridge.ResumeGame();
                    isPaused = false;
                }
            }
            
            GUILayout.EndArea();
        }
    }
    
    /// <summary>
    /// 连接到服务器
    /// </summary>
    private void ConnectToServer()
    {
        _client = new WebSocketClient(ServerUrl, "Player1");
        
        // 注册事件处理
        _client.OnConnected += OnConnected;
        _client.OnMatchSuccess += OnMatchSuccess;
        _client.OnGameStart += OnGameStart;

        // 连接网络适配器和客户端
        _networkAdaptor = new WebSocketNetworkAdaptor(worldBridge, _client);
        
        // 连接服务器
        zUDebug.Log("[WebSocketNetworkAdaptor] 正在连接服务器...");
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
        
        // 更新 Game 中的玩家ID
        worldBridge.Game.SetLocalPlayerId(data.CampId);
        
        // 处理创世阶段 - 初始化游戏世界
        if (data.InitialState != null)
        {
            worldBridge.Game.InitializeWorldFromMatchData(data.InitialState);
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
        if (worldBridge != null && worldBridge.Game != null &&
            worldBridge.Game.FrameSyncManager != null)
        {
            // 确认第0帧（空帧），启动帧同步逻辑
            worldBridge.Game.FrameSyncManager.ConfirmFrame(0, new System.Collections.Generic.List<ICommand>());
        }
        
        zUDebug.Log("[Ra2Demo] 游戏开始，帧同步已启动");
    }
}