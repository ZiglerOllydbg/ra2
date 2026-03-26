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
                    ConfUnit confUnit = ConfigManager.Get<ConfUnit>((int)unitType);
                    int maxProgress = confUnit?.ProduceTime ?? 100;

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
                        if (progress >= maxProgress)
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
            // 获取阵营组件以确定玩家 ID
            if (!ComponentManager.HasComponent<CampComponent>(factoryEntity))
            {
                UnityEngine.Debug.LogWarning($"[ProduceSystem] 工厂实体 {factoryEntity.Id} 没有阵营组件");
                return;
            }
            
            var campComponent = ComponentManager.GetComponent<CampComponent>(factoryEntity);
            int playerId = campComponent.CampId;
            
            // 从 ConfCamp 配置表中读取生产出生点
            zVector3 baseSpawnPosition = factoryPosition;
           List<ConfCamp> confCamps = ConfigManager.GetAll<ConfCamp>();
            foreach (var confCamp in confCamps)
            {
                if (confCamp.ID == playerId)
                {
                    if (unitType == UnitType.Infantry)
                    {
                        // 解析生产位置字符串（格式："x,y,z"）
                        if (!string.IsNullOrEmpty(confCamp.BarracksPosition))
                        {
                            baseSpawnPosition = StringToVector3Converter.StringToZVector3(confCamp.BarracksPosition);
                        }
                    } 
                    else
                    {
                        if (!string.IsNullOrEmpty(confCamp.VehicleFactoryPosition))
                        {
                            baseSpawnPosition = StringToVector3Converter.StringToZVector3(confCamp.VehicleFactoryPosition);
                        }    
                    }
                    break;
                }
            }
            
            // 计算距离 baseSpawnPosition 2 米的出生点
            zVector3 spawnPosition = CalculateSpawnPosition(baseSpawnPosition, factoryPosition);
            
            // 使用 EntityCreationManager 创建单位
            int prefabId = 6; // 默认预制体 ID
            
            var unitEvent = EntityCreationManager.CreateUnitEntity(World, playerId, unitType, spawnPosition, prefabId);
            
            if (unitEvent.HasValue)
            {
                // 发布单位创建事件
                EventManager.Publish(unitEvent.Value);

                // 为新单位设置目标点，使用原始 spawnPosition
                var navSystem = world.GameInstance.GetNavSystem();
                zVector2 targetPos = new zVector2(baseSpawnPosition.x, baseSpawnPosition.z);
                navSystem.SetMoveTarget(new Entity(unitEvent.Value.EntityId), targetPos);
                zUDebug.Log($"[ProduceSystem] 玩家{playerId}的工厂{factoryEntity.Id}生产了单位类型{unitType}，出生点{spawnPosition}，目标点{targetPos}");
            }

        }
        
        /// <summary>
        /// 计算距离目标点 2 米的出生位置
        /// </summary>
        /// <param name="targetPosition">目标位置（配置表中的出生点）</param>
        /// <param name="fromPosition">起始位置（工厂位置）</param>
        /// <returns>距离目标点 2 米的出生位置</returns>
        private zVector3 CalculateSpawnPosition(zVector3 targetPosition, zVector3 fromPosition)
        {
            // 计算从工厂到目标点的方向向量
            zVector3 direction = targetPosition - fromPosition;
            direction = direction.normalized;
            
            // 在目标点前方 2 米处设置出生点
            zfloat distance = new zfloat(2);
            zVector3 spawnPos = fromPosition + direction * distance;
            
            return spawnPos;
        }
    }
}