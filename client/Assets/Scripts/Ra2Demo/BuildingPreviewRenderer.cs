using Game.Examples;
using UnityEngine;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Utils;
using ZLockstep.View;
using zUnity;

/// <summary>
/// 建筑预览渲染器，专门处理建筑预览的场景绘制逻辑
/// </summary>
public class BuildingPreviewRenderer : MonoBehaviour
{
    private Ra2Demo _ra2Demo;
    
    // 添加建造功能相关字段
    private BuildingType buildingToBuild = BuildingType.None;
    private GameObject previewBuilding;

    // 添加可建造区域相关字段
    private bool showBuildableArea = false;
    private int buildableMinGridX, buildableMinGridY, buildableMaxGridX, buildableMaxGridY;
    private Material lineMaterial;

    private bool isReady = false;

    public void Initialize(Ra2Demo ra2Demo)
    {
        _ra2Demo = ra2Demo;
        
        // 创建用于绘制线条的材质
        CreateLineMaterial();
    }

    /// <summary>
    /// 创建用于绘制线条的材质
    /// </summary>
    private void CreateLineMaterial()
    {
        // 创建一个简单的材质用于绘制线条
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;

        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;

        // 关闭背面裁剪，开启混合
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    /// <summary>
    /// 在OnRenderObject中绘制可建造区域
    /// </summary>
    private void OnRenderObject()
    {
        // 只在游戏运行时绘制
        if (!Application.isPlaying) return;

        if (showBuildableArea && lineMaterial != null && buildingToBuild != BuildingType.None && _ra2Demo.GetBattleGame()?.MapManager != null)
        {
            // 设置材质
            lineMaterial.SetPass(0);

            // 开始绘制线条
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(Color.green);

            // 获取网格大小
            float gridSize = (float)_ra2Demo.GetBattleGame().MapManager.GetGridSize();

            // 绘制可建造区域边界（只绘制边缘，而不是整个区域）
            // 绘制水平线
            for (int y = buildableMinGridY; y <= buildableMaxGridY; y++)
            {
                Vector3 left = new Vector3(buildableMinGridX * gridSize, 0.1f, y * gridSize);
                Vector3 right = new Vector3(buildableMaxGridX * gridSize, 0.1f, y * gridSize);
                GL.Vertex(left);
                GL.Vertex(right);
            }

            // 绘制垂直线
            for (int x = buildableMinGridX; x <= buildableMaxGridX; x++)
            {
                Vector3 bottom = new Vector3(x * gridSize, 0.1f, buildableMinGridY * gridSize);
                Vector3 top = new Vector3(x * gridSize, 0.1f, buildableMaxGridY * gridSize);
                GL.Vertex(bottom);
                GL.Vertex(top);
            }

            // 绘制网格内的斜线
            for (int x = buildableMinGridX; x < buildableMaxGridX; x++)
            {
                for (int y = buildableMinGridY; y < buildableMaxGridY; y++)
                {
                    // 绘制每个网格的对角线（斜线效果）
                    Vector3 bottomLeft = new Vector3(x * gridSize, 0.1f, y * gridSize);
                    Vector3 topRight = new Vector3((x + 1) * gridSize, 0.1f, (y + 1) * gridSize);
                    Vector3 bottomRight = new Vector3((x + 1) * gridSize, 0.1f, y * gridSize);
                    Vector3 topLeft = new Vector3(x * gridSize, 0.1f, (y + 1) * gridSize);

                    // 绘制交叉线形成网格
                    GL.Vertex(bottomLeft);
                    GL.Vertex(topRight);

                    GL.Vertex(topLeft);
                    GL.Vertex(bottomRight);
                }
            }

            GL.End();
            GL.PopMatrix();
        }
    }

    /// <summary>
    /// 更新建筑预览位置
    /// </summary>
    public void UpdateBuildingPreview(BuildingType currentBuildingToBuild, bool currentIsReady)
    {
        buildingToBuild = currentBuildingToBuild;
        isReady = currentIsReady;
        
        // 如果没有要建造的建筑或游戏未准备好，则不显示预览
        if (buildingToBuild == BuildingType.None || !isReady || _ra2Demo.GetBattleGame() == null || _ra2Demo.GetBattleGame().MapManager == null)
            return;

        // 显示可建造区域，主建筑x,y+-16范围
        ShowBuildableArea();

        // 如果还没有创建预览对象，则创建它
        if (previewBuilding == null)
        {
            int prefabId = BuildingTypeToPrefabId(buildingToBuild);

            // 使用对应的预制体创建预览建筑
            GameObject prefab = _ra2Demo.unitPrefabs[prefabId];
            if (prefab != null)
            {
                previewBuilding = Instantiate(prefab);

                // 设置为半透明
                Renderer[] renderers = previewBuilding.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        // 创建新材质以避免修改原始材质
                        Material transparentMaterial = new Material(materials[i]);
                        transparentMaterial.color = new Color(
                            transparentMaterial.color.r,
                            transparentMaterial.color.g,
                            transparentMaterial.color.b,
                            0.5f // 50% 透明度
                        );

                        // 确保材质支持透明渲染
                        if (transparentMaterial.HasProperty("_Mode"))
                        {
                            transparentMaterial.SetFloat("_Mode", 3); // Transparent mode
                            transparentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            transparentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            transparentMaterial.SetInt("_ZWrite", 0);
                            transparentMaterial.DisableKeyword("_ALPHATEST_ON");
                            transparentMaterial.DisableKeyword("_ALPHABLEND_ON");
                            transparentMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                            transparentMaterial.renderQueue = 3000;
                        }

                        materials[i] = transparentMaterial;
                    }
                    renderer.materials = materials;
                }
            }
        }

        // 更新预览建筑的位置
        if (previewBuilding != null && UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            if (TryGetGroundPosition(mousePosition, out Vector3 worldPosition))
            {
                // 对齐到网格中心
                zVector2 zWorldPos = new zVector2(
                    zfloat.CreateFloat((long)(worldPosition.x * zfloat.SCALE_10000)),
                    zfloat.CreateFloat((long)(worldPosition.z * zfloat.SCALE_10000))
                );

                // 使用WorldToGrid将世界坐标转换为网格坐标
                _ra2Demo.GetBattleGame().MapManager.WorldToGrid(zWorldPos, out int gridX, out int gridY);

                // 使用GridToWorld将网格坐标转换回世界坐标（确保对齐到网格中心）
                zVector2 alignedWorldPos = _ra2Demo.GetBattleGame().MapManager.GridToWorld(gridX, gridY);

                // 设置预览建筑的位置
                previewBuilding.transform.position = new Vector3(
                    (float)alignedWorldPos.x,
                    0, // 保持原来的Y轴位置
                    (float)alignedWorldPos.y
                );

                // 检查建筑是否可以放置在此位置
                zVector3 logicPosition = new zVector3(
                    zfloat.CreateFloat((long)(alignedWorldPos.x * zfloat.SCALE_10000)),
                    zfloat.Zero,
                    zfloat.CreateFloat((long)(alignedWorldPos.y * zfloat.SCALE_10000))
                );

                bool canPlace = BuildingPlacementUtils.CheckBuildingPlacement(
                    buildingToBuild, logicPosition, _ra2Demo.GetBattleGame().MapManager);

                // 根据是否可以放置来改变预览建筑的颜色
                Renderer[] renderers = previewBuilding.GetComponentsInChildren<Renderer>();
                Color color = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f); // 绿色或红色

                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i].color = color;
                    }
                    renderer.materials = materials;
                }
            }
        }
    }

    /// <summary>
    /// 通过射线检测获取地面点击位置
    /// </summary>
    private bool TryGetGroundPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (_ra2Demo._mainCamera == null)
        {
            Debug.LogWarning("[BuildingPreviewRenderer] 找不到主相机！");
            return false;
        }

        Ray ray = _ra2Demo._mainCamera.ScreenPointToRay(screenPosition);

        // 尝试射线检测地面
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, -1))
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

        Debug.LogWarning("[BuildingPreviewRenderer] 无法获取点击位置！请确保有地面碰撞体或使用默认平面。");
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
    /// 显示可建造区域（主建筑x,y+-16范围）
    /// </summary>
    private void ShowBuildableArea()
    {
        // 查找主基地位置
        zVector2 mainBasePos = zVector2.zero;
        bool foundMainBase = false;

        BattleGame game = _ra2Demo.GetBattleGame();
        if (game != null && game.World != null)
        {
            var entities = game.World.ComponentManager.GetAllEntityIdsWith<ZLockstep.Simulation.ECS.Components.BuildingComponent>();
            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                var building = game.World.ComponentManager.GetComponent<ZLockstep.Simulation.ECS.Components.BuildingComponent>(entity);

                // 查找本地玩家的基地（BuildingType=1）
                if (building.BuildingType == 1 && game.World.ComponentManager.HasComponent<ZLockstep.Simulation.ECS.Components.LocalPlayerComponent>(entity))
                {
                    var transform = game.World.ComponentManager.GetComponent<ZLockstep.Simulation.ECS.Components.TransformComponent>(entity);
                    mainBasePos = new zVector2(transform.Position.x, transform.Position.z);
                    foundMainBase = true;
                    break;
                }
            }
        }

        // 如果找到了主基地，计算可建造区域
        if (foundMainBase)
        {
            // 计算可建造区域的边界（主建筑x,y+-20范围）
            int range = 20;
            zVector2 minPos = new zVector2(mainBasePos.x - range, mainBasePos.y - range);
            zVector2 maxPos = new zVector2(mainBasePos.x + range, mainBasePos.y + range);

            // 将世界坐标转换为网格坐标
            game.MapManager.WorldToGrid(minPos, out int minGridX, out int minGridY);
            game.MapManager.WorldToGrid(maxPos, out int maxGridX, out int maxGridY);

            // 确保网格坐标在地图范围内
            buildableMinGridX = Mathf.Max(0, minGridX);
            buildableMinGridY = Mathf.Max(0, minGridY);
            buildableMaxGridX = Mathf.Min(game.MapManager.GetWidth() - 1, maxGridX);
            buildableMaxGridY = Mathf.Min(game.MapManager.GetHeight() - 1, maxGridY);

            // 启用可建造区域显示
            showBuildableArea = true;
        }
        else
        {
            // 没有找到主基地，禁用可建造区域显示
            showBuildableArea = false;
        }
    }

    /// <summary>
    /// 放置建筑
    /// </summary>
    public void PlaceBuilding(out bool wasPlaced)
    {
        wasPlaced = false;
        
        if (buildingToBuild == BuildingType.None || previewBuilding == null || _ra2Demo.GetBattleGame() == null)
            return;

        Vector3 placementPosition = previewBuilding.transform.position;

        // 转换为逻辑层坐标
        zVector3 logicPosition = placementPosition.ToZVector3();

        int prefabId = BuildingTypeToPrefabId(buildingToBuild);

        // 创建建筑命令（使用CreateBuildingCommand）
        var createBuildingCommand = new ZLockstep.Sync.Command.Commands.CreateBuildingCommand(
            campId: 0,
            buildingType: buildingToBuild,
            position: logicPosition,
            prefabId: prefabId
        )
        {
            Source = ZLockstep.Sync.Command.CommandSource.Local,
        };

        // 提交命令到游戏世界
        _ra2Demo.GetBattleGame().SubmitCommand(createBuildingCommand);

        Debug.Log($"[BuildingPreviewRenderer] 提交创建建筑命令: 类型={buildingToBuild}, 位置={placementPosition}");

        // 清理预览对象
        Destroy(previewBuilding);
        previewBuilding = null;
        buildingToBuild = BuildingType.None;
        showBuildableArea = false; // 隐藏可建造区域
        
        wasPlaced = true;
    }

    /// <summary>
    /// 取消建筑建造
    /// </summary>
    public void CancelBuilding()
    {
        if (previewBuilding != null)
        {
            Destroy(previewBuilding);
            previewBuilding = null;
        }
        buildingToBuild = BuildingType.None;
        showBuildableArea = false; // 隐藏可建造区域
    }

    /// <summary>
    /// 获取当前正在预览的建筑类型
    /// </summary>
    public BuildingType GetCurrentBuildingToBuild()
    {
        return buildingToBuild;
    }
}