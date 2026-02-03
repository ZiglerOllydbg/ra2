using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;
using System.Text;
using ZLockstep.Simulation.Events;
using ZLockstep.Simulation.ECS.Utils;

namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 创建建筑命令
    /// 用于在地图上建造静态建筑（基地、防御塔等）
    /// </summary>
    [CommandType(CommandTypes.BuildStructure)]
    public class CreateBuildingCommand : BaseCommand
    {
        /// <summary>
        /// 配置表ID
        /// </summary>
        public int ConfBuildingID { get; set; }

        /// <summary>
        /// 建筑类型
        /// 0=基地, 1=防御塔等
        /// </summary>
        public BuildingType BuildingType { get; set; }

        /// <summary>
        /// 建筑位置（世界坐标）
        /// </summary>
        public zVector3 Position { get; set; }


        public CreateBuildingCommand(int confID, int campId, zVector3 position)
            : base(campId)
        {
            ConfBuildingID = confID;
            var confBuilding = ConfigManager.Get<ConfBuilding>(ConfBuildingID);
            BuildingType = confBuilding != null? (BuildingType)confBuilding.Type : BuildingType.None;
            zUDebug.Log($"[EntityCreationManager] 创建建筑类型{BuildingType} " +
                $"在位置{Position}");
            Position = position;
        }

        public override void Execute(zWorld world)
        {
            // 如果是非采矿场建筑，需要判断在主城的限制区域内
            if (BuildingType != BuildingType.Smelter)
            {
                if (!BuildingPlacementUtils.CheckBuildableArea(world, Position, CampId))
                {
                    UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 建造建筑类型{BuildingType} 在位置{Position} 失败：超出主城建造范围");
                    world.EventManager.Publish(new MessageEvent($"建造失败：超出主城建造范围"));
                    return;
                }
            }
            // 如果是采矿场，需要判断是否在矿源附近
            else
            {
                if (!CheckMineProximity(world, Position))
                {
                    UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 建造采矿场 在位置{Position} 失败：不在矿源附近");
                    world.EventManager.Publish(new MessageEvent($"建造采矿场失败：不在矿源附近"));
                    return;
                }
            }
            
            // 阻挡判断
            var mapManager = world.GameInstance.GetMapManager();
            if (!BuildingPlacementUtils.CheckBuildingPlacement(ConfBuildingID, Position, mapManager))
            {
                UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 建造建筑类型{BuildingType} 在位置{Position} 失败：位置被阻挡或超出边界");
                world.EventManager.Publish(new MessageEvent("无法建造建筑，请检查资源是否充足"));
                return;
            }

            // 检查并扣除资源
            if (!CheckAndDeductResources(world))
            {
                UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 资源不足，无法建造建筑类型{BuildingType}");
                return;
            }

            // 使用EntityCreationManager创建建筑实体
            var unitCreatedEvent = EntityCreationManager.CreateBuildingEntity(
                world, 
                CampId, 
                Position, 
                ConfBuildingID,
                world.GameInstance.GetMapManager(),
                world.GameInstance.GetFlowFieldManager()
            );

            // 发布事件通知表现层创建视图
            if (unitCreatedEvent.HasValue)
            {
                world.EventManager.Publish(unitCreatedEvent.Value);
            }

            UnityEngine.Debug.Log($"[CreateBuildingCommand] 阵营{CampId} 建造了建筑类型{BuildingType} " +
                $"在位置{Position}");
        }

        /// <summary>
        /// 检查采矿场是否在矿源附近（10格范围内）
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <param name="position">采矿场位置</param>
        /// <returns>是否在矿源附近</returns>
        private bool CheckMineProximity(zWorld world, zVector3 position)
        {
            // 获取所有矿源实体
            var mineEntities = world.ComponentManager.GetAllEntityIdsWith<MineComponent>();
            
            foreach (var entityId in mineEntities)
            {
                var entity = new Entity(entityId);
                
                // 检查实体是否具有Transform组件
                if (world.ComponentManager.HasComponent<TransformComponent>(entity))
                {
                    var transformComponent = world.ComponentManager.GetComponent<TransformComponent>(entity);
                    zVector3 minePosition = transformComponent.Position;
                    
                    // 计算与矿源的距离
                    zVector3 delta = position - minePosition;
                    zfloat distanceSquared = delta.x * delta.x + delta.z * delta.z; // 只考虑水平距离
                    
                    // 检查是否在10格范围内（距离小于等于10）
                    zfloat maxDistance = zfloat.CreateFloat(10 * zfloat.SCALE_10000);
                    if (distanceSquared <= maxDistance * maxDistance)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// 检查并扣除建造建筑所需的资源
        /// </summary>
        /// <param name="world">游戏世界实例</param>
        /// <returns>是否有足够的资源</returns>
        private bool CheckAndDeductResources(zWorld world)
        {
            // 获取玩家的经济组件
            var (economyComponent, economyEntity) = world.ComponentManager.GetComponentWithCondition<EconomyComponent>(
                e => world.ComponentManager.HasComponent<CampComponent>(e) && 
                world.ComponentManager.GetComponent<CampComponent>(e).CampId == CampId);
            
            if (economyEntity.Id == -1)
            {
                UnityEngine.Debug.LogWarning($"[CreateBuildingCommand] 未找到阵营 {CampId} 的经济组件");
                return false;
            }

            var confBuilding = ConfigManager.Get<ConfBuilding>(ConfBuildingID);
            if (confBuilding == null)
            {
                zUDebug.LogError($"[CreateBuildingCommand] 未找到建筑配置信息 {ConfBuildingID}");
                return false;
            }
            
            // 根据建筑类型确定所需资源
            int costMoney = confBuilding.CostMoney;
            int costPower = confBuilding.CostPower;
            
            // 检查是否有足够的资源
            if (economyComponent.Money < costMoney)
            {
                UnityEngine.Debug.Log($"[CreateBuildingCommand] 资金不足。需要: {costMoney}, 当前: {economyComponent.Money}");

                world.EventManager.Publish(new MessageEvent
                {
                    Message = "资金不足！"
                });
                return false;
            }

            if (economyComponent.Power < costPower)
            {
                UnityEngine.Debug.Log($"[CreateBuildingCommand] 电力不足。需要: {costPower}, 当前: {economyComponent.Power}");
                world.EventManager.Publish(new MessageEvent
                {
                    Message = "电力不足！"
                });
                return false;
            }
            
            // 扣除资源
            int oldMoney = economyComponent.Money;
            economyComponent.Money -= costMoney;
            int newMoney = economyComponent.Money;
            
            // 触发资金变化事件
            world.EventManager.Publish(new MoneyChangedEvent
            {
                CampId = CampId,
                OldMoney = oldMoney,
                NewMoney = newMoney,
                Reason = "建造建筑消耗资金"
            });
            
            if (costPower > 0)
            {
                int oldPower = economyComponent.Power;
                economyComponent.Power -= costPower; // 扣除电力供应
                int newPower = economyComponent.Power;
                
                // 触发电力变化事件
                world.EventManager.Publish(new PowerChangedEvent
                {
                    CampId = CampId,
                    OldPower = oldPower,
                    NewPower = newPower,
                    Reason = "建造建筑消耗电力"
                });
                
                zUDebug.Log($"[CreateBuildingCommand] 扣除电力成功。消耗电力: {costPower}。剩余电力: {economyComponent.Power}");
            }
            
            // 更新经济组件
            world.ComponentManager.AddComponent(economyEntity, economyComponent);
            
            UnityEngine.Debug.Log($"[CreateBuildingCommand] 扣除资源成功。花费资金: {costMoney}, 获得电力: {costPower}。剩余资金: {economyComponent.Money}, 总电力: {economyComponent.Power}");
            return true;
        }

        public override string ToString()
        {
            return $"[CreateBuildingCommand] 阵营{CampId} 建造建筑类型{BuildingType} " +
                $"在位置{Position}（格子坐标）";
        }
    }
}