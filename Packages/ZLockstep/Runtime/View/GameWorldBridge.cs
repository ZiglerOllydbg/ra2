using UnityEngine;
using System.Collections.Generic;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.View.Systems;
using ZLockstep.Sync.Command;

namespace ZLockstep.View
{
    /// <summary>
    /// 游戏世界桥接器
    /// 连接逻辑层(zWorld)和表现层(Unity)的桥梁
    /// 通常挂载在Unity场景中的管理对象上
    /// </summary>
    public class GameWorldBridge : MonoBehaviour
    {
        [Header("逻辑设置")]
        [Tooltip("逻辑帧率（帧/秒）")]
        public int logicFrameRate = 20;

        [Header("表现设置")]
        [Tooltip("是否启用平滑插值")]
        public bool enableSmoothInterpolation = true;

        [Tooltip("插值速度")]
        public float interpolationSpeed = 10f;

        [Header("引用")]
        [Tooltip("表现层根节点")]
        public Transform viewRoot;

        [Header("预制体设置")]
        [Tooltip("单位预制体列表（索引对应单位类型）")]
        public GameObject[] unitPrefabs = new GameObject[10];

        // 逻辑世界
        private zWorld _logicWorld;

        // 实体工厂
        private EntityFactory _entityFactory;

        // 表现系统
        private PresentationSystem _presentationSystem;

        // 视图事件监听器
        private ViewEventListener _viewEventListener;

        // 命令缓冲区
        private CommandBuffer _commandBuffer;

        // 逻辑帧计时器
        private float _logicTimer = 0f;
        private float _logicInterval;

        /// <summary>
        /// 获取逻辑世界
        /// </summary>
        public zWorld LogicWorld => _logicWorld;

        /// <summary>
        /// 获取实体工厂（已废弃，建议使用Command系统）
        /// </summary>
        [System.Obsolete("建议使用 SubmitCommand 方法代替直接使用EntityFactory")]
        public EntityFactory EntityFactory => _entityFactory;

        /// <summary>
        /// 获取命令缓冲区
        /// </summary>
        public CommandBuffer CommandBuffer => _commandBuffer;

        private void Awake()
        {
            InitializeLogicWorld();
        }

        /// <summary>
        /// 初始化逻辑世界
        /// </summary>
        private void InitializeLogicWorld()
        {
            // 创建逻辑世界
            _logicWorld = new zWorld();
            _logicWorld.Init(logicFrameRate);

            // 计算逻辑帧间隔
            _logicInterval = 1.0f / logicFrameRate;

            // 创建视图根节点
            if (viewRoot == null)
                viewRoot = transform;

            // 创建实体工厂（保留用于兼容）
            _entityFactory = new EntityFactory(_logicWorld, viewRoot);

            // 创建命令缓冲区
            _commandBuffer = new CommandBuffer(_logicWorld.CommandManager);

            // 创建视图事件监听器
            _viewEventListener = new ViewEventListener(_logicWorld, viewRoot);
            RegisterPrefabs();

            // 注册游戏系统（按执行顺序）
            RegisterSystems();

            Debug.Log($"[GameWorldBridge] 逻辑世界初始化完成，帧率: {logicFrameRate} FPS");
        }

        /// <summary>
        /// 注册预制体到视图监听器
        /// </summary>
        private void RegisterPrefabs()
        {
            for (int i = 0; i < unitPrefabs.Length; i++)
            {
                if (unitPrefabs[i] != null)
                {
                    _viewEventListener.RegisterUnitPrefab(i, unitPrefabs[i]);
                }
            }
        }

        /// <summary>
        /// 注册所有游戏系统
        /// </summary>
        private void RegisterSystems()
        {
            // 1. 移动系统
            _logicWorld.SystemManager.RegisterSystem(new MovementSystem());

            // TODO: 2. 战斗系统
            // _logicWorld.SystemManager.RegisterSystem(new CombatSystem());

            // TODO: 3. AI系统
            // _logicWorld.SystemManager.RegisterSystem(new HarvesterAISystem());

            // 最后: 表现同步系统（必须最后执行）
            _presentationSystem = new PresentationSystem();
            _presentationSystem.EnableSmoothInterpolation = enableSmoothInterpolation;
            _logicWorld.SystemManager.RegisterSystem(_presentationSystem);
        }

        private void FixedUpdate()
        {
            // 在FixedUpdate中执行逻辑帧
            // 这样可以保证确定性
            _logicWorld.Update();
        }

        private void Update()
        {
            // 处理视图事件
            _viewEventListener?.ProcessEvents();

            // 如果启用了插值，在Update中进行平滑处理
            if (enableSmoothInterpolation && _presentationSystem != null)
            {
                _presentationSystem.LerpUpdate(Time.deltaTime, interpolationSpeed);
            }
        }

        /// <summary>
        /// 提交命令到游戏世界
        /// </summary>
        public void SubmitCommand(ICommand command)
        {
            _logicWorld.CommandManager.SubmitCommand(command);
        }

        /// <summary>
        /// 批量提交命令
        /// </summary>
        public void SubmitCommands(IEnumerable<ICommand> commands)
        {
            _logicWorld.CommandManager.SubmitCommands(commands);
        }

        private void OnDestroy()
        {
            // 清理逻辑世界
            _logicWorld?.Shutdown();
        }

        #region 调试方法

        /// <summary>
        /// 创建测试单位
        /// </summary>
        [ContextMenu("创建测试单位")]
        public void CreateTestUnit()
        {
            if (_entityFactory == null)
            {
                Debug.LogWarning("实体工厂未初始化");
                return;
            }

            // 这里需要提供预制体
            // var entity = _entityFactory.CreateInfantry(
            //     new zVector3(0, 0, 0), 
            //     playerId: 0, 
            //     prefab: yourPrefab
            // );

            Debug.Log("请在代码中提供预制体引用");
        }

        #endregion
    }
}

