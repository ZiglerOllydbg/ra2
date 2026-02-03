# Unity 资源缓存系统

## 简介

这是一个通用的Unity资源缓存管理系统，旨在解决频繁的Resources.Load调用带来的性能问题。通过缓存机制，可以显著提升资源加载速度，减少重复加载开销。

## 主要特性

- ✅ **多类型支持**: 支持GameObject、Texture、AudioClip、Material、Sprite、TextAsset等多种资源类型
- ✅ **高性能**: 首次加载后缓存资源，后续访问直接从内存获取
- ✅ **易用性**: 静态类设计，全局访问，API简洁明了
- ✅ **内存管理**: 提供缓存清理和管理功能
- ✅ **统计监控**: 可查看缓存使用情况和性能统计
- ✅ **预加载支持**: 支持批量预加载常用资源

## 使用方法

### 基本使用

```csharp
// 加载GameObject预制体
GameObject prefab = ResourceCache.GetPrefab("Prefabs/Player");

// 实例化预制体
GameObject instance = ResourceCache.InstantiatePrefab("Prefabs/Enemy");

// 加载纹理
Texture2D texture = ResourceCache.GetTexture2D("Textures/UI/Background");

// 加载音频
AudioClip sound = ResourceCache.GetAudioClip("Audio/SFX/Click");
```

### 通用加载方法

```csharp
// 使用泛型方法加载任意资源类型
GameObject prefab = ResourceCache.LoadResource<GameObject>("Prefabs/Player");
Texture2D texture = ResourceCache.LoadResource<Texture2D>("Textures/UITexture");
AudioClip audio = ResourceCache.LoadResource<AudioClip>("Audio/BGM");
```

### 预加载机制

```csharp
// 预加载多个预制体
ResourceCache.PreloadPrefabs(
    "Prefabs/UI/Panel1",
    "Prefabs/UI/Panel2", 
    "Prefabs/Units/Soldier"
);

// 在游戏启动时预加载常用资源
public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        ResourceCache.PreloadPrefabs(
            "Prefabs/UI/MainMenu",
            "Prefabs/UI/HUD",
            "Prefabs/Units/Player",
            "Prefabs/Units/Enemy"
        );
    }
}
```

## API参考

### GameObject相关方法
- `GetPrefab(string path)` - 获取预制体资源
- `InstantiatePrefab(string path, Transform parent = null)` - 实例化预制体
- `PreloadPrefabs(params string[] paths)` - 预加载多个预制体

### 其他资源类型方法
- `GetTexture(string path)` - 获取Texture资源
- `GetTexture2D(string path)` - 获取Texture2D资源
- `GetAudioClip(string path)` - 获取AudioClip资源
- `GetMaterial(string path)` - 获取Material资源
- `GetSprite(string path)` - 获取Sprite资源
- `GetTextAsset(string path)` - 获取TextAsset资源

### 通用方法
- `LoadResource<T>(string path)` - 泛型资源加载方法

### 缓存管理方法
- `ClearAllCache()` - 清除所有缓存
- `ClearCache<T>()` - 清除指定类型的缓存
- `RemoveFromCache<T>(string path)` - 从缓存中移除指定资源
- `GetCacheStats()` - 获取缓存统计信息

## 性能优势

### 测试数据示例
```
直接Resources.Load: 1000次调用耗时 850ms (平均0.85ms/次)
缓存加载: 1000次调用耗时 15ms (平均0.015ms/次)
性能提升: ~56倍
```

### 内存使用优化
- 避免重复加载相同资源
- 减少GC压力
- 提升资源访问速度

## 最佳实践

### 1. 合理使用预加载
```csharp
// 在游戏启动时预加载核心资源
public class ResourceManager : MonoBehaviour
{
    void Awake()
    {
        // 预加载UI资源
        ResourceCache.PreloadPrefabs(
            "Prefabs/UI/LoadingScreen",
            "Prefabs/UI/MainMenu",
            "Prefabs/UI/HUD"
        );
        
        // 预加载常用音效
        ResourceCache.GetAudioClip("Audio/SFX/ButtonClick");
        ResourceCache.GetAudioClip("Audio/SFX/Notification");
    }
}
```

### 2. 及时清理不用的缓存
```csharp
// 场景切换时清理特定资源
public class SceneTransition : MonoBehaviour
{
    public void OnSceneUnload()
    {
        // 清理UI相关缓存
        ResourceCache.ClearCache<GameObject>();
        ResourceCache.ClearCache<Sprite>();
    }
}
```

### 3. 监控缓存使用情况
```csharp
// 定期检查缓存状态
void Update()
{
    if (Input.GetKeyDown(KeyCode.F1))
    {
        Debug.Log(ResourceCache.GetCacheStats());
    }
}
```

## 注意事项

1. **资源路径**: 使用相对路径，相对于Resources文件夹
2. **内存管理**: 虽然有缓存清理功能，但仍需注意总体内存使用
3. **线程安全**: 当前版本不是线程安全的，请在主线程中使用
4. **资源更新**: 如果Resources文件夹中的资源发生变化，需要重启游戏才能生效

## 示例场景

项目中包含以下示例脚本：
- `ResourceCacheExample.cs` - 基本使用示例
- `ResourceCachePerformanceTest.cs` - 性能测试示例

## 扩展建议

可以根据项目需求扩展支持更多资源类型：
```csharp
// 添加对自定义资源类型的支持
public static CustomAsset GetCustomAsset(string path)
{
    // 实现自定义资源的缓存逻辑
}
```

## 版本历史

- v1.0.0: 初始版本，支持基础资源类型缓存
- v1.1.0: 添加性能测试和统计功能
- v1.2.0: 增加预加载和批量操作支持

---
*该资源缓存系统遵循Unity最佳实践，经过性能优化，适用于大多数Unity项目。*