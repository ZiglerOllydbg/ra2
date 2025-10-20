using UnityEngine;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using zUnity;

namespace ZLockstep.View
{
    /// <summary>
    /// 实体工厂：创建带表现的游戏实体
    /// 负责同时创建逻辑实体和Unity表现
    /// </summary>
    public class EntityFactory
    {
        private readonly zWorld _world;
        private readonly Transform _viewRoot;

        /// <summary>
        /// 创建实体工厂
        /// </summary>
        /// <param name="world">游戏逻辑世界</param>
        /// <param name="viewRoot">Unity中的表现根节点（用于组织GameObject层级）</param>
        public EntityFactory(zWorld world, Transform viewRoot)
        {
            _world = world;
            _viewRoot = viewRoot;
        }

        #region 单位创建

        /// <summary>
        /// 创建一个单位实体（逻辑+视觉）
        /// </summary>
        /// <param name="unitType">单位类型</param>
        /// <param name="position">初始位置</param>
        /// <param name="playerId">所属玩家ID</param>
        /// <param name="prefab">Unity预制体</param>
        /// <param name="enableInterpolation">是否启用插值</param>
        /// <returns>创建的实体</returns>
        public Entity CreateUnit(int unitType, zVector3 position, int playerId, GameObject prefab, bool enableInterpolation = false)
        {
            // 1. 创建逻辑实体
            var entity = _world.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            _world.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one
            });

            // 3. 根据单位类型添加Unit组件
            UnitComponent unitComponent = CreateUnitComponent(unitType, playerId);
            _world.ComponentManager.AddComponent(entity, unitComponent);

            // 4. 添加Health组件
            var healthComponent = CreateHealthComponent(unitType);
            _world.ComponentManager.AddComponent(entity, healthComponent);

            // 5. 如果单位有攻击能力，添加Attack组件
            if (CanAttack(unitType))
            {
                var attackComponent = CreateAttackComponent(unitType);
                _world.ComponentManager.AddComponent(entity, attackComponent);
            }

            // 6. 创建Unity表现（如果提供了prefab）
            if (prefab != null)
            {
                CreateView(entity, prefab, enableInterpolation);
            }

            return entity;
        }

        /// <summary>
        /// 创建动员兵
        /// </summary>
        public Entity CreateInfantry(zVector3 position, int playerId, GameObject prefab)
        {
            return CreateUnit(1, position, playerId, prefab);
        }

        /// <summary>
        /// 创建犀牛坦克
        /// </summary>
        public Entity CreateTank(zVector3 position, int playerId, GameObject prefab)
        {
            return CreateUnit(2, position, playerId, prefab);
        }

        /// <summary>
        /// 创建矿车
        /// </summary>
        public Entity CreateHarvester(zVector3 position, int playerId, GameObject prefab)
        {
            return CreateUnit(3, position, playerId, prefab);
        }

        #endregion

        #region 建筑创建

        /// <summary>
        /// 创建建筑实体
        /// </summary>
        public Entity CreateBuilding(int buildingType, zVector3 position, int playerId, GameObject prefab)
        {
            var entity = _world.EntityManager.CreateEntity();

            // Transform
            _world.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one
            });

            // Health (建筑通常血量更高)
            _world.ComponentManager.AddComponent(entity, new HealthComponent((zfloat)500.0f));

            // TODO: 添加BuildingComponent

            // View
            if (prefab != null)
            {
                CreateView(entity, prefab, false); // 建筑不需要插值
            }

            return entity;
        }

        #endregion

        #region 视图创建与销毁

        /// <summary>
        /// 为实体创建Unity表现
        /// </summary>
        private void CreateView(Entity entity, GameObject prefab, bool enableInterpolation)
        {
            // 实例化GameObject
            GameObject viewObject = Object.Instantiate(prefab, _viewRoot);

            // 设置名称（便于调试）
            viewObject.name = $"Entity_{entity.Id}";

            // 创建ViewComponent
            var viewComponent = ViewComponent.Create(viewObject, enableInterpolation);

            // 添加到实体
            _world.ComponentManager.AddComponent(entity, viewComponent);
        }

        /// <summary>
        /// 销毁实体（逻辑+视觉）
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            // 1. 销毁Unity对象
            if (_world.ComponentManager.HasComponent<ViewComponent>(entity))
            {
                var view = _world.ComponentManager.GetComponent<ViewComponent>(entity);
                view.Destroy();
            }

            // 2. 销毁逻辑实体
            _world.EntityManager.DestroyEntity(entity);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据单位类型创建UnitComponent
        /// </summary>
        private UnitComponent CreateUnitComponent(int unitType, int playerId)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                    return UnitComponent.CreateInfantry(playerId);
                case 2: // 犀牛坦克
                    return UnitComponent.CreateTank(playerId);
                case 3: // 矿车
                    return UnitComponent.CreateHarvester(playerId);
                default:
                    return UnitComponent.CreateInfantry(playerId);
            }
        }

        /// <summary>
        /// 根据单位类型创建HealthComponent
        /// </summary>
        private HealthComponent CreateHealthComponent(int unitType)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                    return new HealthComponent((zfloat)50.0f);
                case 2: // 犀牛坦克
                    return new HealthComponent((zfloat)200.0f);
                case 3: // 矿车
                    return new HealthComponent((zfloat)150.0f);
                default:
                    return new HealthComponent((zfloat)100.0f);
            }
        }

        /// <summary>
        /// 根据单位类型创建AttackComponent
        /// </summary>
        private AttackComponent CreateAttackComponent(int unitType)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                    return new AttackComponent
                    {
                        Damage = (zfloat)10.0f,
                        Range = (zfloat)4.0f,
                        AttackInterval = (zfloat)1.0f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                case 2: // 犀牛坦克
                    return new AttackComponent
                    {
                        Damage = (zfloat)30.0f,
                        Range = (zfloat)8.0f,
                        AttackInterval = (zfloat)2.0f,
                        TimeSinceLastAttack = zfloat.Zero,
                        TargetEntityId = -1
                    };
                default:
                    return AttackComponent.CreateDefault();
            }
        }

        /// <summary>
        /// 判断单位是否有攻击能力
        /// </summary>
        private bool CanAttack(int unitType)
        {
            switch (unitType)
            {
                case 1: // 动员兵
                case 2: // 犀牛坦克
                    return true;
                case 3: // 矿车
                    return false;
                default:
                    return false;
            }
        }

        #endregion
    }
}

