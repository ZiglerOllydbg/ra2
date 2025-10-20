using UnityEngine;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS.Systems;
using ZLockstep.View.Systems;

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

        // 逻辑世界
        private zWorld _logicWorld;

        // 实体工厂
        private EntityFactory _entityFactory;

        // 表现系统
        private PresentationSystem _presentationSystem;

        // 逻辑帧计时器
        private float _logicTimer = 0f;
        private float _logicInterval;

        /// <summary>
        /// 获取逻辑世界
        /// </summary>
        public zWorld LogicWorld => _logicWorld;

        /// <summary>
        /// 获取实体工厂
        /// </summary>
        public EntityFactory EntityFactory => _entityFactory;

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

            // 创建实体工厂
            if (viewRoot == null)
                viewRoot = transform;
            _entityFactory = new EntityFactory(_logicWorld, viewRoot);

            // 注册游戏系统（按执行顺序）
            RegisterSystems();

            Debug.Log($"[GameWorldBridge] 逻辑世界初始化完成，帧率: {logicFrameRate} FPS");
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
            // 如果启用了插值，在Update中进行平滑处理
            if (enableSmoothInterpolation && _presentationSystem != null)
            {
                _presentationSystem.LerpUpdate(Time.deltaTime, interpolationSpeed);
            }
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

