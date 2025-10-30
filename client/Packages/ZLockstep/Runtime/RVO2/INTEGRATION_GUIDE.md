# RVO2 集成指南

## 快速开始

### 1. 约定的输入输出

#### 输入（每帧需要提供）
```csharp
// 智能体配置（添加时设置一次）
- position: zVector2      // 当前位置
- radius: zfloat          // 碰撞半径（建议：实际半径 × 1.1-1.2）
- maxSpeed: zfloat        // 最大速度

// 每帧更新
- prefVelocity: zVector2  // 期望速度（朝向目标的方向 × 期望速度大小）
```

#### 输出（每帧获取）
```csharp
- velocity: zVector2      // 避障后的实际速度
- position: zVector2      // 更新后的位置（已自动计算）
```

### 2. 三步集成

```csharp
// 第一步：初始化（游戏开始时）
RVO2Simulator simulator = new RVO2Simulator();
int agentId = simulator.AddAgent(
    startPosition,           // 起始位置
    new zfloat(0, 5000),    // 半径 0.5
    new zfloat(2)           // 最大速度 2.0
);

// 第二步：每帧更新
void FixedUpdate(zfloat deltaTime)
{
    // 计算期望速度（朝向目标）
    zVector2 currentPos = simulator.GetAgentPosition(agentId);
    zVector2 direction = (targetPosition - currentPos).normalized;
    zVector2 desiredVelocity = direction * desiredSpeed;
    
    // 设置期望速度
    simulator.SetAgentPrefVelocity(agentId, desiredVelocity);
    
    // 执行避障计算
    simulator.DoStep(deltaTime);
    
    // 位置已自动更新，获取新位置
    zVector2 newPosition = simulator.GetAgentPosition(agentId);
    
    // 或者获取速度自己更新位置
    // zVector2 velocity = simulator.GetAgentVelocity(agentId);
    // myPosition += velocity * deltaTime;
}

// 第三步：清理（可选）
simulator.Clear();
```

### 3. 完整示例：RTS单位移动

```csharp
public class RTSUnit
{
    private int rvoAgentId;
    private RVO2Simulator rvoSimulator;
    private zVector2 targetPosition;
    private zfloat moveSpeed = new zfloat(3); // 3.0 单位/秒
    
    public void Initialize(RVO2Simulator sim, zVector2 startPos)
    {
        rvoSimulator = sim;
        rvoAgentId = sim.AddAgent(
            startPos,
            new zfloat(0, 5000),  // 半径 0.5
            moveSpeed,
            10,                    // 最多考虑10个邻居
            new zfloat(2)         // 2秒避障时间
        );
    }
    
    public void SetDestination(zVector2 destination)
    {
        targetPosition = destination;
    }
    
    public void Update(zfloat deltaTime)
    {
        zVector2 currentPos = rvoSimulator.GetAgentPosition(rvoAgentId);
        zVector2 toTarget = targetPosition - currentPos;
        zfloat distance = toTarget.magnitude;
        
        if (distance > new zfloat(0, 1000)) // 0.1
        {
            // 计算期望速度
            zVector2 direction = toTarget / distance;
            
            // 接近目标时减速
            zfloat speed = moveSpeed;
            zfloat slowDownDist = new zfloat(2);
            if (distance < slowDownDist)
            {
                speed = moveSpeed * distance / slowDownDist;
            }
            
            rvoSimulator.SetAgentPrefVelocity(rvoAgentId, direction * speed);
        }
        else
        {
            // 到达目标，停止
            rvoSimulator.SetAgentPrefVelocity(rvoAgentId, zVector2.zero);
        }
    }
}

// 游戏管理器
public class GameManager
{
    private RVO2Simulator rvoSimulator;
    private List<RTSUnit> units;
    
    public void Initialize()
    {
        rvoSimulator = new RVO2Simulator();
        units = new List<RTSUnit>();
    }
    
    public void FixedUpdate(zfloat deltaTime)
    {
        // 1. 每个单位设置期望速度
        foreach (var unit in units)
        {
            unit.Update(deltaTime);
        }
        
        // 2. 统一执行避障计算和位置更新
        rvoSimulator.DoStep(deltaTime);
        
        // 3. 同步位置到表现层（可选）
        foreach (var unit in units)
        {
            // unit.SyncPositionToView();
        }
    }
}
```

## 常见问题

### Q1: 智能体还是会发生碰撞？
**A:** 
- 增大 `radius` 参数（建议实际半径的1.2倍）
- 增大 `timeHorizon`（从2.0增加到3.0）
- 降低 `maxSpeed`

### Q2: 智能体移动太保守，绕很远的路？
**A:**
- 减小 `radius` 参数
- 减小 `timeHorizon`（从2.0减少到1.0）

### Q3: 性能问题？
**A:**
- 使用空间分区，只考虑附近的智能体
- 降低 `maxNeighbors` 参数（从10降到5）
- 降低更新频率（不是每帧都更新）

### Q4: 智能体卡在角落出不来？
**A:**
- RVO2是局部避障算法，需要配合全局路径规划（A*）
- 定期检测卡住状态，重新规划路径

### Q5: 需要避开静态障碍物？
**A:**
- 方案1: 在障碍物边界添加速度为0的RVO智能体
- 方案2: 配合导航网格（NavMesh）使用
- 方案3: 使用全局路径规划避开大型障碍物

## 参数推荐值

### 人群模拟（密集场景）
```csharp
radius: 0.3 - 0.5
maxSpeed: 1.0 - 1.5
timeHorizon: 2.0 - 3.0
maxNeighbors: 10 - 15
```

### RTS游戏单位
```csharp
radius: 0.5 - 0.8
maxSpeed: 2.0 - 5.0
timeHorizon: 1.5 - 2.5
maxNeighbors: 5 - 10
```

### 机器人导航（稀疏场景）
```csharp
radius: 0.4 - 0.6
maxSpeed: 1.0 - 2.0
timeHorizon: 2.0
maxNeighbors: 5
```

## 优化建议

### 1. 空间分区
```csharp
// 只对附近的智能体进行避障计算
// 可以使用网格分区或四叉树
```

### 2. 更新频率控制
```csharp
// 不是每帧都更新RVO
if (frameCount % 2 == 0) // 每2帧更新一次
{
    simulator.DoStep(deltaTime * 2);
}
```

### 3. LOD系统
```csharp
// 远处的智能体使用简化的避障
// 或者直接不参与RVO计算
```

## 与其他系统集成

### 配合A*路径规划
```csharp
1. 使用A*计算全局路径
2. 提取路径的下一个航点作为目标
3. 使用RVO计算朝向该航点的局部避障速度
4. 到达航点后，切换到下一个航点
```

### 配合ECS架构
```csharp
1. 创建RVO组件存储agentId
2. 在RVO System中统一处理所有智能体
3. 位置更新后写回Transform组件
```

## 调试技巧

### 可视化
- 绘制智能体的当前速度（蓝色箭头）
- 绘制智能体的期望速度（绿色箭头）
- 绘制智能体的碰撞半径（圆圈）
- 绘制ORCA线（高级调试）

### 日志输出
```csharp
// 检查速度差异
zVector2 velocity = simulator.GetAgentVelocity(id);
zVector2 prefVelocity = agent.prefVelocity;
zfloat difference = (velocity - prefVelocity).magnitude;
if (difference > threshold)
{
    zUDebug.Log($"智能体 {id} 速度偏离较大: {difference}");
}
```

## 总结

RVO2是一个强大的局部避障算法，适合用于：
- ✅ 多智能体实时避障
- ✅ 人群模拟
- ✅ RTS游戏单位移动
- ✅ 机器人导航

不适合用于：
- ❌ 静态障碍物避障（需要配合其他方法）
- ❌ 全局路径规划（需要配合A*等算法）
- ❌ 物理模拟（使用物理引擎更合适）

**最佳实践**: RVO2 + A*路径规划 + 导航网格

