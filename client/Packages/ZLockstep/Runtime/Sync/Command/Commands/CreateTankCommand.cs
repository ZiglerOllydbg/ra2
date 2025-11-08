using zUnity;
using ZLockstep.Simulation;
using ZLockstep.Simulation.Events;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Flow;


namespace ZLockstep.Sync.Command.Commands
{
    /// <summary>
    /// 创建坦克命令
    /// </summary>
    [CommandType(CommandTypes.ProduceUnit)]
    public class CreateTankCommand : BaseCommand
    {

        /// <summary>
        /// 单位类型ID
        /// 1=动员兵, 2=犀牛坦克, 3=矿车
        /// </summary>
        public int UnitType { get; set; }

        /// <summary>
        /// 创建位置
        /// </summary>
        public zVector3 Position { get; set; }

        /// <summary>
        /// 坦克预制体ID（用于表现层）
        /// </summary>
        public int PrefabId { get; set; }

        /// <summary>
        /// 坦克半径（用于导航）
        /// </summary>
        public zfloat Radius { get; set; }

        /// <summary>
        /// 坦克最大速度
        /// </summary>
        public zfloat MaxSpeed { get; set; }

        public CreateTankCommand(int playerId, int unitType, zVector3 position, int prefabId, zfloat radius, zfloat maxSpeed)
            : base(playerId)
        {
            UnitType = unitType;
            Position = position;
            PrefabId = prefabId;
            Radius = radius;
            MaxSpeed = maxSpeed;
        }

        public override void Execute(zWorld world)
        {
            // 1. 创建单位实体
            var entity = world.EntityManager.CreateEntity();

            // 2. 添加Transform组件
            world.ComponentManager.AddComponent(entity, new TransformComponent
            {
                Position = Position,
                Rotation = zQuaternion.identity,
                Scale = zVector3.one * Radius
            });

            // 3. 添加阵营组件
            var camp = CampComponent.Create(PlayerId);
            world.ComponentManager.AddComponent(entity, camp);

            // 4. 添加单位组件
            var unit = new UnitComponent
            {
                UnitType = UnitType,
                PlayerId = PlayerId,
                PrefabId = PrefabId,
                MoveSpeed = MaxSpeed
            };
            world.ComponentManager.AddComponent(entity, unit);

            // 5. 添加生命值组件
            var health = new HealthComponent
            {
                MaxHealth = UnitType == 1 ? (zfloat)50 : UnitType == 2 ? (zfloat)100 : (zfloat)150,
                CurrentHealth = UnitType == 1 ? (zfloat)50 : UnitType == 2 ? (zfloat)100 : (zfloat)150
            };
            world.ComponentManager.AddComponent(entity, health);

            // 6. 如果单位有攻击能力，添加攻击组件
            if (UnitType == 1 || UnitType == 2) // 动员兵和坦克有攻击能力
            {
                var attack = new AttackComponent
                {
                    Damage = UnitType == 1 ? (zfloat)10 : (zfloat)30,
                    Range = UnitType == 1 ? (zfloat)4 : (zfloat)10,
                    AttackInterval = (zfloat)1,
                    TimeSinceLastAttack = zfloat.Zero,
                    TargetEntityId = -1
                };
                world.ComponentManager.AddComponent(entity, attack);
            }

            // 7. 添加速度组件
            world.ComponentManager.AddComponent(entity, new VelocityComponent(zVector3.zero));

            var navSystem = world.GameInstance.GetNavSystem();
            // 8. 添加导航能力
            navSystem.AddNavigator(entity, Radius, MaxSpeed);

            // 9. 添加旋转相关组件
            // 根据单位类型添加不同的载具类型组件
            VehicleTypeComponent vehicleType;
            if (UnitType == 1) // 动员兵
            {
                vehicleType = VehicleTypeComponent.CreateInfantry();
            }
            else if (UnitType == 2) // 坦克
            {
                vehicleType = VehicleTypeComponent.CreateHeavyTank();
            }
            else
            {
                vehicleType = VehicleTypeComponent.CreateInfantry(); // 默认
            }
            world.ComponentManager.AddComponent(entity, vehicleType);

            // 添加旋转状态组件
            var rotationState = RotationStateComponent.Create();
            world.ComponentManager.AddComponent(entity, rotationState);

            // 如果有炮塔，添加炮塔组件
            if (vehicleType.HasTurret)
            {
                var turret = TurretComponent.CreateDefault();
                world.ComponentManager.AddComponent(entity, turret);
            }

            // 判断是本地玩家，添加本地玩家组件
            if (world.ComponentManager.HasGlobalComponent<GlobalInfoComponent>())
            {
                var globalInfoComponent = world.ComponentManager.GetGlobalComponent<GlobalInfoComponent>();
                if (globalInfoComponent.LocalPlayerCampId == PlayerId)
                {
                    world.ComponentManager.AddComponent(entity, new LocalPlayerComponent());
                }
            }

            // 10. 创建但不发布事件，返回事件对象
            var unitCreatedEvent = new UnitCreatedEvent
            {
                EntityId = entity.Id,
                UnitType = UnitType,
                Position = Position,
                PlayerId = PlayerId,
                PrefabId = PrefabId
            };

            world.EventManager.Publish(unitCreatedEvent);

            UnityEngine.Debug.Log($"[CreateTankCommand] 玩家{PlayerId} 阵营{PlayerId} 创建了坦克在位置{Position}");
        }

        // tostring
        public override string ToString()
        {
            return $"[CreateTankCommand] 玩家{PlayerId} 阵营{PlayerId} 创建了坦克在位置{Position} PrefabId: {PrefabId}";
        }
    }
}