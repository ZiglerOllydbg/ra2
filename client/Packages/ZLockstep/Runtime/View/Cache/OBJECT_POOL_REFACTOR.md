# GameObjectPoolManager 技术文档

## 概述

`GameObjectPoolManager` 是一个高效的 Unity GameObject 对象池管理器，通过复用 GameObject 实例来减少频繁的实例化和销毁操作，从而提升游戏性能。该管理器提供了完整的对象生命周期管理、容量控制、延迟回收等高级功能。

## 核心特性

- ✅ **对象复用**: 减少 `Instantiate` 和 `Destroy` 的调用次数，降低 GC 压力
- ✅ **容量控制**: 支持初始容量和最大容量配置，防止内存无限增长
- ✅ **延迟回收**: 支持延迟回收机制，适配死亡动画等场景
- ✅ **预热功能**: 支持对象池预热，避免运行时首次创建的开销
- ✅ **自动映射**: 自动维护 GameObject 与预制体路径的映射关系
- ✅ **统计监控**: 提供对象池统计信息，便于性能分析和调试
- ✅ **异常处理**: 完善的空值检查和错误处理机制

## 数据结构

### GameObjectPool 类

对象池的核心数据结构，包含以下字段：

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `availableObjects` | `Queue<GameObject>` | 可用对象队列，存储可复用的非活跃对象 |
| `activeObjects` | `List<GameObject>` | 使用中对象列表，存储当前正在使用的活跃对象 |
| `prefab` | `GameObject` | 预制体引用，用于创建新实例 |
| `parent` | `Transform` | 父节点 Transform，所有池化对象都以此为父节点 |
| `initialCapacity` | `int` | 初始容量，对象池初始化时预创建的对象数量 |
| `maxCapacity` | `int` | 最大容量，对象池允许容纳的最大对象数量 |
| `TotalCount` | `int` | 只读属性，当前总对象数（可用 + 活跃） |

## API 参考

### 常量定义

```csharp
public const int DEFAULT_POOL_INITIAL_CAPACITY = 5   // 默认初始容量
public const int DEFAULT_POOL_MAX_CAPACITY = 50      // 默认最大容量
```

### 对象池获取与创建

#### GetFromPool

从对象池获取 GameObject 实例。

**方法签名**:
```csharp
public static GameObject GetFromPool(
    string prefabPath, 
    Transform parent = null, 
    Vector3? position = null, 
    Quaternion? rotation = null)
```

**参数说明**:
- `prefabPath`: 预制体路径（必填），用于标识对象池
- `parent`: 父对象 Transform（可选），默认为 null
- `position`: 世界坐标位置（可选），默认为 (0,0,0)
- `rotation`: 旋转（可选），默认为 `Quaternion.identity`

**返回值**:
- 成功：返回激活的 GameObject 实例
- 失败：返回 null（路径为空、池创建失败、超出最大容量）

**工作流程**:
1. 检查路径有效性
2. 如果对象池不存在，自动创建池
3. 从可用队列中取出对象，或创建新对象（未超上限时）
4. 设置对象的父节点、位置和旋转
5. 激活对象并加入使用中列表
6. 记录对象到池的映射关系

**使用示例**:
```csharp
// 基础用法
GameObject enemy = GameObjectPoolManager.GetFromPool("Prefabs/Enemy");

// 指定父节点和位置
GameObject bullet = GameObjectPoolManager.GetFromPool(
    "Prefabs/Bullet", 
    transform, 
    transform.position, 
    transform.rotation
);
```

---

#### CreatePool

手动创建对象池。

**方法签名**:
```csharp
public static bool CreatePool(
    string prefabPath, 
    Transform parent = null, 
    int initialCapacity = DEFAULT_POOL_INITIAL_CAPACITY, 
    int maxCapacity = DEFAULT_POOL_MAX_CAPACITY)
```

**参数说明**:
- `prefabPath`: 预制体路径
- `parent`: 父对象 Transform
- `initialCapacity`: 初始容量（默认 5）
- `maxCapacity`: 最大容量（默认 50）

**返回值**:
- 成功：返回 true
- 失败：返回 false（路径为空、池已存在、预制体加载失败）

**注意事项**:
- 该方法通常不需要手动调用，`GetFromPool` 会自动创建池
- 适用于需要预先配置池容量的场景

**使用示例**:
```csharp
// 创建一个大容量的对象池
GameObjectPoolManager.CreatePool("Prefabs/Bullet", null, 20, 200);
```

---

### 对象回收

#### ReturnToPool

回收 GameObject 到对象池。

**方法签名**:
```csharp
public static void ReturnToPool(GameObject obj, float delay = 0f)
```

**参数说明**:
- `obj`: 要回收的 GameObject 实例
- `delay`: 延迟回收时间（秒），<=0 表示立即回收

**工作流程**:
1. 检查对象有效性
2. 通过映射字典查找对应的预制体路径
3. 查找对应的对象池
4. 从使用中列表移除对象
5. 根据 delay 参数决定立即回收或延迟回收

**使用示例**:
```csharp
// 立即回收
GameObjectPoolManager.ReturnToPool(enemy);

// 延迟 2 秒回收（用于播放死亡动画）
GameObjectPoolManager.ReturnToPool(enemy, 2.0f);
```

**异常情况处理**:
- 对象为 null: 记录错误并返回
- 对象未被池追踪: 记录警告并直接销毁
- 对象池不存在: 记录警告并直接销毁
- 对象已在回收状态: 记录警告避免重复回收

---

### 对象池预热

#### PrewarmPool

预创建对象池中的对象，用于减少首次使用时的延迟。

**方法签名**:
```csharp
public static void PrewarmPool(string prefabPath, int count)
```

**参数说明**:
- `prefabPath`: 预制体路径
- `count`: 预创建数量

**使用示例**:
```csharp
// 游戏启动时预热子弹池
void Start()
{
    GameObjectPoolManager.PrewarmPool("Prefabs/Bullet", 50);
}
```

**注意事项**:
- 如果池不存在，会先自动创建池
- 预创建数量受最大容量限制

---

### 对象池管理

#### ClearPool

清空指定对象池。

**方法签名**:
```csharp
public static void ClearPool(string prefabPath, bool destroyAll = false)
```

**参数说明**:
- `prefabPath`: 预制体路径
- `destroyAll`: 是否销毁所有对象（包括使用中的），默认 false

**使用场景**:
- 关卡切换时清理资源
- 测试模式下重置环境

**示例**:
```csharp
// 安全清理（保留使用中的对象）
GameObjectPoolManager.ClearPool("Prefabs/Enemy");

// 强制清理（销毁所有对象）
GameObjectPoolManager.ClearPool("Prefabs/Enemy", true);
```

---

#### ClearAllPools

清空所有对象池。

**方法签名**:
```csharp
public static void ClearAllPools(bool destroyAll = false)
```

**参数说明**:
- `destroyAll`: 是否销毁所有对象（包括使用中的）

**使用场景**:
- 游戏退出前清理资源
- 场景切换时重置对象池

---

#### ResizePool

调整对象池的最大容量。

**方法签名**:
```csharp
public static void ResizePool(string prefabPath, int newMaxCapacity)
```

**参数说明**:
- `prefabPath`: 预制体路径
- `newMaxCapacity`: 新的最大容量（必须 > 0）

**示例**:
```csharp
// 动态扩容
GameObjectPoolManager.ResizePool("Prefabs/Bullet", 300);
```

---

### 统计信息与查询

#### GetPoolStats

获取单个对象池的统计信息。

**方法签名**:
```csharp
public static string GetPoolStats(string prefabPath)
```

**返回值**: 格式化的统计信息字符串

**示例输出**:
```
Pool Stats for Prefabs/Bullet - Available: 15, Active: 5, Total: 20/50
```

---

#### GetAllPoolStats

获取所有对象池的统计信息。

**方法签名**:
```csharp
public static string GetAllPoolStats()
```

**返回值**: 包含所有池统计信息的字符串

**示例输出**:
```
=== Object Pool Statistics ===
Pool: Prefabs/Bullet
  Available: 15, Active: 5, Total: 20/50
Pool: Prefabs/Enemy
  Available: 3, Active: 7, Total: 10/50
Total Pools: 2
```

**使用场景**:
- 性能监控
- 调试模式显示
- FPS 面板集成

---

#### IsPooledObject

检查对象是否由对象池管理。

**方法签名**:
```csharp
public static bool IsPooledObject(GameObject obj)
```

**参数说明**:
- `obj`: 要检查的 GameObject

**返回值**:
- true: 对象由对象池管理
- false: 对象不是池化对象或为 null

**使用示例**:
```csharp
if (GameObjectPoolManager.IsPooledObject(obj))
{
    GameObjectPoolManager.ReturnToPool(obj);
}
else
{
    GameObject.Destroy(obj);
}
```

---

## 架构设计

### 核心组件关系

```
┌─────────────────────────────────────┐
│     GameObjectPoolManager           │
│  (静态管理类，无实例)                │
└─────────────────────────────────────┘
                  │
                  │ 管理多个
                  ▼
┌─────────────────────────────────────┐
│     GameObjectPool (字典存储)        │
│  - availableObjects (Queue)         │
│  - activeObjects (List)             │
│  - prefab, parent, capacity         │
└─────────────────────────────────────┘
                  │
                  │ 映射关系
                  ▼
┌─────────────────────────────────────┐
│  gameObjectToPathMap (Dictionary)   │
│  GameObject -> prefabPath           │
└─────────────────────────────────────┘
```

### 对象生命周期流程

#### 获取对象流程
```
GetFromPool 请求
    ↓
检查池是否存在 → 不存在 → CreatePool
    ↓
从 availableObjects  dequeue
    ↓
队列为空？→ 是 → 检查容量 → 超限 → 返回 null
                        ↓ 未超限
                        Instantiate
    ↓
设置 transform (parent, position, rotation)
    ↓
SetActive(true)
    ↓
加入 activeObjects
    ↓
记录到 gameObjectToPathMap
    ↓
返回 GameObject
```

#### 回收对象流程
```
ReturnToPool 请求
    ↓
检查对象有效性
    ↓
查找 gameObjectToPathMap → 未找到 → Destroy 并返回
    ↓
查找对应池 → 未找到 → Destroy 并返回
    ↓
从 activeObjects Remove
    ↓
delay > 0？
    ├─ 是 → DelayedReturnToPool (协程延迟)
    └─ 否 → ImmediateReturnToPool
              ↓
           SetActive(false)
              ↓
           设置父节点为 pool.parent
              ↓
           enqueue 到 availableObjects
              ↓
           从 gameObjectToPathMap 移除
```

---

## 最佳实践

### 1. 对象池配置建议

| 对象类型 | 初始容量 | 最大容量 | 说明 |
|----------|----------|----------|------|
| 子弹 | 20-50 | 200-500 | 高频创建，建议大池 |
| 敌人 | 5-10 | 50-100 | 中频创建，中等池 |
| 特效 | 10-20 | 100-200 | 短时存在，需要快速复用 |
| UI 元素 | 5-10 | 30-50 | 低频创建，小池即可 |

### 2. 使用时机

**推荐使用对象池的场景**:
- ✅ 频繁创建和销毁的对象（子弹、特效）
- ✅ 对性能敏感的对象
- ✅ 预制体加载开销较大的对象

**不建议使用对象池的场景**:
- ❌ 一次性对象（过场动画道具）
- ❌ 状态复杂且难以重置的对象
- ❌ 内存敏感的低频对象

### 3. 延迟回收应用

```csharp
// 敌人死亡时播放动画，2 秒后回收
void OnEnemyDeath(GameObject enemy)
{
    // 播放死亡动画
    enemy.GetComponent<Animator>().SetTrigger("Die");
    
    // 延迟回收，给动画播放留时间
    GameObjectPoolManager.ReturnToPool(enemy, 2.0f);
}
```

### 4. 预热策略

```csharp
// 游戏启动时预热常用对象池
void InitializePools()
{
    GameObjectPoolManager.PrewarmPool("Prefabs/Bullet", 50);
    GameObjectPoolManager.PrewarmPool("Prefabs/Explosion", 20);
    GameObjectPoolManager.PrewarmPool("Prefabs/Enemy", 10);
}
```

### 5. 性能监控

```csharp
// 在调试界面显示对象池状态
void OnGUI()
{
    if (showStats)
    {
        GUILayout.Label(GameObjectPoolManager.GetAllPoolStats());
    }
}
```

---

## 注意事项与常见问题

### ⚠️ 状态重置责任

**重要**: 对象池不会自动重置 GameObject 的状态！回收前必须手动重置。

```csharp
// 错误示例 ❌
GameObjectPoolManager.ReturnToPool(enemy);

// 正确示例 ✅
enemy.GetComponent<Enemy>().Reset();  // 重置脚本状态
enemy.transform.localPosition = Vector3.zero;  // 重置位置
GameObjectPoolManager.ReturnToPool(enemy);
```

### ⚠️ 防止重复回收

确保每个对象只被回收一次：

```csharp
// 添加标记防止重复回收
private bool isReturned = false;

void ReturnToPool()
{
    if (isReturned) return;
    isReturned = true;
    
    GameObjectPoolManager.ReturnToPool(gameObject);
}
```

### ⚠️ 容量规划

对象池达到最大容量后会返回 null，需要根据游戏规模合理配置：

```csharp
// 监测容量警告
GameObject obj = GameObjectPoolManager.GetFromPool("Prefabs/Bullet");
if (obj == null)
{
    // 容量不足，降级处理
    Debug.LogWarning("Bullet pool exhausted!");
}
```

### ⚠️ 场景切换清理

场景切换时建议清理对象池，避免跨场景对象污染：

```csharp
void OnSceneUnload()
{
    GameObjectPoolManager.ClearAllPools(true);
}
```

---

## 依赖关系

### 内部依赖
- `ResourceCache.GetPrefab(string path)`: 用于加载预制体资源

### Unity API 使用
- `GameObject.Instantiate`: 实例化对象
- `GameObject.Destroy`: 销毁对象
- `GameObject.SetActive`: 激活/停用对象
- `Transform.SetParent/SetPosition/SetRotation`: 变换操作
- `zUDebug.Log/Warning/Error`: 日志输出

---

## 扩展与优化建议

### 可能的扩展方向

1. **异步加载支持**: 集成 UniTask 实现异步对象池创建
2. **分级池管理**: 根据优先级管理不同对象池
3. **自动扩缩容**: 根据使用情况动态调整池容量
4. **对象池事件**: 添加获取/回收事件钩子
5. **持久化池**: 跨场景保留特定对象池

### 性能优化建议

1. **使用 ArrayPool**: 替换 List 减少 GC
2. **批量操作**: 支持批量获取和回收
3. **层级池结构**: 父子对象一起池化
4. **条件编译**: 在开发版本启用详细日志，发布版本关闭

---

## 版本历史

| 版本 | 日期 | 变更说明 |
|------|------|----------|
| 1.0 | 2024 | 初始版本，实现基础对象池功能 |

---

## 相关文件

- `GameObjectPoolManager.cs`: 对象池管理器实现
- `ResourceCache.cs`: 资源缓存系统（依赖）
- `OBJECT_POOL_REFACTOR.md`: 重构记录文档

---

## 联系方式

如有问题或建议，请联系 ZLockstep 框架维护团队。
