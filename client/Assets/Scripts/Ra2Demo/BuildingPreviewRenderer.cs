using Game.Examples;
using UnityEngine;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
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
    // 添加矿源位置列表，用于显示采矿场可建造区域
    private System.Collections.Generic.List<Vector2> minePositions = new System.Collections.Generic.List<Vector2>();

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
            
            // 如果是采矿场，使用蓝色绘制矿源周围可建造区域；否则使用绿色绘制主城周围可建造区域
            if (buildingToBuild == BuildingType.Smelter)
            {
                // 绘制每个矿源周围的可建造区域
                float gridSize = (float)_ra2Demo.GetBattleGame().MapManager.GetGridSize();
                int mineBuildRange = 10; // 采矿场可在矿源周围10格内建造
                
                foreach (Vector2 minePos in minePositions)
                {
                    // 计算矿源周围的可建造区域边界
                    int mineGridX, mineGridY;
                    _ra2Demo.GetBattleGame().MapManager.WorldToGrid(
                        new zVector2(
                            zfloat.CreateFloat((long)(minePos.x * zfloat.SCALE_10000)),
                            zfloat.CreateFloat((long)(minePos.y * zfloat.SCALE_10000))
                        ),
                        out mineGridX, out mineGridY
                    );
                    
                    int minX = Mathf.Max(0, mineGridX - mineBuildRange);
                    int minY = Mathf.Max(0, mineGridY - mineBuildRange);
                    int maxX = Mathf.Min(_ra2Demo.GetBattleGame().MapManager.GetWidth() - 1, mineGridX + mineBuildRange);
                    int maxY = Mathf.Min(_ra2Demo.GetBattleGame().MapManager.GetHeight() - 1, mineGridY + mineBuildRange);
                    
                    // 绘制区域内的每个格子
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            // 检查格子是否可行走，不可行走则显示红色
                            if (_ra2Demo.GetBattleGame().MapManager.IsWalkable(x, y))
                            {
                                GL.Color(Color.yellow);
                            }
                            else
                            {
                                GL.Color(Color.red);
                            }
                            
                            // 绘制格子边界
                            Vector3 bottomLeft = new Vector3(x * gridSize, 0.1f, y * gridSize);
                            Vector3 topLeft = new Vector3(x * gridSize, 0.1f, (y + 1) * gridSize);
                            Vector3 topRight = new Vector3((x + 1) * gridSize, 0.1f, (y + 1) * gridSize);
                            Vector3 bottomRight = new Vector3((x + 1) * gridSize, 0.1f, y * gridSize);
                            
                            // 绘制四条边
                            GL.Vertex(bottomLeft);
                            GL.Vertex(topLeft);
                            
                            GL.Vertex(topLeft);
                            GL.Vertex(topRight);
                            
                            GL.Vertex(topRight);
                            GL.Vertex(bottomRight);
                            
                            GL.Vertex(bottomRight);
                            GL.Vertex(bottomLeft);
                            
                            // 绘制对角线
                            GL.Vertex(bottomLeft);
                            GL.Vertex(topRight);
                            
                            GL.Vertex(topLeft);
                            GL.Vertex(bottomRight);
                        }
                    }
                }
            }
            else
            {
                // 绘制可建造区域内的每个格子
                float gridSize = (float)_ra2Demo.GetBattleGame().MapManager.GetGridSize();
                for (int x = buildableMinGridX; x <= buildableMaxGridX; x++)
                {
                    for (int y = buildableMinGridY; y <= buildableMaxGridY; y++)
                    {
                        // 检查格子是否可行走，不可行走则显示红色
                        if (_ra2Demo.GetBattleGame().MapManager.IsWalkable(x, y))
                        {
                            GL.Color(Color.green);
                        }
                        else
                        {
                            GL.Color(Color.red);
                        }
                        
                        // 绘制格子边界
                        Vector3 bottomLeft = new Vector3(x * gridSize, 0.1f, y * gridSize);
                        Vector3 topLeft = new Vector3(x * gridSize, 0.1f, (y + 1) * gridSize);
                        Vector3 topRight = new Vector3((x + 1) * gridSize, 0.1f, (y + 1) * gridSize);
                        Vector3 bottomRight = new Vector3((x + 1) * gridSize, 0.1f, y * gridSize);
                        
                        // 绘制四条边
                        GL.Vertex(bottomLeft);
                        GL.Vertex(topLeft);
                        
                        GL.Vertex(topLeft);
                        GL.Vertex(topRight);
                        
                        GL.Vertex(topRight);
                        GL.Vertex(bottomRight);
                        
                        GL.Vertex(bottomRight);
                        GL.Vertex(bottomLeft);
                        
                        // 绘制对角线
                        GL.Vertex(bottomLeft);
                        GL.Vertex(topRight);
                        
                        GL.Vertex(topLeft);
                        GL.Vertex(bottomRight);
                    }
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

        // 显示可建造区域
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
                    0.1f, // 保持原来的Y轴位置
                    (float)alignedWorldPos.y
                );

                // 检查建筑是否可以放置在此位置
                zVector3 logicPosition = new zVector3(
                    zfloat.CreateFloat((long)(alignedWorldPos.x * zfloat.SCALE_10000)),
                    zfloat.Zero,
                    zfloat.CreateFloat((long)(alignedWorldPos.y * zfloat.SCALE_10000))
                );

                // 检查建筑是否可以放置在此位置
                bool canPlace = BuildingPlacementUtils.CheckBuildingPlacement(
                    buildingToBuild, logicPosition, _ra2Demo.GetBattleGame().MapManager);

                // 如果是非采矿场，需要判断在主城的限制区域内
                if (canPlace && buildingToBuild != BuildingType.Smelter)
                {
                    // 获取本地玩家阵营ID
                    int localPlayerCampId = _ra2Demo.GetBattleGame().World.ComponentManager.GetGlobalComponent<GlobalInfoComponent>().LocalPlayerCampId;

                    canPlace = BuildingPlacementUtils.CheckBuildableArea(_ra2Demo.GetBattleGame().World, logicPosition, buildingToBuild, localPlayerCampId);
                }
                
                // 如果是采矿场，需要判断是否在矿源附近
                if (canPlace && buildingToBuild == BuildingType.Smelter)
                {
                    canPlace = CheckMineProximity(logicPosition);
                }

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
    /// 检查采矿场是否在矿源附近（10格范围内）
    /// </summary>
    /// <param name="position">采矿场位置</param>
    /// <returns>是否在矿源附近</returns>
    private bool CheckMineProximity(zVector3 position)
    {
        foreach (Vector2 minePos in minePositions)
        {
            // 计算与矿源的距离
            Vector3 mineWorldPos = new Vector3(minePos.x, 0, minePos.y);
            Vector3 smelterPos = new Vector3((float)position.x, 0, (float)position.z);
            
            float distance = Vector3.Distance(mineWorldPos, smelterPos);
            
            // 检查是否在10格范围内
            if (distance <= 10.0f)
            {
                return true;
            }
        }
        
        return false;
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
        // 清空之前的矿源位置
        minePositions.Clear();
        
        // 如果是采矿场，收集所有矿源位置
        if (buildingToBuild == BuildingType.Smelter)
        {
            BattleGame game = _ra2Demo.GetBattleGame();
            if (game != null && game.World != null)
            {
                var entities = game.World.ComponentManager.GetAllEntityIdsWith<MineComponent>();
                foreach (var entityId in entities)
                {
                    var entity = new Entity(entityId);
                    if (game.World.ComponentManager.HasComponent<TransformComponent>(entity))
                    {
                        var transform = game.World.ComponentManager.GetComponent<TransformComponent>(entity);
                        Vector2 minePos = new Vector2((float)transform.Position.x, (float)transform.Position.z);
                        minePositions.Add(minePos);
                    }
                }
            }
            
            // 启用可建造区域显示
            showBuildableArea = true;
        }
        else
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

                    // 查找本地玩家的基地
                    if (building.BuildingType == (int)BuildingType.Base && game.World.ComponentManager.HasComponent<ZLockstep.Simulation.ECS.Components.LocalPlayerComponent>(entity))
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
        minePositions.Clear(); // 清空矿源位置
    }

    /// <summary>
    /// 获取当前正在预览的建筑类型
    /// </summary>
    /// <returns>当前正在预览的建筑类型</returns>
    public BuildingType GetCurrentBuildingToBuild()
    {
        return buildingToBuild;
    }
}