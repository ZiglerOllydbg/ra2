using UnityEngine;

/// <summary>
/// 资源缓存使用示例
/// 演示如何使用ResourceCache类进行各种资源的缓存管理
/// </summary>
public class ResourceCacheExample : MonoBehaviour
{
    [Header("测试资源配置")]
    [SerializeField] private string testPrefabPath = "Prefabs/TestObject";
    [SerializeField] private string testTexturePath = "Textures/TestTexture";
    [SerializeField] private string testAudioPath = "Audio/TestSound";
    [SerializeField] private string testMaterialPath = "Materials/TestMaterial";
    [SerializeField] private string testSpritePath = "Sprites/TestSprite";
    [SerializeField] private string testTextPath = "Data/Config";

    void Start()
    {
        // 显示初始缓存状态
        Debug.Log("Initial Cache Status: " + ResourceCache.GetCacheStats());
        
        // 测试各种资源缓存
        TestGameObjectCache();
        TestTextureCache();
        TestAudioCache();
        TestMaterialCache();
        TestSpriteCache();
        TestTextAssetCache();
        TestGenericCache();
        
        // 显示最终缓存状态
        Debug.Log("Final Cache Status: " + ResourceCache.GetCacheStats());
    }

    /// <summary>
    /// 测试GameObject资源缓存
    /// </summary>
    private void TestGameObjectCache()
    {
        Debug.Log("--- Testing GameObject Cache ---");
        
        // 第一次加载（会从Resources加载并缓存）
        GameObject prefab1 = ResourceCache.GetPrefab(testPrefabPath);
        if (prefab1 != null)
        {
            Debug.Log($"Loaded prefab: {prefab1.name}");
            
            // 实例化预制体
            GameObject instance = ResourceCache.InstantiatePrefab(testPrefabPath);
            if (instance != null)
            {
                Debug.Log($"Instantiated prefab: {instance.name}");
                // 为了演示，立即销毁实例
                Destroy(instance);
            }
        }
        
        // 第二次加载（从缓存获取）
        GameObject prefab2 = ResourceCache.GetPrefab(testPrefabPath);
        Debug.Log($"Second load from cache: {prefab2?.name ?? "null"}");
        
        // 预加载多个预制体
        ResourceCache.PreloadPrefabs("Prefabs/UI/Panel1", "Prefabs/UI/Panel2", "Prefabs/Units/Soldier");
        Debug.Log("Preloaded multiple prefabs");
    }

    /// <summary>
    /// 测试Texture资源缓存
    /// </summary>
    private void TestTextureCache()
    {
        Debug.Log("--- Testing Texture Cache ---");
        
        // 加载Texture
        Texture texture = ResourceCache.GetTexture(testTexturePath);
        Debug.Log($"Loaded texture: {texture?.name ?? "null"}");
        
        // 加载Texture2D
        Texture2D texture2D = ResourceCache.GetTexture2D(testTexturePath);
        Debug.Log($"Loaded texture2D: {texture2D?.name ?? "null"}");
    }

    /// <summary>
    /// 测试AudioClip资源缓存
    /// </summary>
    private void TestAudioCache()
    {
        Debug.Log("--- Testing Audio Cache ---");
        
        AudioClip clip = ResourceCache.GetAudioClip(testAudioPath);
        Debug.Log($"Loaded audio clip: {clip?.name ?? "null"}");
    }

    /// <summary>
    /// 测试Material资源缓存
    /// </summary>
    private void TestMaterialCache()
    {
        Debug.Log("--- Testing Material Cache ---");
        
        Material material = ResourceCache.GetMaterial(testMaterialPath);
        Debug.Log($"Loaded material: {material?.name ?? "null"}");
    }

    /// <summary>
    /// 测试Sprite资源缓存
    /// </summary>
    private void TestSpriteCache()
    {
        Debug.Log("--- Testing Sprite Cache ---");
        
        Sprite sprite = ResourceCache.GetSprite(testSpritePath);
        Debug.Log($"Loaded sprite: {sprite?.name ?? "null"}");
    }

    /// <summary>
    /// 测试TextAsset资源缓存
    /// </summary>
    private void TestTextAssetCache()
    {
        Debug.Log("--- Testing TextAsset Cache ---");
        
        TextAsset textAsset = ResourceCache.GetTextAsset(testTextPath);
        Debug.Log($"Loaded text asset: {textAsset?.name ?? "null"}");
        if (textAsset != null)
        {
            Debug.Log($"Text content length: {textAsset.text.Length}");
        }
    }

    /// <summary>
    /// 测试通用资源加载方法
    /// </summary>
    private void TestGenericCache()
    {
        Debug.Log("--- Testing Generic Cache ---");
        
        // 使用泛型方法加载不同类型资源
        GameObject genericPrefab = ResourceCache.LoadResource<GameObject>(testPrefabPath);
        Texture2D genericTexture = ResourceCache.LoadResource<Texture2D>(testTexturePath);
        AudioClip genericClip = ResourceCache.LoadResource<AudioClip>(testAudioPath);
        
        Debug.Log($"Generic loaded - Prefab: {genericPrefab?.name ?? "null"}, " +
                  $"Texture: {genericTexture?.name ?? "null"}, " +
                  $"Audio: {genericClip?.name ?? "null"}");
    }

    /// <summary>
    /// 测试缓存管理功能
    /// </summary>
    private void TestCacheManagement()
    {
        Debug.Log("--- Testing Cache Management ---");
        
        // 移除特定资源
        ResourceCache.RemoveFromCache<GameObject>(testPrefabPath);
        Debug.Log("Removed specific prefab from cache");
        
        // 清除特定类型缓存
        ResourceCache.ClearCache<Texture>();
        Debug.Log("Cleared texture cache");
        
        // 清除所有缓存
        ResourceCache.ClearAllCache();
        Debug.Log("Cleared all caches");
        
        // 显示清理后的状态
        Debug.Log("Cache status after cleanup: " + ResourceCache.GetCacheStats());
    }

    void OnDestroy()
    {
        // 组件销毁时可以选择清理缓存
        // ResourceCache.ClearAllCache();
    }
}