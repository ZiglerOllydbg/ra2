using System.Collections.Generic;
using zUnity;
using ZLockstep.RVO;
using ZLockstep.Simulation.ECS;
using ZLockstep.Simulation.ECS.Components;

namespace ZLockstep.Flow
{
    /// <summary>
    /// 流场导航系统
    /// 处理所有使用流场寻路的实体
    /// 继承自BaseSystem，集成到现有ECS框架
    /// </summary>
    public class FlowFieldNavigationSystem : BaseSystem
    {
        private FlowFieldManager flowFieldManager;
        private RVO2Simulator rvoSimulator;

        /// <summary>
        /// 初始化流场导航系统
        /// 在系统注册到World后调用
        /// </summary>
        public void InitializeNavigation(FlowFieldManager ffMgr, RVO2Simulator rvoSim)
        {
            flowFieldManager = ffMgr;
            rvoSimulator = rvoSim;
        }

        protected override void OnInitialize()
        {
            // 系统初始化
        }

        /// <summary>
        /// 为实体添加流场导航能力
        /// </summary>
        public void AddNavigator(Entity entity, zfloat radius, zfloat maxSpeed)
        {
            // 获取实体位置
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 pos2D = new zVector2(transform.Position.x, transform.Position.z);

            // 添加导航组件
            var navigator = FlowFieldNavigatorComponent.Create(radius, maxSpeed);
            
            // 在RVO中创建智能体
            int rvoAgentId = rvoSimulator.AddAgent(
                pos2D,
                radius,
                maxSpeed,
                maxNeighbors: 10,
                timeHorizon: new zfloat(2)
            );
            navigator.RvoAgentId = rvoAgentId;

            ComponentManager.AddComponent(entity, navigator);
        }

        /// <summary>
        /// 设置实体的移动目标
        /// </summary>
        public void SetMoveTarget(Entity entity, zVector2 targetPos)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            // 释放旧流场
            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
            }

            // 请求新流场
            navigator.CurrentFlowFieldId = flowFieldManager.RequestFlowField(targetPos);
            navigator.HasReachedTarget = false;

            // 更新导航组件
            ComponentManager.AddComponent(entity, navigator);

            // 添加或更新目标组件
            var target = MoveTargetComponent.Create(targetPos);
            ComponentManager.AddComponent(entity, target);
        }

        /// <summary>
        /// 批量设置多个实体的目标（常见于RTS选中操作）
        /// </summary>
        public void SetMultipleTargets(List<Entity> entities, zVector2 targetPos)
        {
            foreach (var entity in entities)
            {
                SetMoveTarget(entity, targetPos);
            }
        }

        /// <summary>
        /// 清除实体的移动目标
        /// </summary>
        public void ClearMoveTarget(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
                navigator.CurrentFlowFieldId = -1;
                navigator.HasReachedTarget = false;

                ComponentManager.AddComponent(entity, navigator);
            }

            // 移除目标组件
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);

            // 停止RVO智能体
            if (navigator.RvoAgentId >= 0)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
            }
        }

        /// <summary>
        /// 系统更新（每帧调用）
        /// </summary>
        public override void Update()
        {
            if (flowFieldManager == null || rvoSimulator == null)
                return;

            // 1. 更新流场管理器（处理脏流场）
            flowFieldManager.Tick();

            // 2. 为每个有导航组件的实体设置期望速度
            var navigatorEntities = ComponentManager.GetAllEntityIdsWith<FlowFieldNavigatorComponent>();
            foreach (var entityId in navigatorEntities)
            {
                Entity entity = new Entity(entityId);
                UpdateEntityNavigation(entity);
            }

            // 3. RVO统一计算避障
            rvoSimulator.DoStep(DeltaTime);

            // 4. 同步位置回Transform组件
            foreach (var entityId in navigatorEntities)
            {
                Entity entity = new Entity(entityId);
                SyncPosition(entity);
            }
        }

        /// <summary>
        /// 更新单个实体的导航
        /// </summary>
        private void UpdateEntityNavigation(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            if (!navigator.IsEnabled)
                return;

            // 没有目标
            if (!ComponentManager.HasComponent<MoveTargetComponent>(entity))
            {
                if (navigator.RvoAgentId >= 0)
                {
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                }
                return;
            }

            if (navigator.CurrentFlowFieldId < 0)
                return;

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            var target = ComponentManager.GetComponent<MoveTargetComponent>(entity);

            zVector2 currentPos = new zVector2(transform.Position.x, transform.Position.z);

            // 检查是否到达目标
            if (flowFieldManager.IsAtTarget(navigator.CurrentFlowFieldId, currentPos, navigator.ArrivalRadius))
            {
                navigator.HasReachedTarget = true;
                ComponentManager.AddComponent(entity, navigator);
                ClearMoveTarget(entity);
                return;
            }

            // 从流场获取方向
            zVector2 flowDirection = flowFieldManager.SampleDirection(navigator.CurrentFlowFieldId, currentPos);

            if (flowDirection == zVector2.zero)
            {
                // 无法到达目标或已经在目标处
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                return;
            }

            // 计算速度（接近目标时减速）
            zfloat speed = navigator.MaxSpeed;
            zfloat distanceToTarget = (target.TargetPosition - currentPos).magnitude;

            if (distanceToTarget < navigator.SlowDownRadius)
            {
                // 线性减速
                speed = navigator.MaxSpeed * distanceToTarget / navigator.SlowDownRadius;
                
                // 最小速度
                zfloat minSpeed = navigator.MaxSpeed * new zfloat(0, 2000); // 20%
                if (speed < minSpeed)
                {
                    speed = minSpeed;
                }
            }

            // 设置期望速度（RVO会处理避障）
            zVector2 desiredVelocity = flowDirection * speed;
            rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, desiredVelocity);
        }

        /// <summary>
        /// 同步RVO位置到Transform组件
        /// </summary>
        private void SyncPosition(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            if (navigator.RvoAgentId < 0)
                return;

            zVector2 pos2D = rvoSimulator.GetAgentPosition(navigator.RvoAgentId);
            
            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            transform.Position.x = pos2D.x;
            transform.Position.z = pos2D.y;
            
            ComponentManager.AddComponent(entity, transform);

            // 同步速度到VelocityComponent（如果有）
            if (ComponentManager.HasComponent<VelocityComponent>(entity))
            {
                zVector2 vel2D = rvoSimulator.GetAgentVelocity(navigator.RvoAgentId);
                var velocity = new VelocityComponent(new zVector3(vel2D.x, zfloat.Zero, vel2D.y));
                ComponentManager.AddComponent(entity, velocity);
            }
        }

        /// <summary>
        /// 移除实体的导航能力
        /// </summary>
        public void RemoveNavigator(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);

            // 释放流场
            if (navigator.CurrentFlowFieldId >= 0)
            {
                flowFieldManager.ReleaseFlowField(navigator.CurrentFlowFieldId);
            }

            // 移除组件
            ComponentManager.RemoveComponent<FlowFieldNavigatorComponent>(entity);
            ComponentManager.RemoveComponent<MoveTargetComponent>(entity);
        }
    }
}
