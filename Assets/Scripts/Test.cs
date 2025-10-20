using UnityEngine;
using UnityEngine.InputSystem;
using ZLockstep.View;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using zUnity;

/// <summary>
/// 测试脚本：点击地面创建单位
/// </summary>
public class Test : MonoBehaviour
{
    [Header("游戏世界")]
    [SerializeField] private GameWorldBridge worldBridge;

    [Header("单位预制体")]
    [SerializeField] private GameObject unitCubePrefab;

    [Header("创建设置")]
    [SerializeField] private LayerMask groundLayer = -1; // 地面层
    [SerializeField] private int playerId = 0; // 玩家ID

    private RTSControl _controls;
    private Camera _mainCamera;

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
    /// 响应创建单位的输入
    /// </summary>
    private void OnCreateUnit(InputAction.CallbackContext context)
    {
        if (worldBridge == null || worldBridge.EntityFactory == null)
        {
            Debug.LogWarning("[Test] GameWorldBridge 未初始化！");
            return;
        }

        if (unitCubePrefab == null)
        {
            Debug.LogWarning("[Test] 单位预制体未分配！");
            return;
        }

        // 获取鼠标/触摸位置
        Vector2 screenPosition = Pointer.current.position.ReadValue();

        // 射线检测获取点击位置
        if (TryGetGroundPosition(screenPosition, out Vector3 worldPosition))
        {
            CreateUnitAtPosition(worldPosition);
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
    /// 在指定位置创建单位
    /// </summary>
    private void CreateUnitAtPosition(Vector3 position)
    {
        // 转换为逻辑层坐标
        zVector3 logicPosition = position.ToZVector3();

        // 使用 EntityFactory 创建单位
        Entity entity = worldBridge.EntityFactory.CreateUnit(
            unitType: 1, // 1=动员兵类型
            position: logicPosition,
            playerId: playerId,
            prefab: unitCubePrefab,
            enableInterpolation: true
        );

        Debug.Log($"[Test] 在位置 {position} 创建了单位 Entity_{entity.Id}");

        // 可选：给新创建的单位一个测试移动命令
        // 让它移动到附近的位置
        // StartCoroutine(TestMoveUnit(entity, logicPosition));
    }

    /// <summary>
    /// 测试：让单位自动移动（可选）
    /// </summary>
    private System.Collections.IEnumerator TestMoveUnit(Entity entity, zVector3 startPos)
    {
        yield return new WaitForSeconds(1f);

        // 生成一个随机目标位置
        zVector3 targetPos = new zVector3(
            startPos.x + (zfloat)Random.Range(-5f, 5f),
            startPos.y,
            startPos.z + (zfloat)Random.Range(-5f, 5f)
        );

        // 添加移动命令
        var moveCmd = new MoveCommandComponent(targetPos);
        worldBridge.LogicWorld.ComponentManager.AddComponent(entity, moveCmd);

        Debug.Log($"[Test] 单位 Entity_{entity.Id} 开始移动到 {targetPos}");
    }

    #region 调试辅助

    // 在编辑器中显示点击位置
    private Vector3 _lastClickPosition;
    private float _gizmoDisplayTime = 2f;
    private float _lastClickTime;

    private void Update()
    {
        // 调试：显示点击位置
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPosition = Mouse.current.position.ReadValue();
            if (TryGetGroundPosition(screenPosition, out Vector3 worldPosition))
            {
                _lastClickPosition = worldPosition;
                _lastClickTime = Time.time;
            }
        }
    }

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
}

