using System.Collections.Generic;
using zUnity;
using ZLockstep.Simulation.ECS.Components;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 生产系统
    /// 处理建筑的单位生产逻辑
    /// </summary>
    public class ProduceSystem : ISystem
    {
        public int GetOrder()
        {
            return (int)SystemOrder.Produce;
        }

        public void SetWorld(zWorld world)
        {
            throw new System.NotImplementedException();
        }


        public void Update(zWorld world)
        {
            var componentManager = world.ComponentManager;
            
            // 获取所有具有ProduceComponent的实体
            var entities = componentManager.GetAllEntityIdsWith<ProduceComponent>();
            
            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                var produceComponent = componentManager.GetComponent<ProduceComponent>(entity);
                var transformComponent = componentManager.GetComponent<TransformComponent>(entity);
                
                bool componentUpdated = false;
                
                // 遍历所有支持生产的单位类型
                foreach (var unitType in produceComponent.SupportedUnitTypes)
                {
                    // 获取当前生产数量
                    int produceNumber = produceComponent.ProduceNumbers.ContainsKey(unitType) ? 
                        produceComponent.ProduceNumbers[unitType] : 0;
                    
                    // 获取当前生产进度
                    int progress = produceComponent.ProduceProgress.ContainsKey(unitType) ? 
                        produceComponent.ProduceProgress[unitType] : 0;
                    
                    // 如果生产数量大于0，增加进度
                    if (produceNumber > 0)
                    {
                        progress += 1;
                        
                        // 如果进度达到100，生产一个单位
                        if (progress >= 100)
                        {
                            // 重置进度
                            progress = 0;
                            
                            // 生产单位（这里需要调用单位创建逻辑）
                            CreateUnit(world, entity, unitType, transformComponent.Position);
                        }
                    }
                    else
                    {
                        // 如果生产数量为0，重置进度
                        progress = 0;
                    }
                    
                    // 更新进度
                    if (!produceComponent.ProduceProgress.ContainsKey(unitType))
                    {
                        produceComponent.ProduceProgress.Add(unitType, progress);
                    }
                    else
                    {
                        produceComponent.ProduceProgress[unitType] = progress;
                    }
                    
                    componentUpdated = true;
                }
                
                // 如果组件有更新，保存回组件管理器
                if (componentUpdated)
                {
                    componentManager.AddComponent(entity, produceComponent);
                }
            }
        }

        public void Update()
        {
            throw new System.NotImplementedException();
        }


        /// <summary>
        /// 创建单位
        /// </summary>
        /// <param name="world">游戏世界</param>
        /// <param name="factoryEntity">工厂实体</param>
        /// <param name="unitType">单位类型</param>
        /// <param name="factoryPosition">工厂位置</param>
        private void CreateUnit(zWorld world, Entity factoryEntity, int unitType, zVector3 factoryPosition)
        {
            // 获取阵营组件以确定玩家ID
            var componentManager = world.ComponentManager;
            if (!componentManager.HasComponent<CampComponent>(factoryEntity))
            {
                UnityEngine.Debug.LogWarning($"[ProduceSystem] 工厂实体 {factoryEntity.Id} 没有阵营组件");
                return;
            }
            
            var campComponent = componentManager.GetComponent<CampComponent>(factoryEntity);
            int playerId = campComponent.CampId;
            
            // 简单地在工厂位置附近创建单位
            // 在实际实现中，可能需要更复杂的逻辑来确定单位的精确生成位置
            zVector3 spawnPosition = factoryPosition;
            spawnPosition.x += (zfloat)1.0f; // 稍微偏移位置
            
            // 使用EntityCreationManager创建单位
            // 注意：这需要访问EntityCreationManager，可能需要通过world或其他方式传递
            // 这里我们直接使用world的EntityManager来创建
            
            var unitEntity = world.EntityManager.CreateEntity();
            
            // 添加Transform组件
            world.ComponentManager.AddComponent(unitEntity, new TransformComponent
            {
                Position = spawnPosition,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one
            });
            
            // 添加单位组件
            UnitComponent unitComponent;
            switch (unitType)
            {
                case 1: // 动员兵
                    unitComponent = UnitComponent.CreateInfantry(playerId);
                    break;
                case 2: // 坦克
                    unitComponent = UnitComponent.CreateTank(playerId);
                    break;
                case 3: // 矿车
                    unitComponent = UnitComponent.CreateHarvester(playerId);
                    break;
                default:
                    unitComponent = UnitComponent.CreateInfantry(playerId);
                    break;
            }
            world.ComponentManager.AddComponent(unitEntity, unitComponent);
            
            // 添加生命值组件
            HealthComponent healthComponent;
            switch (unitType)
            {
                case 1: // 动员兵
                    healthComponent = new HealthComponent((zfloat)50.0f);
                    break;
                case 2: // 坦克
                    healthComponent = new HealthComponent((zfloat)200.0f);
                    break;
                case 3: // 矿车
                    healthComponent = new HealthComponent((zfloat)150.0f);
                    break;
                default:
                    healthComponent = new HealthComponent((zfloat)100.0f);
                    break;
            }
            world.ComponentManager.AddComponent(unitEntity, healthComponent);
            
            // 如果单位有攻击能力，添加攻击组件
            if (unitType == 1 || unitType == 2) // 动员兵和坦克有攻击能力
            {
                AttackComponent attackComponent;
                switch (unitType)
                {
                    case 1: // 动员兵
                        attackComponent = new AttackComponent
                        {
                            Damage = (zfloat)10.0f,
                            Range = (zfloat)4.0f,
                            AttackInterval = (zfloat)1.0f,
                            TimeSinceLastAttack = zfloat.Zero,
                            TargetEntityId = -1
                        };
                        break;
                    case 2: // 坦克
                        attackComponent = new AttackComponent
                        {
                            Damage = (zfloat)30.0f,
                            Range = (zfloat)10.0f,
                            AttackInterval = (zfloat)2.0f,
                            TimeSinceLastAttack = zfloat.Zero,
                            TargetEntityId = -1
                        };
                        break;
                    default:
                        attackComponent = AttackComponent.CreateDefault();
                        break;
                }
                world.ComponentManager.AddComponent(unitEntity, attackComponent);
            }
            
            zUDebug.Log($"[ProduceSystem] 玩家{playerId}的工厂{factoryEntity.Id}生产了单位类型{unitType}，位置{spawnPosition}");
        }
    }
}