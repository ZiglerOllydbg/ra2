<!-- dc94aee6-4b08-4dfe-b78e-015dcfdf1a04 073b05dd-b8f6-4ae1-9725-bb9d15b71bbd -->
# 坦克旋转系统实现计划

## 一、设计概述

实现一个独立的旋转系统，支持：

- 坦克原地转向（角度差>30度时停止移动）
- 行进转向（角度差<30度时边移动边转）
- 不同单位类型的差异化旋转速度（轻型/重型坦克）
- 炮塔独立旋转（战斗时朝向目标）
- 与现有FlowFieldNavigationSystem和MovementSystem协同工作

## 二、核心设计思路

### 系统职责分离

- **FlowFieldNavigationSystem**: 流场导航、RVO避障、位置更新（检查旋转状态决定是否移动）
- **MovementSystem**: 简单移动、位置更新（检查旋转状态决定是否移动）
- **RotationSystem**: 车体旋转、炮塔旋转、原地转向判断（设置旋转状态标志）
- **CombatSystem**: 战斗逻辑、设置炮塔期望方向

### 协作机制

通过 `RotationStateComponent.IsInPlaceRotating` 标志位实现系统间协调：

- RotationSystem判断是否需要原地转向，设置标志位
- 移动相关系统检查标志位，决定是否更新位置

### 执行顺序

```
1. FlowFieldNavigationSystem  (设置期望方向，检查是否被阻止移动)
2. MovementSystem             (设置期望方向，检查是否被阻止移动)
3. AutoChaseSystem
4. CombatSystem              (设置炮塔期望方向)
5. RotationSystem            (处理旋转，设置下一帧的阻止标志)
6. ProjectileSystem
7. DeathRemovalSystem
```

## 三、实现步骤

### 步骤1: 创建旋转相关组件

**文件**: `Packages/ZLockstep/Runtime/Simulation/ECS/Components/VehicleTypeComponent.cs`

- 定义载具类型枚举（步兵、轻型载具、重型坦克、建筑）
- 车体旋转角速度
- 原地转向阈值角度
- 是否有炮塔
- 炮塔旋转角速度

**文件**: `Packages/ZLockstep/Runtime/Simulation/ECS/Components/RotationStateComponent.cs`

- 期望朝向方向（zVector2）
- 当前朝向方向（zVector2）
- 是否正在原地转向（bool）
- 是否启用旋转控制（bool）

**文件**: `Packages/ZLockstep/Runtime/Simulation/ECS/Components/TurretComponent.cs`

- 炮塔当前旋转角度（相对车体）
- 炮塔期望旋转角度
- 炮塔最大旋转角度限制（可选）

### 步骤2: 创建RotationSystem

**文件**: `Packages/ZLockstep/Runtime/Simulation/ECS/Systems/RotationSystem.cs`

核心逻辑：

1. 遍历所有有 `RotationStateComponent` 的实体
2. 计算当前朝向与期望朝向的角度差
3. 根据 `VehicleTypeComponent` 判断是否需要原地转向：

   - 角度差 > 阈值 → 设置 `IsInPlaceRotating = true`
   - 角度差 <= 阈值 → 设置 `IsInPlaceRotating = false`

4. 根据角速度平滑旋转车体
5. 如果有炮塔，单独处理炮塔旋转

关键方法：

- `UpdateEntityRotation(Entity)`: 处理单个实体的旋转
- `CalculateAngleDifference(zVector2, zVector2)`: 计算角度差
- `RotateTowards(zQuaternion, zVector2, zfloat)`: 平滑旋转到目标方向
- `UpdateTurretRotation(Entity)`: 更新炮塔旋转

### 步骤3: 修改FlowFieldNavigationSystem

**文件**: `Packages/ZLockstep/Runtime/Flow/FlowFieldSystem.cs`

修改位置：

- `UpdateEntityNavigation()` 方法（约行395-586）
  - 计算流场方向后，设置到 `RotationStateComponent.DesiredDirection`
  - 检查 `RotationStateComponent.IsInPlaceRotating`
  - 如果为true，不设置RVO速度（停止移动）

- `SyncPosition()` 方法（约行671-727）
  - 添加对 `IsInPlaceRotating` 的检查
  - 原地转向时冻结位置更新

### 步骤4: 修改MovementSystem

**文件**: `Packages/ZLockstep/Runtime/Simulation/ECS/Systems/MovementSystem.cs`

修改位置：

- `ProcessMoveCommands()` 方法（约行26-79）
  - 移除直接设置旋转的代码（行64-69）
  - 改为设置 `RotationStateComponent.DesiredDirection`
  - 检查 `IsInPlaceRotating`，如果为true则不更新速度

- `ApplyVelocity()` 方法（约行84-104）
  - 检查 `IsInPlaceRotating`
  - 原地转向时不更新位置

### 步骤5: 修改CombatSystem（可选，炮塔旋转）

**文件**: `Packages/ZLockstep/Runtime/Simulation/ECS/Systems/CombatSystem.cs`

修改位置：

- `ProcessAttacks()` 方法（约行26-103）
  - 找到目标后，如果单位有 `TurretComponent`
  - 计算炮塔朝向目标的方向
  - 设置到 `TurretComponent.DesiredTurretAngle`

### 步骤6: 更新单位创建逻辑

**文件**: `Assets/Scripts/Examples/BattleGame.cs`

修改位置：

- `CreateUnitEntity()` 方法（约行305-396）
  - 根据单位类型添加 `VehicleTypeComponent`
  - 添加 `RotationStateComponent`
  - 坦克单位添加 `TurretComponent`

示例配置：

```csharp
// 轻型坦克
VehicleType = LightVehicle
BodyRotationSpeed = 180度/秒
InPlaceRotationThreshold = 60度
HasTurret = true
TurretRotationSpeed = 240度/秒

// 重型坦克
VehicleType = HeavyTank
BodyRotationSpeed = 120度/秒
InPlaceRotationThreshold = 30度
HasTurret = true
TurretRotationSpeed = 180度/秒

// 步兵
VehicleType = Infantry
BodyRotationSpeed = 360度/秒
InPlaceRotationThreshold = 180度（基本不原地转向）
HasTurret = false
```

### 步骤7: 注册RotationSystem

**文件**: `Assets/Scripts/Examples/BattleGame.cs`

修改位置：

- `RegisterSystems()` 方法（约行116-145）
- 在 `CombatSystem` 之后、`ProjectileSystem` 之前注册 `RotationSystem`
```csharp
// 注册战斗系统
World.SystemManager.RegisterSystem(new CombatSystem());

// 注册旋转系统（在移动和战斗之后）
World.SystemManager.RegisterSystem(new RotationSystem());

World.SystemManager.RegisterSystem(new ProjectileSystem());
```


## 四、关键实现细节

### 角度计算

使用 zVector2 计算角度差，注意确定性数学：

```csharp
zfloat angle = zMath.Atan2(direction.y, direction.x) * zfloat.Rad2Deg;
```

### 平滑旋转

使用 zQuaternion.RotateTowards 或自定义插值：

```csharp
zfloat maxRotation = angularSpeed * deltaTime;
newRotation = zQuaternion.RotateTowards(currentRotation, targetRotation, maxRotation);
```

### 炮塔旋转范围限制

可选功能，限制炮塔相对车体的旋转角度（如±180度）

## 五、测试验证

1. 创建重型坦克，命令大角度转向，验证是否停止移动
2. 创建轻型坦克，命令小角度转向，验证是否边移动边转
3. 坦克遇到敌人，验证车体移动+炮塔转向
4. 步兵单位不受原地转向限制

## 六、后续优化（可选）

- 转向动画事件（通知表现层播放转向音效）
- 炮塔射击后座力效果
- 不同地形对旋转速度的影响
- 履带转向效果（左右履带不同速度）

### To-dos

- [ ] 创建VehicleTypeComponent、RotationStateComponent、TurretComponent三个组件文件
- [ ] 创建RotationSystem，实现车体旋转、原地转向判断、炮塔旋转逻辑
- [ ] 修改FlowFieldNavigationSystem，添加原地转向检查和期望方向设置
- [ ] 修改MovementSystem，移除旧旋转代码，添加原地转向检查
- [ ] 修改CombatSystem，添加炮塔朝向目标的逻辑
- [ ] 在BattleGame.CreateUnitEntity中为不同单位类型添加旋转相关组件
- [ ] 在BattleGame.RegisterSystems中注册RotationSystem到正确位置
- [ ] 测试不同单位类型的旋转行为，调优参数（角速度、阈值等）