using System.Collections.Generic;
using Game.Examples;
using UnityEngine;
using UnityEngine.InputSystem;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.View;
using zUnity;

public class UnitSelectionManager : MonoBehaviour
{
    [Header("选择设置")]
    [SerializeField] private LayerMask groundLayer = -1;
    private const float dragThreshold = 5f;
    
    private Camera _mainCamera;
    private RTSControl _controls;
    
    // 选中单位相关字段
    private int selectedEntityId = -1;
    private List<int> selectedEntityIds = new List<int>();
    private bool isSelecting = false;
    private bool isClickOnUnit = false;
    private Vector2 selectionStartPoint;
    private Vector2 selectionEndPoint;
    
    private BattleGame _game;
    private System.Action<List<int>> onSelectionChanged;
    
    private void Awake()
    {
        _mainCamera = Camera.main;
        _controls = new RTSControl();
    }
    
    public void Initialize(BattleGame game, System.Action<List<int>> onSelectionChangedCallback)
    {
        _game = game;
        onSelectionChanged = onSelectionChangedCallback;
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
            Debug.LogWarning("[UnitSelectionManager] GameWorldBridge 未初始化！");
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
            Debug.LogWarning("[UnitSelectionManager] 找不到主相机！");
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

        Debug.LogWarning("[UnitSelectionManager] 无法获取点击位置！请确保有地面碰撞体或使用默认平面。");
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
        selectedEntityIds.Clear();

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
                    Debug.Log($"[UnitSelectionManager] 框选添加单位: EntityId={entityId}");
                }
            }

            // 兼容旧的单选变量
            selectedEntityId = selectedEntityIds.Count > 0 ? selectedEntityIds[0] : -1;
        }

        // 为所有新选中的单位启用OutlineComponent
        foreach (int entityId in selectedEntityIds)
        {
            EnableOutlineForEntity(entityId);
        }
        
        // 通知选择变更
        onSelectionChanged?.Invoke(selectedEntityIds);
    }
    
    public void Update()
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
    }
    
    /// <summary>
    /// 发送移动命令给选中的单位
    /// </summary>
    public void SendMoveCommandForSelectedUnit()
    {
        // 检查是否有选中的单位
        if (selectedEntityIds.Count == 0)
        {
            Debug.Log("[UnitSelectionManager] 没有选中的单位");
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
                Debug.Log($"[UnitSelectionManager] 选中的单位 {entityId} 已不存在");
                // 禁用该单位的OutlineComponent
                DisableOutlineForEntity(entityId);
                continue;
            }

            // 判断拥有LocalPlayerComponent就是当前玩家的单位
            if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
            {
                Debug.Log($"[UnitSelectionManager] 选中的单位 {entityId} 不属于当前玩家");
                // 禁用该单位的OutlineComponent
                DisableOutlineForEntity(entityId);
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
            Debug.Log("[UnitSelectionManager] 没有有效的选中单位");
            return;
        }

        zfloat x = zfloat.CreateFloat((long)(worldPosition.x * zfloat.SCALE_10000));
        zfloat z = zfloat.CreateFloat((long)(worldPosition.z * zfloat.SCALE_10000));

        // 创建移动命令
        /*var moveCommand = new EntityMoveCommand(
            playerId: 0,
            entityIds: validEntityIds.ToArray(),
            targetPosition: new zVector2(x, z)
        )
        {
            Source = CommandSource.Local
        };

        _game.SubmitCommand(moveCommand);*/
        // Debug.Log($"[UnitSelectionManager] 发送移动命令: {validEntityIds.Count}个单位 → {worldPosition}");
        
        // 通知选择变更
        onSelectionChanged?.Invoke(selectedEntityIds);
    }
    
    /// <summary>
    /// 为指定实体启用OutlineComponent
    /// </summary>
    /// <param name="entityId">实体ID</param>
    private void EnableOutlineForEntity(int entityId)
    {
        if (_game == null || _game.World == null)
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
        if (_game == null || _game.World == null)
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
    public void ClearAllOutlines()
    {
        if (_game == null || _game.World == null)
            return;

        // 禁用之前选中单位的OutlineComponent
        foreach (int entityId in selectedEntityIds)
        {
            DisableOutlineForEntity(entityId);
        }
        selectedEntityIds.Clear();
    }
    
    public List<int> GetSelectedEntityIds()
    {
        return new List<int>(selectedEntityIds);
    }
}