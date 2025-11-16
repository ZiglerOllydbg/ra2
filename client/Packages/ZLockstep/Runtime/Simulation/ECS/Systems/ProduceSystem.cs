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
        private void CreateUnit(zWorld world, Entity factoryEntity, int unitType, zVector3 factoryPosition)
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
            int prefabId = 0; // 默认预制体ID
            switch (unitType)
            {
                case 1: // 动员兵
                    prefabId = 1;
                    break;
                case 2: // 坦克
                    prefabId = 2;
                    break;
                case 3: // 矿车
                    prefabId = 3;
                    break;
            }
            
            var unitEvent = EntityCreationManager.CreateUnitEntity(World, playerId, unitType, spawnPosition, prefabId, world.GameInstance.GetNavSystem());
            
            // 发布单位创建事件
            EventManager.Publish(unitEvent);
            
            zUDebug.Log($"[ProduceSystem] 玩家{playerId}的工厂{factoryEntity.Id}生产了单位类型{unitType}，位置{spawnPosition}");
        }
    }
}