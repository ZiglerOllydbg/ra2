# RVO2 定点数实现

这是一个基于定点数（zfloat）的RVO2（Reciprocal Velocity Obstacles）算法实现，用于多智能体的局部避障和运动规划。

## 算法简介

RVO2使用ORCA（Optimal Reciprocal Collision Avoidance）算法，通过线性规划在速度空间中找到最优避障速度。该实现完全基于定点数，保证了多端确定性同步。

## 核心概念

### 输入参数
每个智能体需要设置：
- **position** (zVector2): 当前位置
- **radius** (zfloat): 智能体半径
- **maxSpeed** (zfloat): 最大移动速度
- **prefVelocity** (zVector2): 期望速度（目标方向和速度）
- **timeHorizon** (zfloat): 避障时间范围（默认2.0，表示在未来2秒内避免碰撞）

### 输出结果
- **velocity** (zVector2): 计算得到的实际速度（已考虑避障）

## 使用示例

### 基础使用

```csharp
using ZLockstep.RVO;
using zUnity;

// 1. 创建模拟器
RVO2Simulator simulator = new RVO2Simulator();

// 2. 添加智能体
zVector2 pos1 = new zVector2((zfloat)0, (zfloat)0);
zfloat radius = new zfloat(0, 5000); // 0.5
zfloat maxSpeed = new zfloat(2); // 2.0
int agent1 = simulator.AddAgent(pos1, radius, maxSpeed);

zVector2 pos2 = new zVector2((zfloat)5, (zfloat)0);
int agent2 = simulator.AddAgent(pos2, radius, maxSpeed);

// 3. 每帧更新
void Update(zfloat deltaTime)
{
    // 设置期望速度（例如朝向目标）
    zVector2 targetPos = new zVector2((zfloat)10, (zfloat)0);
    zVector2 currentPos = simulator.GetAgentPosition(agent1);
    zVector2 direction = (targetPos - currentPos).normalized;
    simulator.SetAgentPrefVelocity(agent1, direction * maxSpeed);
    
    // 执行模拟步
    simulator.DoStep(deltaTime);
    
    // 获取避障后的速度
    zVector2 velocity = simulator.GetAgentVelocity(agent1);
    
    // 位置已自动更新，可直接获取
    zVector2 newPos = simulator.GetAgentPosition(agent1);
}
```

### 多智能体避障示例

```csharp
// 创建一群智能体
List<int> agentIds = new List<int>();
for (int i = 0; i < 10; i++)
{
    zVector2 pos = new zVector2((zfloat)i, (zfloat)0);
    zfloat radius = new zfloat(0, 3000); // 0.3
    zfloat maxSpeed = new zfloat(1, 5000); // 1.5
    int id = simulator.AddAgent(pos, radius, maxSpeed);
    agentIds.Add(id);
}

// 每帧更新所有智能体
void UpdateAllAgents(zfloat deltaTime)
{
    // 设置每个智能体的期望速度
    for (int i = 0; i < agentIds.Count; i++)
    {
        int agentId = agentIds[i];
        zVector2 targetPos = GetTargetForAgent(agentId); // 自定义目标
        zVector2 currentPos = simulator.GetAgentPosition(agentId);
        
        zVector2 direction = targetPos - currentPos;
        zfloat distance = direction.magnitude;
        
        if (distance > zfloat.Epsilon)
        {
            direction = direction / distance;
            zfloat speed = zMathf.Min(distance / deltaTime, (zfloat)1.5);
            simulator.SetAgentPrefVelocity(agentId, direction * speed);
        }
        else
        {
            simulator.SetAgentPrefVelocity(agentId, zVector2.zero);
        }
    }
    
    // 执行一步模拟
    simulator.DoStep(deltaTime);
}
```

### 参数调优建议

1. **radius（半径）**: 
   - 设置为实际物体半径的1.1-1.2倍，留出安全边距
   - 太大会导致过于保守的避障
   - 太小可能产生碰撞

2. **maxSpeed（最大速度）**:
   - 设置为实际最大移动速度
   - 避障后的速度不会超过此值

3. **timeHorizon（时间范围）**:
   - 默认2.0秒通常适用于大多数场景
   - 增大：更早开始避障，路径更平滑但可能绕远路
   - 减小：更晚避障，更直接但可能来不及避开

4. **maxNeighbors（最大邻居数）**:
   - 默认10个，影响性能
   - 密集场景可以增加到15-20
   - 稀疏场景可以减少到5

## 性能考虑

- 时间复杂度: O(n²)，n为智能体数量
- 对于大规模场景（100+智能体），建议：
  1. 只考虑附近的智能体（空间分区）
  2. 降低更新频率
  3. 使用更小的maxNeighbors值

## 注意事项

1. **定点数精度**: 所有计算使用zfloat（定点数），保证多端同步
2. **碰撞处理**: 算法会尽力避免碰撞，但极端情况下可能穿透
3. **静态障碍物**: 当前实现不支持静态障碍物，可通过添加速度为0的智能体模拟
4. **局部避障**: 这是局部避障算法，需要配合全局路径规划使用

## API参考

### RVO2Simulator

- `int AddAgent(position, radius, maxSpeed, maxNeighbors, timeHorizon)`: 添加智能体
- `void SetAgentPrefVelocity(agentId, velocity)`: 设置期望速度
- `void SetAgentPosition(agentId, position)`: 手动设置位置
- `zVector2 GetAgentPosition(agentId)`: 获取位置
- `zVector2 GetAgentVelocity(agentId)`: 获取速度
- `void DoStep(deltaTime)`: 执行一步模拟
- `int GetNumAgents()`: 获取智能体数量
- `void Clear()`: 清除所有智能体

## 参考文献

- Berg, J. van den, et al. "Reciprocal n-body collision avoidance." ISRR 2011.
- 原始RVO2库: https://github.com/snape/RVO2

