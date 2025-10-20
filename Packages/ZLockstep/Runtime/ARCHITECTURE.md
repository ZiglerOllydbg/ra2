# ZLockstep ECS 架构文档

## 文件结构

```
Packages/ZLockstep/Runtime/
├── Core/                           # 核心库（确定性数学、工具）
│   ├── Math/                       # zfloat, zVector3, zQuaternion, zMatrix4x4
│   ├── PathFinding/                # A*寻路
│   └── Tools/                      # 调试工具
│
├── Simulation/                     # 逻辑层（纯C#，0依赖Unity）
│   ├── ECS/                        # ECS核心
│   │   ├── Components/             # 所有组件定义
│   │   │   ├── TransformComponent.cs       # 位置旋转缩放
│   │   │   ├── VelocityComponent.cs        # 速度
│   │   │   ├── UnitComponent.cs            # 单位属性
│   │   │   ├── HealthComponent.cs          # 生命值
│   │   │   ├── AttackComponent.cs          # 攻击能力
│   │   │   └── MoveCommandComponent.cs     # 移动命令
│   │   ├── Systems/                # 所有系统实现
│   │   │   └── MovementSystem.cs           # 移动系统
│   │   ├── BaseSystem.cs           # 系统基类
│   │   ├── ComponentManager.cs     # 组件管理器
│   │   ├── EntityManager.cs        # 实体管理器
│   │   ├── SystemManager.cs        # 系统管理器
│   │   ├── Entity.cs               # 实体定义
│   │   ├── IComponent.cs           # 组件接口
│   │   └── ISystem.cs              # 系统接口
│   ├── Events/                     # 事件系统
│   ├── zWorld.cs                   # 游戏世界
│   └── zTime.cs                    # 时间管理
│
├── View/                           # 表现层（依赖Unity）
│   ├── Systems/                    
│   │   └── PresentationSystem.cs           # 表现同步系统
│   ├── ViewComponent.cs            # Unity GameObject绑定
│   ├── EntityFactory.cs            # 实体工厂
│   ├── GameWorldBridge.cs          # 逻辑表现桥接
│   ├── MathConversionExtensions.cs # 数学类型转换
│   └── README_USAGE.md             # 使用指南
│
└── Sync/                           # 帧同步（命令、回放）
    ├── Command/
    ├── GameLogic/
    └── LockstepManager.cs
```

## 核心架构图

```
┌─────────────────────────────────────────────────────────┐
│                     Unity Layer                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │          GameWorldBridge.cs                       │  │
│  │  - 连接逻辑层和表现层                              │  │
│  │  - FixedUpdate: 驱动逻辑帧                        │  │
│  │  - Update: 插值平滑显示                           │  │
│  └──────────────┬────────────────┬───────────────────┘  │
│                 │                │                       │
└─────────────────┼────────────────┼───────────────────────┘
                  │                │
    ┌─────────────▼──────────┐  ┌──▼──────────────────────┐
    │   Simulation Layer     │  │   Presentation Layer    │
    │   (纯逻辑，确定性)      │  │   (Unity显示)           │
    │  ┌──────────────────┐  │  │  ┌──────────────────┐  │
    │  │     zWorld       │  │  │  │  EntityFactory   │  │
    │  │  ┌────────────┐  │  │  │  │  - CreateUnit()  │  │
    │  │  │SystemMgr  │  │  │  │  │  - CreateView()  │  │
    │  │  │ComponentMgr│  │  │  │  └──────────────────┘  │
    │  │  │EntityMgr   │  │  │  │  ┌──────────────────┐  │
    │  │  └────────────┘  │  │  │  │ViewComponent     │  │
    │  └──────────────────┘  │  │  │- GameObject      │  │
    │  ┌──────────────────┐  │  │  │- Transform       │  │
    │  │    Systems       │  │  │  │- Animator        │  │
    │  │  - Movement      │  │  │  └──────────────────┘  │
    │  │  - Combat        │  │  │  ┌──────────────────┐  │
    │  │  - AI            │  │  │  │PresentationSys   │  │
    │  └──────────────────┘  │  │  │- Sync Views      │  │
    │  ┌──────────────────┐  │  │  │- Lerp Update     │  │
    │  │   Components     │  │  │  └──────────────────┘  │
    │  │  - Transform     │──┼──┼──> MathConversion     │
    │  │  - Velocity      │  │  │    (zVector3 → Vector3)│
    │  │  - Unit          │  │  └────────────────────────┘
    │  │  - Health        │  │
    │  │  - Attack        │  │
    │  └──────────────────┘  │
    └─────────────────────────┘
```

## 数据流

### 1. 初始化流程

```
Unity Scene
  └─> GameWorldBridge.Awake()
       ├─> 创建 zWorld
       ├─> 初始化 SystemManager, ComponentManager, EntityManager
       ├─> 注册所有 Systems (Movement, Combat, Presentation...)
       └─> 创建 EntityFactory
```

### 2. 游戏循环

```
FixedUpdate (固定帧率，如20FPS)
  └─> zWorld.Update()
       ├─> TimeManager.Advance()              [更新逻辑时间]
       ├─> SystemManager.UpdateAll()          [执行所有系统]
       │    ├─> MovementSystem.Update()       [处理移动]
       │    ├─> CombatSystem.Update()         [处理战斗]
       │    ├─> AISystem.Update()             [处理AI]
       │    └─> PresentationSystem.Update()   [同步显示]
       └─> EventManager.Clear()               [清理事件]

Update (每帧，如60/120FPS)
  └─> PresentationSystem.LerpUpdate()         [插值平滑]
       └─> 将Unity对象插值到逻辑位置
```

### 3. 创建实体流程

```
EntityFactory.CreateUnit()
  ├─> EntityManager.CreateEntity()            [创建逻辑实体]
  ├─> 添加 TransformComponent                 [位置]
  ├─> 添加 UnitComponent                      [单位属性]
  ├─> 添加 HealthComponent                    [生命值]
  ├─> 添加 AttackComponent (可选)            [攻击能力]
  └─> 创建 ViewComponent                      [Unity表现]
       └─> Instantiate(prefab)
```

### 4. 移动流程

```
玩家点击地面
  └─> 添加 MoveCommandComponent(target)       [移动命令]

MovementSystem.Update()
  ├─> 遍历所有有 MoveCommandComponent 的实体
  ├─> 计算方向: (target - position).normalized
  ├─> 更新 TransformComponent.Rotation        [朝向目标]
  ├─> 设置 VelocityComponent                  [设置速度]
  └─> 到达时移除 MoveCommandComponent

MovementSystem.ApplyVelocity()
  ├─> 遍历所有有 VelocityComponent 的实体
  └─> 更新 TransformComponent.Position += velocity * deltaTime

PresentationSystem.Update()
  ├─> 遍历所有有 ViewComponent 的实体
  ├─> 读取 TransformComponent
  └─> 同步到 Unity Transform
       └─> viewTransform.position = logicPosition.ToVector3()
```

## 关键设计原则

### 1. 逻辑表现分离

| 层次 | 特点 | 依赖 |
|------|------|------|
| **Simulation** | 纯逻辑，确定性计算 | 仅依赖Core（数学库）|
| **View** | 显示、插值、特效 | 依赖Unity + Simulation |

### 2. 确定性保证

- ✅ 使用 `zfloat` 定点数
- ✅ 所有逻辑在 `FixedUpdate` 中以固定帧率运行
- ✅ 遍历实体时排序（`GetAllEntityIdsWith` 自动排序）
- ✅ 逻辑层0随机数，必须用 `zRandom`

### 3. 组件设计

- ✅ **struct**: 纯数据，值类型，修改后需写回
- ✅ **最小化**: 每个组件只存一种数据
- ❌ **不含逻辑**: 组件不包含方法（除了辅助属性）

```csharp
// ✅ 好的设计
public struct HealthComponent : IComponent
{
    public zfloat MaxHealth;
    public zfloat CurrentHealth;
    public bool IsAlive => CurrentHealth > zfloat.Zero;  // 只读属性OK
}

// ❌ 坏的设计
public class HealthComponent : IComponent  // ❌ 不要用class
{
    public void TakeDamage() { ... }        // ❌ 不要在组件里写逻辑
}
```

### 4. System设计

- ✅ 继承 `BaseSystem`
- ✅ 逻辑写在 `Update()` 中
- ✅ 使用 `ComponentManager.GetAllEntityIdsWith<T>()` 遍历
- ✅ 通过添加/移除组件来控制实体行为

```csharp
public class MySystem : BaseSystem
{
    public override void Update()
    {
        var entities = ComponentManager.GetAllEntityIdsWith<MyComponent>();
        
        foreach (var entityId in entities)
        {
            var entity = new Entity(entityId);
            
            // 读取组件
            var component = ComponentManager.GetComponent<MyComponent>(entity);
            
            // 修改数据
            component.Value += DeltaTime;
            
            // 写回（因为是struct）
            ComponentManager.AddComponent(entity, component);
        }
    }
}
```

## 常见模式

### 模式1：基于命令的行为

```csharp
// 添加命令组件来触发行为
var moveCmd = new MoveCommandComponent(targetPosition);
ComponentManager.AddComponent(entity, moveCmd);

// System处理命令
// 完成后移除命令
ComponentManager.RemoveComponent<MoveCommandComponent>(entity);
```

### 模式2：标记组件

```csharp
// 用空组件作为标记
public struct SelectedTag : IComponent { }

// 标记为选中
ComponentManager.AddComponent(entity, new SelectedTag());

// System只处理被标记的实体
var selected = ComponentManager.GetAllEntityIdsWith<SelectedTag>();
```

### 模式3：状态机

```csharp
public struct UnitStateComponent : IComponent
{
    public enum State { Idle, Moving, Attacking, Dead }
    public State CurrentState;
}

// 根据状态执行不同逻辑
var state = ComponentManager.GetComponent<UnitStateComponent>(entity);
switch (state.CurrentState)
{
    case State.Idle: /* ... */ break;
    case State.Moving: /* ... */ break;
}
```

## 性能优化建议

1. **缓存查询结果**：不要在Update中重复查询相同的组件类型
2. **按需添加ViewComponent**：服务器端不需要
3. **合理的逻辑帧率**：20FPS通常够用，不要超过30FPS
4. **使用struct**：组件用struct而非class（值类型，内存友好）
5. **空间划分**：对于碰撞检测，使用Quadtree/Grid

## 扩展建议

### 近期任务
- [ ] CombatSystem（战斗系统）
- [ ] BuildingSystem（建筑系统）
- [ ] ResourceSystem（资源系统）
- [ ] PathfindingSystem（寻路系统）

### 中期任务
- [ ] CommandQueue（命令队列）
- [ ] SelectionSystem（单位选择）
- [ ] FogOfWar（战争迷雾）
- [ ] ReplaySystem（回放系统）

### 长期任务
- [ ] 网络同步（帧同步）
- [ ] AI行为树集成
- [ ] 技能系统
- [ ] Buff/Debuff系统

---

更多使用示例请参考：[View/README_USAGE.md](View/README_USAGE.md)

