<!-- c7c37444-6aa9-4cce-9898-305123e43051 106695d2-72db-4d02-afd8-f2f057dcef54 -->
# 修复RVO2核心Bug

## 目标

修复`Packages/ZLockstep/Runtime/RVO2/RVO2.cs`中的两个关键bug，提升多智能体避障的公平性和稳定性。

## Bug 1: 速度更新不对称（第116-137行）

### 问题

`DoStep`方法中，循环计算新速度时直接修改`agent.velocity`，导致后面的智能体计算时看到的是部分更新后的状态，破坏了ORCA算法的对称性。

### 修复方案

1. 在`RVO2Agent`类中添加临时字段`newVelocity`用于存储计算结果
2. 修改`ComputeNewVelocity`方法，将结果存到`agent.newVelocity`而非直接修改`agent.velocity`
3. 在`DoStep`中分两个阶段：

   - 阶段1：计算所有智能体的新速度（存到newVelocity）
   - 阶段2：统一更新velocity和position

### 涉及代码

- `RVO2Agent`类（第367-392行）：添加`newVelocity`字段
- `ComputeNewVelocity`方法（第142-228行）：修改第227行
- `DoStep`方法（第116-137行）：重构为两阶段更新

## Bug 2: 数组越界风险（第292-301行）

### 问题

`LinearProgram2`方法中，当`i = 0`时访问`lines[i - 1]`可能导致数组越界。虽然理论上只在`feasible = false`时进入（需要i > 0），但缺少显式检查。

### 修复方案

在第294行访问`lines[i - 1]`之前，添加`i > 0`的检查条件：

```csharp
if (i > 0)
{
    zfloat determinant = Det(lines[i].direction, lines[i - 1].direction);
    // ... 现有逻辑
}
```

### 涉及代码

- `LinearProgram2`方法的else分支（第291-307行）

## 验证

修复后，多智能体密集避障应该更对称、更稳定，不会因为智能体ID顺序而产生不公平的避障表现。

### To-dos

- [ ] 在RVO2Agent类中添加newVelocity字段
- [ ] 重构DoStep方法为两阶段更新（计算新速度 -> 统一应用）
- [ ] 修改ComputeNewVelocity将结果存到newVelocity而非直接修改velocity
- [ ] 在LinearProgram2的数组访问前添加边界检查