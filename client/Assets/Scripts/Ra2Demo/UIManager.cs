using System.Collections.Generic;
using Game.Examples;
using Game.RA2.Client;
using UnityEngine;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using zUnity;

public class UIManager : MonoBehaviour
{
    [Header("UI设置")]
    private Rect miniMapRect = new Rect(-1, 0, 200, 200);
    private int miniMapMargin = 10;
    
    private bool showRoomTypeDropdown = false;
    private string[] roomTypeOptions = { "单人(SOLO)", "双人(DUO)", "三人(TRIO)", "四人(QUAD)", "八人(OCTO)" };
    
    // GM相关
    private bool _isConsoleVisible = false;
    private string _inputFieldGM = "";
    private Vector2 _scrollPosition;
    
    // 可视化设置
    public bool showUnits = true;
    public bool showRVOAgents = true;
    public bool showGrid = true;
    public bool showObstacles = true;
    public bool showFlowField = true;
    
    private BattleGame _game;
    private NetworkManager _networkManager;
    private MiniMapController _miniMapController;
    private RenderTexture _miniMapTexture;
    private Camera _mainCamera;
    
    // 工厂生产相关字段
    private int selectedFactoryEntityId = -1;
    private bool showFactoryUI = false;
    private bool showProductionList = false;
    
    public System.Action onRestartGame;
    
    private void Awake()
    {
        _mainCamera = Camera.main;
    }
    
    public void Initialize(BattleGame game, NetworkManager networkManager)
    {
        _game = game;
        _networkManager = networkManager;
        
        // 初始化小地图系统
        InitializeMiniMap();
    }
    
    /// <summary>
    /// 初始化小地图系统
    /// </summary>
    private void InitializeMiniMap()
    {
        // 尝试查找场景中的小地图控制器
        _miniMapController = FindObjectOfType<MiniMapController>();
        
        // 如果找不到，创建一个新的
        if (_miniMapController == null)
        {
            GameObject miniMapObject = new GameObject("MiniMapController");
            _miniMapController = miniMapObject.AddComponent<MiniMapController>();
        }
        
        // 获取小地图渲染纹理
        _miniMapTexture = _miniMapController.GetMiniMapTexture();
    }
    
    private void OnGUI()
    {
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 24;
        buttonStyle.fixedHeight = 60;
        buttonStyle.fixedWidth = 200;

        // 绘制中央的匹配/准备按钮
        if (!_networkManager.IsConnected || (_networkManager.IsConnected && !_networkManager.IsMatched) || (_networkManager.IsMatched && !_networkManager.IsReady))
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
            
            if (!_networkManager.IsConnected)
            {
                GUILayout.Space(10);
                if (GUILayout.Button("匹配", buttonStyle))
                {
                    // 连接将在Ra2Demo主类中处理
                }
            }
            else if (_networkManager.IsConnected && !_networkManager.IsMatched)
            {
                GUILayout.Label("匹配中...", buttonStyle);
            }
            else if (_networkManager.IsMatched && !_networkManager.IsReady)
            {
                GUILayout.Label("等待中", buttonStyle);
            }
            
            GUILayout.EndArea();
        }
        
        // 绘制生产按钮（左下角）
        if (_networkManager.IsReady)
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

        // 绘制小地图
        DrawMiniMap();

        // 绘制左上角的房间类型选择和本地测试选项
        if (!_networkManager.IsConnected) 
        {
            Rect roomTypeRect = new Rect(20, 20, 250, 800);
            GUILayout.BeginArea(roomTypeRect);
            // 添加本地测试选项
            bool previousValue = _networkManager.useLocalServer;
            _networkManager.useLocalServer = GUILayout.Toggle(_networkManager.useLocalServer, "使用本地服务器");
            // 如果值发生变化，保存设置
            if (_networkManager.useLocalServer != previousValue)
            {
                _networkManager.SaveLocalServerOption();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("房间类型:", buttonStyle);
            
            // 自定义下拉列表实现
            if (GUILayout.Button(roomTypeOptions[(int)_networkManager.selectedRoomType - 1], buttonStyle))
            {
                showRoomTypeDropdown = !showRoomTypeDropdown;
            }
            
            if (showRoomTypeDropdown)
            {
                for (int i = 0; i < roomTypeOptions.Length; i++)
                {
                    if (GUILayout.Button(roomTypeOptions[i], buttonStyle))
                    {
                        _networkManager.selectedRoomType = (RoomType)(i + 1);
                        showRoomTypeDropdown = false;
                        _networkManager.SaveRoomTypeSelection(); // 保存选择
                    }
                }
            }
            GUILayout.EndArea();
        }
        else
        {
            // 将重新开始按钮定位在左上角
            Rect restartButtonRect = new Rect(20, 20, 300, 200);
            GUILayout.BeginArea(restartButtonRect);
            
            if (GUILayout.Button("重新开始", buttonStyle))
            {
                onRestartGame?.Invoke();
            }
            
            GUILayout.EndArea();
            
            // 绘制ping值显示（在重新开始按钮下方）
            GUIStyle pingStyle = new GUIStyle(GUI.skin.label);
            pingStyle.fontSize = 20;
            pingStyle.normal.textColor = Color.white;
            
            string pingText = _networkManager.CurrentPing >= 0 ? $"Ping: {_networkManager.CurrentPing}ms" : "Ping: --";
            GUI.Label(new Rect(20, 90, 200, 30), pingText, pingStyle);
            
            // 绘制调试显示开关（在ping值显示下方）
            GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.fontSize = 16;
            toggleStyle.normal.textColor = Color.green;
            // 选中颜色
            toggleStyle.onNormal.textColor = Color.red;
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = Color.white;
            
            Rect toggleRect = new Rect(20, 130, 200, 150);
            GUILayout.BeginArea(toggleRect);
            showUnits = GUILayout.Toggle(showUnits, "显示单位", toggleStyle);
            showRVOAgents = GUILayout.Toggle(showRVOAgents, "显示RVO智能体", toggleStyle);
            
            showGrid = GUILayout.Toggle(showGrid, "显示网格", toggleStyle);
            showObstacles = GUILayout.Toggle(showObstacles, "显示障碍物", toggleStyle);
            showFlowField = GUILayout.Toggle(showFlowField, "显示流场", toggleStyle);

            // 显示流场数量
            if (showFlowField && _game != null && _game.FlowFieldManager != null)
            {
                int flowFieldCount = _game.FlowFieldManager.GetActiveFieldCount();
                GUILayout.Label($"流场数量: {flowFieldCount}", labelStyle);
            }
            
            // 显示RVO agents数量
            if (showRVOAgents && _game != null && _game.RvoSimulator != null)
            {
                int rvoAgentCount = _game.RvoSimulator.GetNumAgents();
                GUILayout.Label($"RVO智能体数量: {rvoAgentCount}", labelStyle);
            }
            
            GUILayout.EndArea();
        }

        DrawGMConsole();
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
    /// 处理小地图点击事件
    /// </summary>
    /// <param name="miniMapRect">小地图在屏幕上的矩形区域</param>
    private void HandleMiniMapClick(Rect miniMapRect)
    {
        // 只有在游戏开始后才响应点击
        if (!_networkManager.IsReady || _mainCamera == null || _miniMapController == null)
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
        if (_mainCamera == null || _miniMapController == null)
            return;
            
        // 获取地图边界
        Vector4 mapBounds = _miniMapController.GetMapBounds();
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
        GUI.DrawTexture(new Rect(drawX - boxSize/2, drawY - boxSize/2, boxSize, 2), Texture2D.whiteTexture); // 上边框
        GUI.DrawTexture(new Rect(drawX - boxSize/2, drawY - boxSize/2, 2, boxSize), Texture2D.whiteTexture); // 左边框
        GUI.DrawTexture(new Rect(drawX + boxSize/2 - 2, drawY - boxSize/2, 2, boxSize), Texture2D.whiteTexture); // 右边框
        GUI.DrawTexture(new Rect(drawX - boxSize/2, drawY + boxSize/2 - 2, boxSize, 2), Texture2D.whiteTexture); // 下边框
        
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

        if (_game != null && _game.World != null && _game.World.GMManager != null)
        {
            var logHistory = _game.World.GMManager.LogHistory;
            foreach (var log in logHistory)
            {
                GUILayout.Label(log);
            }
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        // 输入框
        GUI.SetNextControlName("InputField");
        _inputFieldGM = GUILayout.TextField(_inputFieldGM, GUILayout.ExpandWidth(true));
        // 确保输入框获得焦点
        /*if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
        {
            //_game.SubmitCommand(new GMCommand(_inputFieldGM));
            Event.current.Use(); // 防止重复处理
        }*/
        GUILayout.FlexibleSpace();
        // 执行按钮
        if (GUILayout.Button("Execute", GUILayout.Width(60)))
        {
            //_game.SubmitCommand(new GMCommand(_inputFieldGM));
        }
        GUILayout.EndHorizontal();
        GUI.FocusControl("InputField");
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
                _networkManager.SendProduceCommand(selectedFactoryEntityId, unitType, -1);
            }
            
            GUILayout.Space(20); // 中间空白
            
            // 增加按钮
            if (GUILayout.Button("+", buttonStyle, GUILayout.Width(50), GUILayout.Height(30)) && produceNumber < 99)
            {
                _networkManager.SendProduceCommand(selectedFactoryEntityId, unitType, 1);
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
    private string GetUnitTypeName(int unitType)
    {
        switch (unitType)
        {
            case 1: return "动员兵";
            case 2: return "坦克";
            case 3: return "矿车";
            default: return $"单位{unitType}";
        }
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
    /// <returns>生产建筑实体ID列表</returns>
    private List<int> GetLocalPlayerProductionBuildings()
    {
        List<int> result = new List<int>();
        
        if (_game == null || _game.World == null)
            return result;

        // 获取所有具有生产功能的建筑实体
        var entities = _game.World.ComponentManager
            .GetAllEntityIdsWith<ProduceComponent>();
            
        foreach (var entityId in entities)
        {
            var entity = new Entity(entityId);

            // 检查实体是否包含LocalPlayerComponent（只有本地玩家单位才能被选择）
            if (!_game.World.ComponentManager.HasComponent<LocalPlayerComponent>(entity))
            {
                continue; // 跳过非本地玩家单位
            }
            
            // 确保实体同时具有建筑组件
            if (!_game.World.ComponentManager.HasComponent<BuildingComponent>(entity))
            {
                continue;
            }
            
            result.Add(entityId);
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
    
    public void ToggleConsole()
    {
        _isConsoleVisible = !_isConsoleVisible;
    }
    
    public bool IsConsoleVisible => _isConsoleVisible;
    
    public void GetVisualizationSettings(out bool units, out bool rvoAgents, out bool grid, out bool obstacles, out bool flowField)
    {
        units = showUnits;
        rvoAgents = showRVOAgents;
        grid = showGrid;
        obstacles = showObstacles;
        flowField = showFlowField;
    }
}