using ZLockstep.Simulation.ECS.Components;
using zUnity;

namespace ZLockstep.Simulation.ECS.Systems
{
    /// <summary>
    /// 旋转系统
    /// 处理所有单位的车体旋转和炮塔旋转
    /// 负责原地转向判断和平滑旋转插值
    /// </summary>
    public class RotationSystem : BaseSystem
    {
        protected override void OnInitialize()
        {
        }

        public override void Update()
        {
            var rotationEntities = ComponentManager.GetAllEntityIdsWith<RotationStateComponent>();
            foreach (var entityId in rotationEntities)
            {
                Entity entity = new Entity(entityId);
                UpdateEntityRotation(entity);
            }
        }
        private void UpdateEntityRotation(Entity entity)
        {
            var rotationState = ComponentManager.GetComponent<RotationStateComponent>(entity);
            if (!rotationState.IsEnabled) return;

            var transform = ComponentManager.GetComponent<TransformComponent>(entity);
            VehicleTypeComponent vehicleType = ComponentManager.HasComponent<VehicleTypeComponent>(entity)
               ? ComponentManager.GetComponent<VehicleTypeComponent>(entity)
               : VehicleTypeComponent.CreateInfantry();

            // 1. 获取归一化的方向向量
            zVector2 currentDir = GetForward2D(transform.Rotation);
            if (currentDir == zVector2.zero) currentDir = new zVector2(zfloat.Zero, zfloat.One);
            zVector2 targetDir = rotationState.DesiredDirection.normalized;

            // =================================================================
            // 【修改点 1】 使用标准方法计算相对夹角（修复 Atan2(cross, dot) 符号问题）
            // =================================================================
            // 使用标准方法：计算两个向量的角度，然后相减并归一化
            // 这样可以确保总是选择最短路径角度，避免来回摆动
            // 注意：Atan2(cross, dot) 方法在某些情况下符号相反，会导致选择大角度而不是小角度
            zfloat angleDiff = CalculateRelativeAngle(currentDir, targetDir);

            // =================================================================
            // 【修改点 2】 死锁逻辑 (防止在背后 180 度处左右反复横跳)
            // =================================================================
            zfloat maxStep = vehicleType.BodyRotationSpeed * DeltaTime;
            zfloat rawAbsDiff = zMathf.Abs(angleDiff);

            // 滞后阈值判断
            // 注意：当角度差小于 threshold 时，允许单位继续移动，不需要停车旋转
            // 这样可以避免在 DesiredDirection 在相邻帧之间变化时导致的抖动
            zfloat threshold = rotationState.IsInPlaceRotating ? new zfloat(30, 0) : vehicleType.InPlaceRotationThreshold;
            bool isRotatingNow = rawAbsDiff > threshold;

            // 关键修改：移除强制保持旋转状态的逻辑
            // 当角度差小于 threshold 时，IsInPlaceRotating 应该为 false，允许单位继续移动
            // 这样可以避免在 DesiredDirection 变化时导致的抖动问题

            rotationState.IsInPlaceRotating = isRotatingNow;

            // =================================================================
            // 【修改点 2】基于最新状态执行死锁逻辑
            // =================================================================
            if (rotationState.IsInPlaceRotating)
            {
                // A. 初始化锁：如果是刚开始转（锁是0），立刻决定方向并锁死
                if (rotationState.TurnSign == 0 && rawAbsDiff > zfloat.Epsilon)
                {
                    // 关键修复：当角度差正好是180°时，强制选择顺时针方向（-180°）
                    // 这样可以避免一直转圈的问题
                    if (rawAbsDiff >= new zfloat(180, 0) - new zfloat(1, 0)) // 接近180度（>=179°）
                    {
                        rotationState.TurnSign = -1; // 强制选择顺时针方向
                        // 将角度差修正为 -180°（顺时针转180度），而不是 +180°（逆时针转180度）
                        angleDiff = -rawAbsDiff;
                        zUDebug.Log($"[RotationSystem] 180度初始化：rawAbsDiff={rawAbsDiff}, TurnSign={rotationState.TurnSign}, angleDiff={angleDiff}");
                    }
                    else
                    {
                        rotationState.TurnSign = angleDiff > zfloat.Zero ? 1 : -1;
                        zUDebug.Log($"[RotationSystem] 普通初始化：rawAbsDiff={rawAbsDiff}, TurnSign={rotationState.TurnSign}, angleDiff={angleDiff}");
                    }
                }

                // B. 执行锁：如果已经有锁了，强制检查一致性
                // 关键修复：死锁逻辑应该在所有角度都生效，而不仅仅是在 >90° 时
                // 这样可以确保旋转方向在整个旋转过程中保持一致
                if (rotationState.TurnSign != 0)
                {
                    int currentSign = angleDiff > zfloat.Zero ? 1 : (angleDiff < zfloat.Zero ? -1 : 0);

                    // 如果当前计算的最短路径方向 与 锁定的方向 不一致
                    if (currentSign != rotationState.TurnSign && currentSign != 0)
                    {
                        // 关键修复：只有当角度差 >= 180° 时才执行强制修正
                        // 如果角度差 < 180°，说明最短路径就是当前计算的方向，不应该强制修正
                        // 此时应该清除 TurnSign，让系统选择最短路径，避免转大角度
                        if (rawAbsDiff >= new zfloat(180, 0) - new zfloat(1, 0)) // 接近或等于180°
                        {
                            // 强制修改 angleDiff，使其符号与 TurnSign 一致
                            // 让 -179 变成 +181，保持旋转方向连贯
                            zfloat oldAngleDiff = angleDiff;
                            if (rotationState.TurnSign > 0)
                            {
                                // TurnSign = +1，需要顺时针转，angleDiff 应该是正数
                                // 如果 angleDiff 是负数（比如 -179°），变成 +181°
                                angleDiff += new zfloat(360, 0);
                            }
                            else
                            {
                                // TurnSign = -1，需要逆时针转，angleDiff 应该是负数
                                // 如果 angleDiff 是正数（比如 +179°），变成 -181°
                                angleDiff -= new zfloat(360, 0);
                            }

                            zUDebug.LogWarning($"[RotationSystem] 死锁修正（180°边界）：rawAbsDiff={rawAbsDiff}, TurnSign={rotationState.TurnSign}, " +
                                              $"currentSign={currentSign}, oldAngleDiff={oldAngleDiff}, newAngleDiff={angleDiff}");
                        }
                        else
                        {
                            // 角度差 < 180°，最短路径就是当前计算的方向，清除 TurnSign，让系统选择最短路径
                            zUDebug.Log($"[RotationSystem] 角度差已减小到 {rawAbsDiff}°（<180°），清除 TurnSign，选择最短路径, newAngleDiff={angleDiff}, currentDir = {currentDir}, targetDir = {targetDir} threshold = {threshold}");
                            rotationState.TurnSign = 0;
                        }
                    }
                }
            }
            else
            {
                // 当角度差小于 threshold 时，清除 TurnSign，允许单位继续移动
                // 这样可以避免在 DesiredDirection 变化时，残留的 TurnSign 影响后续判断
                rotationState.TurnSign = 0;
            }

            // =================================================================
            // 【修改点 3：核心修复】吸附逻辑
            // =================================================================

            // 计算经过死锁修正后的实际偏差绝对值
            zfloat finalAbsDiff = zMathf.Abs(angleDiff);

            // 如果这一帧能够转完剩余的角度（或者本来就很小），直接吸附到位
            if (finalAbsDiff <= maxStep)
            {
                // 1. 直接设置为目标角度
                transform.Rotation = zQuaternion.LookRotation(new zVector3(targetDir.x, zfloat.Zero, targetDir.y));

                // 2. 彻底清除旋转状态，防止下一帧微小误差导致抖动
                rotationState.IsInPlaceRotating = false;
                rotationState.TurnSign = 0;

                // 3. 更新组件并返回 (不再执行后续的插值)
                ComponentManager.AddComponent(entity, transform);

                zUDebug.Log("step 1" + DeltaTime + " finalAbsDiff:" + finalAbsDiff + " rawAbsDiff:" + rawAbsDiff);

                // 如果需要移除组件的逻辑放在这里
                if (rawAbsDiff <= new zfloat(1))
                {
                    // ComponentManager.RemoveComponent<RotationStateComponent>(entity);
                    // 注意：如果移除组件，下面回写rotationState就不要执行了
                }
                else
                {
                    ComponentManager.AddComponent(entity, rotationState);
                }
                return;
            }
            else
            {
                zUDebug.Log("step 2 " + DeltaTime + " finalAbsDiff:" + finalAbsDiff + " rawAbsDiff:" + rawAbsDiff + $", currentDir = {currentDir}, targetDir = {targetDir} ");
            }

            // =================================================================
            // 【修改点 4】标准步进旋转
            // =================================================================
            // 既然 finalAbsDiff > maxStep，说明还没转到位，按最大速度转
            // 注意：这里只需判断符号，因为幅度已经被 maxStep 限制了

            // 只要判断符号即可，因为幅度已经被 maxStep 限制
            zfloat step = (angleDiff > zfloat.Zero) ? maxStep : -maxStep;

            // 这里我们需要基于当前的四元数进行旋转，而不是加角度
            // 这样能避免 CurrentAngle 在 ±180 跳变带来的计算困扰
            zQuaternion stepRot = QuaternionFromAngle(step);
            transform.Rotation = transform.Rotation * stepRot; // 叠加旋转

            ComponentManager.AddComponent(entity, transform);

            // 回写状态
            rotationState.LastRotationAngle = rawAbsDiff;
            ComponentManager.AddComponent(entity, rotationState);

            // 炮塔逻辑 (保持不变，省略)
            if (vehicleType.HasTurret && ComponentManager.HasComponent<TurretComponent>(entity))
                UpdateTurretRotation(entity, vehicleType, transform);
        }
        /// <summary>
        /// 更新炮塔旋转
        /// </summary>
        private void UpdateTurretRotation(Entity entity, VehicleTypeComponent vehicleType, TransformComponent transform)
        {
            var turret = ComponentManager.GetComponent<TurretComponent>(entity);

            // 如果没有目标，炮塔恢复到与车体一致
            if (!turret.HasTarget)
            {
                // 平滑回到0度
                if (turret.CurrentTurretAngle != zfloat.Zero)
                {
                    zfloat maxTurretRotation = vehicleType.TurretRotationSpeed * DeltaTime;
                    zfloat angleToZero = zMathf.Abs(turret.CurrentTurretAngle);

                    if (angleToZero <= maxTurretRotation)
                    {
                        turret.CurrentTurretAngle = zfloat.Zero;
                    }
                    else
                    {
                        zfloat rotationDir = turret.CurrentTurretAngle > zfloat.Zero ? -zfloat.One : zfloat.One;
                        turret.CurrentTurretAngle += rotationDir * maxTurretRotation;
                    }
                }

                turret.DesiredTurretAngle = zfloat.Zero;
                ComponentManager.AddComponent(entity, turret);
                return;
            }

            // 计算炮塔期望角度（相对车体）
            // 获取车体朝向
            zVector3 bodyForward = transform.Rotation * zVector3.forward;
            zVector2 bodyDirection2D = new zVector2(bodyForward.x, bodyForward.z);
            if (bodyDirection2D.magnitude > zfloat.Epsilon)
            {
                bodyDirection2D = bodyDirection2D.normalized;
            }

            // 计算炮塔期望朝向相对车体的角度
            if (turret.DesiredTurretDirection.magnitude > zfloat.Epsilon)
            {
                zVector2 desiredDir = turret.DesiredTurretDirection.normalized;

                // 计算相对角度
                turret.DesiredTurretAngle = CalculateRelativeAngle(bodyDirection2D, desiredDir);

                // 如果有旋转限制，限制角度
                if (turret.HasRotationLimit)
                {
                    if (turret.DesiredTurretAngle < turret.MinTurretAngle)
                        turret.DesiredTurretAngle = turret.MinTurretAngle;
                    if (turret.DesiredTurretAngle > turret.MaxTurretAngle)
                        turret.DesiredTurretAngle = turret.MaxTurretAngle;
                }
            }

            // 平滑旋转到期望角度
            zfloat angleDiff = turret.DesiredTurretAngle - turret.CurrentTurretAngle;

            // 处理角度跨越±180度的情况
            if (angleDiff > new zfloat(180))
                angleDiff -= new zfloat(360);
            if (angleDiff < new zfloat(-180))
                angleDiff += new zfloat(360);

            zfloat maxRotation = vehicleType.TurretRotationSpeed * DeltaTime;

            if (zMathf.Abs(angleDiff) <= maxRotation)
            {
                turret.CurrentTurretAngle = turret.DesiredTurretAngle;
            }
            else
            {
                zfloat rotationDir = angleDiff > zfloat.Zero ? zfloat.One : -zfloat.One;
                turret.CurrentTurretAngle += rotationDir * maxRotation;
            }

            // 保持炮塔角度在 [-180, 180] 范围内
            NormalizeAngle(ref turret.CurrentTurretAngle);

            ComponentManager.AddComponent(entity, turret);
        }

        private zVector2 GetForward2D(zQuaternion rotation)
        {
            zVector3 f = rotation * zVector3.forward;
            return new zVector2(f.x, f.z).normalized;
        }

        /// <summary>
        /// Atan2 返回的是弧度，转为度数
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private zfloat GetAngle(zVector2 dir)
        {
            return zMathf.Atan2(dir.x, dir.y) * zMathf.Rad2Deg;
        }
        /// <summary>
        /// 从Y轴角度构建四元数
        /// </summary>
        private zQuaternion QuaternionFromAngle(zfloat angle)
        {
            zfloat rad = angle * zMathf.Deg2Rad;
            return zQuaternion.LookRotation(new zVector3(zMathf.Sin(rad), zfloat.Zero, zMathf.Cos(rad)));
        }

        /// <summary>
        /// 从向量计算角度 (度数, 0度为Z轴正方向，顺时针或逆时针取决于 Atan2 定义)
        /// 统一所有角度计算源头
        /// </summary>
        private zfloat GetAngleFromVector(zVector2 dir)
        {
            if (dir.magnitude < zfloat.Epsilon) return zfloat.Zero;
            // 使用 Atan2(x, z) 对应 Unity 的 (x, z) 平面坐标系
            // 结果通常是弧度，需转为度数
            return zMathf.Atan2(dir.x, dir.y) * zMathf.Rad2Deg;
        }
        /// <summary>
        /// 计算相对角度（from到to的有符号角度，度数）
        /// 正值表示顺时针，负值表示逆时针
        /// </summary>
        private zfloat CalculateRelativeAngle(zVector2 from, zVector2 to)
        {
            zfloat fromAngle = GetAngleFromVector(from);
            zfloat toAngle = GetAngleFromVector(to);
            zfloat angle = toAngle - fromAngle;
            NormalizeAngle(ref angle);
            return angle;
        }
        /// <summary>
        /// 将角度归一化到 [-180, 180] 范围
        /// </summary>
        private void NormalizeAngle(ref zfloat angle)
        {
            while (angle > new zfloat(180)) angle -= new zfloat(360);
            while (angle < new zfloat(-180)) angle += new zfloat(360);
        }

    }
}