using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Components;
using zUnity;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 经济系统
    /// 处理游戏中的经济逻辑，包括采矿、资源增长等
    /// </summary>
    public class EconomySystem : BaseSystem
    {
        public override int GetOrder()
        {
            // 经济系统应该在生产系统之后执行
            return (int)SystemOrder.Produce + 1;
        }

        public override void Update()
        {
            ProcessMining();
            ProcessPower(); // 添加电力处理
        }

        /// <summary>
        /// 处理采矿逻辑
        /// </summary>
        private void ProcessMining()
        {
            // 获取所有具有采矿组件的实体
            var miningEntities = ComponentManager.GetAllEntityIdsWith<MiningComponent>();
            
            foreach (var entityId in miningEntities)
            {
                var miningEntity = new Entity(entityId);
                var miningComponent = ComponentManager.GetComponent<MiningComponent>(miningEntity);
                
                // 检查是否正在采矿
                if (!miningComponent.IsMining)
                    continue;
                
                // 检查建筑是否已完成建造
                if (ComponentManager.HasComponent<BuildingConstructionComponent>(miningEntity))
                {
                    // 建造未完成，不能采矿
                    continue;
                }
                
                // 增加采矿计时器
                miningComponent.MiningTimer++;
                
                // 检查是否到达采矿周期
                if (miningComponent.MiningTimer >= miningComponent.MiningCycleFrames)
                {
                    // 重置计时器
                    miningComponent.MiningTimer = 0;
                    
                    // 获取关联的矿源
                    var mineEntity = new Entity(miningComponent.MineEntityId);
                    if (ComponentManager.HasComponent<MineComponent>(mineEntity))
                    {
                        var mineComponent = ComponentManager.GetComponent<MineComponent>(mineEntity);
                        
                        // 检查矿源是否有足够的资源
                        if (mineComponent.ResourceAmount >= miningComponent.ResourcePerCycle)
                        {
                            // 减少矿源资源
                            mineComponent.ResourceAmount -= miningComponent.ResourcePerCycle;
                            ComponentManager.AddComponent(mineEntity, mineComponent);
                            
                            // 获取采矿场的阵营
                            if (ComponentManager.HasComponent<CampComponent>(miningEntity))
                            {
                                var campComponent = ComponentManager.GetComponent<CampComponent>(miningEntity);
                                int campId = campComponent.CampId;
                                
                                // 增加玩家资金
                                AddResourceToPlayer(campId, miningComponent.ResourcePerCycle);
                            }
                        }
                        else
                        {
                            // 矿源资源不足，停止采矿
                            miningComponent.IsMining = false;
                        }
                        
                        // 更新采矿组件
                        ComponentManager.AddComponent(miningEntity, miningComponent);
                    }
                }
                else
                {
                    // 更新采矿组件的计时器
                    ComponentManager.AddComponent(miningEntity, miningComponent);
                }
            }
        }
        
        /// <summary>
        /// 处理电力系统逻辑
        /// </summary>
        private void ProcessPower()
        {
            // 获取所有阵营
            var campEntities = ComponentManager.GetAllEntityIdsWith<CampComponent>();
            var campIds = new HashSet<int>();
            
            foreach (var entityId in campEntities)
            {
                var entity = new Entity(entityId);
                var campComponent = ComponentManager.GetComponent<CampComponent>(entity);
                campIds.Add(campComponent.CampId);
            }
            
            // 为每个阵营计算电力
            foreach (int campId in campIds)
            {
                UpdateCampPower(campId);
            }
        }
        
        /// <summary>
        /// 更新指定阵营的电力
        /// </summary>
        /// <param name="campId">阵营ID</param>
        private void UpdateCampPower(int campId)
        {
            // 获取该阵营的经济组件
            var (economyComponent, economyEntity) = ComponentManager.GetComponentWithCondition<EconomyComponent>(
                e => ComponentManager.HasComponent<CampComponent>(e) && 
                     ComponentManager.GetComponent<CampComponent>(e).CampId == campId);
            
            if (economyEntity.Id == -1)
            {
                return;
            }
            
            // 计算电力：初始10电 + 电厂数量*10 - 采矿场数量*5 - 坦克工厂数量*5
            int powerPlants = CountBuildingsForCamp(campId, BuildingType.PowerPlant);
            int smelters = CountBuildingsForCamp(campId, BuildingType.Smelter);
            int factories = CountBuildingsForCamp(campId, BuildingType.Factory);
            
            int totalPower = 10 + powerPlants * 10 - smelters * 5 - factories * 5;
            
            // 更新电力值
            economyComponent.Power = totalPower;
            ComponentManager.AddComponent(economyEntity, economyComponent);
        }
        
        /// <summary>
        /// 计算指定阵营的建筑数量
        /// </summary>
        /// <param name="campId">阵营ID</param>
        /// <param name="buildingType">建筑类型</param>
        /// <returns>建筑数量</returns>
        private int CountBuildingsForCamp(int campId, BuildingType buildingType)
        {
            int count = 0;
            var buildingEntities = ComponentManager.GetAllEntityIdsWith<BuildingComponent>();
            
            foreach (var entityId in buildingEntities)
            {
                var entity = new Entity(entityId);
                
                // 检查阵营
                if (!ComponentManager.HasComponent<CampComponent>(entity))
                    continue;
                    
                var campComponent = ComponentManager.GetComponent<CampComponent>(entity);
                if (campComponent.CampId != campId)
                    continue;
                
                // 检查建筑类型
                var buildingComponent = ComponentManager.GetComponent<BuildingComponent>(entity);
                if (buildingComponent.BuildingType != (int)buildingType)
                    continue;
                
                if (buildingComponent.BuildingType == (int)BuildingType.PowerPlant)
                {
                    // 检查建筑是否已完成建造
                    if (ComponentManager.HasComponent<BuildingConstructionComponent>(entity))
                        continue;
                }
                
                count++;
            }
            
            return count;
        }
        
        /// <summary>
        /// 为指定玩家增加资源
        /// </summary>
        /// <param name="campId">玩家ID</param>
        /// <param name="amount">资源数量</param>
        private void AddResourceToPlayer(int campId, int amount)
        {
            // 查找玩家的经济组件
            var (economyComponent, entity) = ComponentManager.GetComponentWithCondition<EconomyComponent>(
                e => ComponentManager.HasComponent<CampComponent>(e) && 
                     ComponentManager.GetComponent<CampComponent>(e).CampId == campId);
            
            if (entity.Id != -1)
            {
                // 找到玩家的经济组件，增加资金
                economyComponent.Money += amount;
                ComponentManager.AddComponent(entity, economyComponent);
            }
        }
    }
}