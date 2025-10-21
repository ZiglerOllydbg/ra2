using UnityEngine;
using System.Collections.Generic;
using ZLockstep.Sync;
using ZLockstep.Sync.Command;
using ZLockstep.View.Systems;
using System.Linq;

namespace ZLockstep.View
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// 【第3层：Unity桥接层】GameWorldBridge
    /// ═══════════════════════════════════════════════════════════════
    /// 
    /// 职责定位：
    /// - Unity生命周期适配：连接Unity的MonoBehaviour和纯C# Game
    /// - Unity资源管理：管理预制体、Transform、场景对象
    /// - Unity特定功能：协程、Gizmos、Inspector配置
    /// - 表现层桥接：将逻辑事件转换为Unity视觉呈现
    /// 
    /// 设计原则（奥卡姆剃刀）：
    /// - 只负责"桥接"，不负责"逻辑"（逻辑在Game/zWorld）
    /// - 持有Game实例，不创建zWorld（避免重复创建）
    /// - Unity特定代码只在这一层，保持Game的纯净
    /// 
    /// 层次关系：
    /// - 上层：被Unity场景调用（Awake/Update/FixedUpdate）
    /// - 下层：创建并驱动 Game，Game再驱动 zWorld
    /// 
    /// 数据流：
    ///   Unity Input → GameWorldBridge → Game → zWorld → CommandManager → ECS
    ///   ECS Events → PresentationSystem → Unity GameObject
    /// 
    /// 使用示例：
    ///   1. 在Unity场景中创建空对象
    ///   2. 添加 GameWorldBridge 组件
    ///   3. 配置 unitPrefabs 数组
    ///   4. 点击运行即可
    /// </summary>
    public class GameWorldBridge : MonoBehaviour
    {
        [Header("游戏配置")]
        [Tooltip("逻辑帧率（帧/秒）")]
        [SerializeField] private int logicFrameRate = 20;
        
        [Tooltip("本地玩家ID")]
        [SerializeField] private int localPlayerId = 0;

        [Header("Unity资源")]
        [Tooltip("表现层根节点（所有实体GameObject的父节点）")]
        [SerializeField] private Transform viewRoot;
        
        [Tooltip("单位预制体列表（索引=单位类型ID）")]
        [SerializeField] private GameObject[] unitPrefabs = new GameObject[10];

        [Header("表现设置")]
        [Tooltip("是否启用插值（让移动更平滑）")]
        [SerializeField] private bool enableSmoothInterpolation = true;
        
        [Tooltip("插值速度")]
        [SerializeField] private float interpolationSpeed = 10f;

        // ═══════════════════════════════════════════════════════════
        // 核心：持有 Game 实例（不是创建 zWorld！）
        // ═══════════════════════════════════════════════════════════
        private Game _game;

        // Unity特定：表现系统
        private PresentationSystem _presentationSystem;

        /// <summary>
        /// 暴露Game供外部访问（如Test.cs）
        /// </summary>
        public Game Game => _game;
        
        /// <summary>
        /// 快捷访问：逻辑世界（实际是 _game.World）
        /// </summary>
        public Simulation.zWorld LogicWorld => _game?.World;

        // ═══════════════════════════════════════════════════════════
        // Unity生命周期
        // ═══════════════════════════════════════════════════════════

        private void Awake()
        {
            InitializeGame();
            InitializeUnityView();
        }

        private void FixedUpdate()
        {
            // 驱动 Game（Game再驱动zWorld）
            _game?.Update();
        }

        private void Update()
        {
            // Unity特定：插值更新（让移动更平滑）
            if (enableSmoothInterpolation && _presentationSystem != null)
            {
                _presentationSystem.LerpUpdate(Time.deltaTime, interpolationSpeed);
            }
        }

        private void OnDestroy()
        {
            _game?.Shutdown();
        }

        // ═══════════════════════════════════════════════════════════
        // 初始化
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 初始化纯逻辑部分（Game + zWorld）
        /// </summary>
        private void InitializeGame()
        {
            // 创建 Game（Game会创建唯一的zWorld）
            _game = new Game(logicFrameRate, localPlayerId);
            _game.Init();

            Debug.Log($"[GameWorldBridge] Game初始化完成");
        }

        /// <summary>
        /// 初始化Unity视图部分
        /// </summary>
        private void InitializeUnityView()
        {
            // 设置视图根节点
            if (viewRoot == null)
                viewRoot = transform;

            // 创建表现系统
            _presentationSystem = new PresentationSystem();
            _presentationSystem.EnableSmoothInterpolation = enableSmoothInterpolation;
            
            // 准备预制体字典
            var prefabDict = new Dictionary<int, GameObject>();
            for (int i = 0; i < unitPrefabs.Length; i++)
            {
                if (unitPrefabs[i] != null)
                {
                    prefabDict[i] = unitPrefabs[i];
                    Debug.Log($"[GameWorldBridge] 注册预制体: Type{i} = {unitPrefabs[i].name}");
                }
            }
            
            // 初始化表现系统并注册到Game
            _presentationSystem.Initialize(viewRoot, prefabDict);
            _game.World.SystemManager.RegisterSystem(_presentationSystem);

            Debug.Log($"[GameWorldBridge] Unity视图初始化完成，预制体数量: {prefabDict.Count}");
        }

        // ═══════════════════════════════════════════════════════════
        // 对外接口（桥接方法）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 提交命令（桥接到Game）
        /// </summary>
        public void SubmitCommand(ICommand command)
        {
            _game?.SubmitCommand(command);
        }

        // ═══════════════════════════════════════════════════════════
        // 调试辅助
        // ═══════════════════════════════════════════════════════════

        private void OnDrawGizmos()
        {
            if (_game == null || _game.World == null)
                return;

            // 可视化所有实体的逻辑位置
            var transforms = _game.World.ComponentManager
                .GetAllEntityIdsWith<Simulation.ECS.Components.TransformComponent>();

            foreach (var entityId in transforms)
            {
                var entity = new Simulation.ECS.Entity(entityId);
                var transform = _game.World.ComponentManager
                    .GetComponent<Simulation.ECS.Components.TransformComponent>(entity);

                Vector3 pos = transform.Position.ToVector3();
                
                // 绘制实体位置
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(pos, Vector3.one * 0.5f);

                // 如果有移动命令，绘制目标
                if (_game.World.ComponentManager.HasComponent<Simulation.ECS.Components.MoveCommandComponent>(entity))
                {
                    var moveCmd = _game.World.ComponentManager
                        .GetComponent<Simulation.ECS.Components.MoveCommandComponent>(entity);
                    
                    Vector3 targetPos = moveCmd.TargetPosition.ToVector3();
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(targetPos, 0.3f);
                    Gizmos.DrawLine(pos, targetPos);
                }
            }
        }

        #region Inspector信息显示

#if UNITY_EDITOR
        [UnityEditor.CustomEditor(typeof(GameWorldBridge))]
        public class GameWorldBridgeEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                var bridge = target as GameWorldBridge;
                if (bridge != null && bridge._game != null && bridge._game.World != null)
                {
                    UnityEditor.EditorGUILayout.Space();
                    UnityEditor.EditorGUILayout.LabelField("═══ 运行时信息 ═══", UnityEditor.EditorStyles.boldLabel);
                    UnityEditor.EditorGUILayout.LabelField("当前帧", bridge._game.World.Tick.ToString());
                    
                    var entityCount = bridge._game.World.ComponentManager
                        .GetAllEntityIdsWith<Simulation.ECS.Components.TransformComponent>()
                        .Count();
                    UnityEditor.EditorGUILayout.LabelField("实体数量", entityCount.ToString());
                }
            }
        }
#endif

        #endregion
    }
}
