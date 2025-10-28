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
        private IFlowFieldMap map;

        /// <summary>
        /// 初始化流场导航系统
        /// 在系统注册到World后调用
        /// </summary>
        public void InitializeNavigation(FlowFieldManager ffMgr, RVO2Simulator rvoSim, IFlowFieldMap gameMap)
        {
            flowFieldManager = ffMgr;
            rvoSimulator = rvoSim;
            map = gameMap;
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
            // 优化的RVO参数，减少卡死在角落的情况
            int rvoAgentId = rvoSimulator.AddAgent(
                pos2D,
                radius,
                maxSpeed,
                maxNeighbors: 10,
                timeHorizon: new zfloat(1, 5000)  // 降低到1.5，减少过早反应
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

            // ===== 卡住检测 =====
            zfloat moveDistance = (currentPos - navigator.LastPosition).magnitude;
            zfloat minMoveThreshold = new zfloat(0, 100); // 0.01单位

            if (moveDistance < minMoveThreshold)
            {
                navigator.StuckFrames++;
            }
            else
            {
                navigator.StuckFrames = 0;
            }
            navigator.LastPosition = currentPos;

            // 从流场获取方向
            zVector2 flowDirection = flowFieldManager.SampleDirection(navigator.CurrentFlowFieldId, currentPos);

            // 当流场方向为零时（通常是在目标格子内），直接朝目标点移动
            if (flowDirection == zVector2.zero)
            {
                zVector2 toTarget = target.TargetPosition - currentPos;
                zfloat distSq = toTarget.sqrMagnitude;
                
                // 如果距离很近（小于到达半径的平方），停止移动
                zfloat arrivalRadiusSq = navigator.ArrivalRadius * navigator.ArrivalRadius;
                if (distSq <= arrivalRadiusSq)
                {
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                    ComponentManager.AddComponent(entity, navigator);
                    return;
                }
                
                // 否则，直接朝目标移动（用于目标格子内的精确移动）
                flowDirection = toTarget.normalized;
            }

            // ===== 卡住处理：添加随机扰动 =====
            if (navigator.StuckFrames > 20) // 卡住超过20帧（约0.67秒）
            {
                // 添加垂直于当前方向的随机扰动
                zfloat randomValue = new zfloat((entity.Id * 137 + World.Tick * 17) % 1000 - 500, 1000); // -0.5 到 0.5
                zVector2 perpendicular = new zVector2(-flowDirection.y, flowDirection.x);
                flowDirection = (flowDirection + perpendicular * randomValue * new zfloat(0, 5000)).normalized; // 混入50%扰动
                
                // 每60帧重置一次计数，避免持续添加扰动
                if (navigator.StuckFrames > 60)
                {
                    navigator.StuckFrames = 0;
                }
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

            // ===== 再次检查是否到达（更精确的判定） =====
            // 在应用速度前检查，避免滑动碰撞让单位在目标附近一直移动
            if (distanceToTarget <= navigator.ArrivalRadius)
            {
                // 已经到达目标，停止移动
                navigator.HasReachedTarget = true;
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                ComponentManager.AddComponent(entity, navigator);
                ClearMoveTarget(entity);
                return;
            }

            // ===== 提前预测碰撞 =====
            zVector2 desiredVelocity = flowDirection * speed;
            
            // 预测下一帧位置
            zVector2 predictedPos = currentPos + desiredVelocity * DeltaTime;
            map.WorldToGrid(predictedPos, out int predGridX, out int predGridY);
            
            // 如果预测位置是障碍物，调整方向
            if (!map.IsWalkable(predGridX, predGridY))
            {
                // 尝试滑动：投影到切线方向
                desiredVelocity = GetSlideVelocity(currentPos, desiredVelocity, map);
            }
            
            // 设置期望速度（RVO会处理避障）
            rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, desiredVelocity);

            // 保存更新后的导航组件
            ComponentManager.AddComponent(entity, navigator);
        }

        /// <summary>
        /// 计算滑动速度（沿墙滑行）
        /// 当预测到碰撞时，将速度投影到墙壁的切线方向
        /// </summary>
        private zVector2 GetSlideVelocity(zVector2 currentPos, zVector2 desiredVelocity, IFlowFieldMap map)
        {
            // 检测碰撞法线方向
            zVector2 wallNormal = DetectWallNormal(currentPos, desiredVelocity, map);
            
            if (wallNormal == zVector2.zero)
            {
                // 没有检测到明确的墙壁方向，保持原速度
                return desiredVelocity;
            }
            
            // 将速度投影到墙壁切线（垂直于法线）
            // 切线 = 旋转法线90度
            zVector2 wallTangent = new zVector2(-wallNormal.y, wallNormal.x);
            
            // 投影：保留沿切线的分量
            zfloat projection = zVector2.Dot(desiredVelocity, wallTangent);
            zVector2 slideVelocity = wallTangent * projection;
            
            // 保持原速度的大小（只改方向）
            zfloat originalSpeed = desiredVelocity.magnitude;
            if (slideVelocity.magnitude > zfloat.Epsilon)
            {
                return slideVelocity.normalized * originalSpeed;
            }
            
            // 如果投影为零，尝试反方向
            return -desiredVelocity * new zfloat(0, 5000); // 50%速度后退
        }

        /// <summary>
        /// 检测墙壁法线方向
        /// 通过检查周围格子，找出障碍物的方向
        /// </summary>
        private zVector2 DetectWallNormal(zVector2 currentPos, zVector2 moveDir, IFlowFieldMap map)
        {
            zVector2 checkPos = currentPos + moveDir.normalized * map.GetGridSize();
            map.WorldToGrid(checkPos, out int gx, out int gy);
            
            if (!map.IsWalkable(gx, gy))
            {
                // 最简单的法线：从当前位置指向碰撞点的反方向
                zVector2 toObstacle = map.GridToWorld(gx, gy) - currentPos;
                return -toObstacle.normalized;
            }
            
            // 检查8个方向，找到障碍物的平均方向
            int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] DY = { 1, 1, 0, -1, -1, -1, 0, 1 };
            
            zVector2 avgNormal = zVector2.zero;
            int obstacleCount = 0;
            
            map.WorldToGrid(currentPos, out int cx, out int cy);
            for (int i = 0; i < 8; i++)
            {
                int nx = cx + DX[i];
                int ny = cy + DY[i];
                
                if (!map.IsWalkable(nx, ny))
                {
                    // 从当前格子指向障碍物格子的方向
                    zVector2 toObstacle = new zVector2((zfloat)DX[i], (zfloat)DY[i]);
                    avgNormal -= toObstacle; // 法线是反方向
                    obstacleCount++;
                }
            }
            
            if (obstacleCount > 0)
            {
                return avgNormal.normalized;
            }
            
            return zVector2.zero;
        }

        /// <summary>
        /// 同步RVO位置到Transform组件
        /// </summary>
        private void SyncPosition(Entity entity)
        {
            var navigator = ComponentManager.GetComponent<FlowFieldNavigatorComponent>(entity);
            if (navigator.RvoAgentId < 0)
                return;

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            zVector2 oldPos = new zVector2(transform.Position.x, transform.Position.z);
            zVector2 newPos = rvoSimulator.GetAgentPosition(navigator.RvoAgentId);
            
            // ===== 检查是否已到达目标 =====
            // 如果已经标记为到达，确保速度为零
            if (navigator.HasReachedTarget)
            {
                rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                // 位置不再更新，停留在当前位置
                return;
            }
            
            // ===== 碰撞检测：检查新位置是否在障碍物里 =====
            if (map != null)
            {
                map.WorldToGrid(newPos, out int gridX, out int gridY);
                
                // 如果新位置不可行走，回退到旧位置
                if (!map.IsWalkable(gridX, gridY))
                {
                    // 回退到旧位置
                    newPos = oldPos;
                    rvoSimulator.SetAgentPosition(navigator.RvoAgentId, oldPos);
                    
                    // 停止速度，避免继续尝试进入障碍物
                    rvoSimulator.SetAgentPrefVelocity(navigator.RvoAgentId, zVector2.zero);
                }
            }
            
            transform.Position.x = newPos.x;
            transform.Position.z = newPos.y;
            
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
