using System.Collections.Generic;
using zUnity;
using ZLockstep.Simulation.ECS.Components;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 生产系统
    /// 处理建筑的单位生产逻辑
    /// </summary>
    public class ProduceSystem : BaseSystem
    {
        public override int GetOrder()
        {
            return (int)SystemOrder.Produce;
        }

        public override void Update()
        {
            // 获取所有具有ProduceComponent的实体
            var entities = ComponentManager.GetAllEntityIdsWith<ProduceComponent>();
            
            foreach (var entityId in entities)
            {
                var entity = new Entity(entityId);
                
                // 检查建筑是否已完成建造
                if (ComponentManager.HasComponent<BuildingConstructionComponent>(entity))
                {
                    // 建造未完成，不能生产
                    continue;
                }
                
                var produceComponent = ComponentManager.GetComponent<ProduceComponent>(entity);
                var transformComponent = ComponentManager.GetComponent<TransformComponent>(entity);
                
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

                            // 数量减1
                            produceNumber -= 1;
                            produceComponent.ProduceNumbers[unitType] = produceNumber;
                            ComponentManager.AddComponent(entity, produceComponent);

                            
                            // 生产单位（这里需要调用单位创建逻辑）
                            CreateUnit(World, entity, unitType, transformComponent.Position);
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
                    ComponentManager.AddComponent(entity, produceComponent);
                }
            }
        }


        /// <summary>
        /// 创建单位
        /// </summary>
        /// <param name="world">游戏世界</param>
        /// <param name="factoryEntity">工厂实体</param>
        /// <param name="unitType">单位类型</param>
        /// <param name="factoryPosition">工厂位置</param>
        private void CreateUnit(zWorld world, Entity factoryEntity, UnitType unitType, zVector3 factoryPosition)
        {
            // 获取阵营组件以确定玩家ID
            if (!ComponentManager.HasComponent<CampComponent>(factoryEntity))
            {
                UnityEngine.Debug.LogWarning($"[ProduceSystem] 工厂实体 {factoryEntity.Id} 没有阵营组件");
                return;
            }
            
            var campComponent = ComponentManager.GetComponent<CampComponent>(factoryEntity);
            int playerId = campComponent.CampId;
            
            // 简单地在工厂位置附近创建单位
            // 在实际实现中，可能需要更复杂的逻辑来确定单位的精确生成位置
            zVector3 spawnPosition = factoryPosition;
            spawnPosition.z -= (zfloat)10.0f; // 稍微偏移位置
            
            // 使用EntityCreationManager创建单位
            int prefabId = 6; // 默认预制体ID
            
            var unitEvent = EntityCreationManager.CreateUnitEntity(World, playerId, unitType, spawnPosition, prefabId);
            
            if (unitEvent.HasValue)
            {
                // 发布单位创建事件
                EventManager.Publish(unitEvent.Value);

                // 为新单位设置一个附近的随机目标，触发流场分散逻辑
                var navSystem = world.GameInstance.GetNavSystem();
                zVector2 randomTarget = GetRandomTarget(new zVector2(spawnPosition.x, spawnPosition.z), (zfloat)10.0f);
                navSystem.SetMoveTarget(new Entity(unitEvent.Value.EntityId), randomTarget);
            }

            zUDebug.Log($"[ProduceSystem] 玩家{playerId}的工厂{factoryEntity.Id}生产了单位类型{unitType}，位置{spawnPosition}");
        }
        
        /// <summary>
        /// 获取指定中心点附近随机位置的目标点
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">随机半径</param>
        /// <returns>随机目标点</returns>
        private zVector2 GetRandomTarget(zVector2 center, zfloat radius)
        {
            // 使用确定性随机数生成器
            long seed = (World.Tick * 137 + center.x.value * 31 + center.y.value * 17) % 1000000;
            zRandom random = new zRandom(seed);
            
            long randomAngle = zMathf.Abs(random.NextLong()) % 360L;
            long randomDistance = zMathf.Abs(random.NextLong()) % radius.value;
            
            zfloat angle = zfloat.CreateFloat(randomAngle * 10000); // 角度转为弧度
            zfloat distance = zfloat.CreateFloat(randomDistance);
            
            zfloat radian = angle * zMathf.PI / new zfloat(180);
            zfloat offsetX = zMathf.Cos(radian) * distance;
            zfloat offsetZ = zMathf.Sin(radian) * distance;
            
            return new zVector2(
                center.x + offsetX,
                center.y + offsetZ
            );
        }
    }
}