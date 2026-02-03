using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用资源缓存管理器
/// 支持多种Unity资源类型的缓存，避免重复加载，提高性能
/// </summary>
public static class ResourceCache
{
    #region 常量定义
    
    /// <summary>
    /// 默认缓存容量
    /// </summary>
    private const int DEFAULT_CACHE_CAPACITY = 100;
    
    /// <summary>
    /// 最大缓存容量
    /// </summary>
    private const int MAX_CACHE_CAPACITY = 1000;
    
    #endregion

    #region 缓存字典
    
    /// <summary>
    /// GameObject资源缓存
    /// </summary>
    private static readonly Dictionary<string, GameObject> gameObjectCache = new Dictionary<string, GameObject>();
    
    /// <summary>
    /// Texture资源缓存
    /// </summary>
    private static readonly Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>();
    
    /// <summary>
    /// AudioClip资源缓存
    /// </summary>
    private static readonly Dictionary<string, AudioClip> audioClipCache = new Dictionary<string, AudioClip>();
    
    /// <summary>
    /// Material资源缓存
    /// </summary>
    private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    
    /// <summary>
    /// Sprite资源缓存
    /// </summary>
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    
    /// <summary>
    /// TextAsset资源缓存
    /// </summary>
    private static readonly Dictionary<string, TextAsset> textAssetCache = new Dictionary<string, TextAsset>();
    
    #endregion

    #region GameObject资源缓存方法
    
    /// <summary>
    /// 获取GameObject资源（带缓存）
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <returns>GameObject实例，如果找不到则返回null</returns>
    public static GameObject GetPrefab(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("Prefab path cannot be null or empty");
            return null;
        }

        if (!gameObjectCache.TryGetValue(prefabPath, out GameObject prefab))
        {
            prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Prefab not found: {prefabPath}");
                return null;
            }
            gameObjectCache[prefabPath] = prefab;
        }
        return prefab;
    }
    
    /// <summary>
    /// 实例化缓存的预制体
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <param name="parent">父对象</param>
    /// <returns>实例化的GameObject</returns>
    public static GameObject InstantiatePrefab(string prefabPath, Transform parent = null)
    {
        GameObject prefab = GetPrefab(prefabPath);
        if (prefab == null) return null;
        
        return parent != null ? 
            GameObject.Instantiate(prefab, parent) : 
            GameObject.Instantiate(prefab);
    }
    
    /// <summary>
    /// 预加载GameObject资源到缓存
    /// </summary>
    /// <param name="prefabPaths">预制体路径数组</param>
    public static void PreloadPrefabs(params string[] prefabPaths)
    {
        foreach (string path in prefabPaths)
        {
            GetPrefab(path);
        }
    }
    
    #endregion

    #region Texture资源缓存方法
    
    /// <summary>
    /// 获取Texture资源（带缓存）
    /// </summary>
    /// <param name="texturePath">纹理路径</param>
    /// <returns>Texture实例</returns>
    public static Texture GetTexture(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath))
        {
            Debug.LogError("Texture path cannot be null or empty");
            return null;
        }

        if (!textureCache.TryGetValue(texturePath, out Texture texture))
        {
            texture = Resources.Load<Texture>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"Texture not found: {texturePath}");
                return null;
            }
            textureCache[texturePath] = texture;
        }
        return texture;
    }
    
    /// <summary>
    /// 获取Texture2D资源（带缓存）
    /// </summary>
    /// <param name="texturePath">纹理路径</param>
    /// <returns>Texture2D实例</returns>
    public static Texture2D GetTexture2D(string texturePath)
    {
        return GetTexture(texturePath) as Texture2D;
    }
    
    #endregion

    #region AudioClip资源缓存方法
    
    /// <summary>
    /// 获取AudioClip资源（带缓存）
    /// </summary>
    /// <param name="audioPath">音频路径</param>
    /// <returns>AudioClip实例</returns>
    public static AudioClip GetAudioClip(string audioPath)
    {
        if (string.IsNullOrEmpty(audioPath))
        {
            Debug.LogError("Audio clip path cannot be null or empty");
            return null;
        }

        if (!audioClipCache.TryGetValue(audioPath, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(audioPath);
            if (clip == null)
            {
                Debug.LogError($"Audio clip not found: {audioPath}");
                return null;
            }
            audioClipCache[audioPath] = clip;
        }
        return clip;
    }
    
    #endregion

    #region Material资源缓存方法
    
    /// <summary>
    /// 获取Material资源（带缓存）
    /// </summary>
    /// <param name="materialPath">材质路径</param>
    /// <returns>Material实例</returns>
    public static Material GetMaterial(string materialPath)
    {
        if (string.IsNullOrEmpty(materialPath))
        {
            Debug.LogError("Material path cannot be null or empty");
            return null;
        }

        if (!materialCache.TryGetValue(materialPath, out Material material))
        {
            material = Resources.Load<Material>(materialPath);
            if (material == null)
            {
                Debug.LogError($"Material not found: {materialPath}");
                return null;
            }
            materialCache[materialPath] = material;
        }
        return material;
    }
    
    #endregion

    #region Sprite资源缓存方法
    
    /// <summary>
    /// 获取Sprite资源（带缓存）
    /// </summary>
    /// <param name="spritePath">精灵路径</param>
    /// <returns>Sprite实例</returns>
    public static Sprite GetSprite(string spritePath)
    {
        if (string.IsNullOrEmpty(spritePath))
        {
            Debug.LogError("Sprite path cannot be null or empty");
            return null;
        }

        if (!spriteCache.TryGetValue(spritePath, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogError($"Sprite not found: {spritePath}");
                return null;
            }
            spriteCache[spritePath] = sprite;
        }
        return sprite;
    }
    
    #endregion

    #region TextAsset资源缓存方法
    
    /// <summary>
    /// 获取TextAsset资源（带缓存）
    /// </summary>
    /// <param name="textPath">文本资源路径</param>
    /// <returns>TextAsset实例</returns>
    public static TextAsset GetTextAsset(string textPath)
    {
        if (string.IsNullOrEmpty(textPath))
        {
            Debug.LogError("Text asset path cannot be null or empty");
            return null;
        }

        if (!textAssetCache.TryGetValue(textPath, out TextAsset textAsset))
        {
            textAsset = Resources.Load<TextAsset>(textPath);
            if (textAsset == null)
            {
                Debug.LogError($"Text asset not found: {textPath}");
                return null;
            }
            textAssetCache[textPath] = textAsset;
        }
        return textAsset;
    }
    
    #endregion

    #region 通用资源加载方法
    
    /// <summary>
    /// 通用资源加载方法（带缓存）
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    /// <param name="resourcePath">资源路径</param>
    /// <returns>资源实例</returns>
    public static T LoadResource<T>(string resourcePath) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            Debug.LogError("Resource path cannot be null or empty");
            return null;
        }

        // 根据类型选择对应的缓存字典
        switch (typeof(T).Name)
        {
            case nameof(GameObject):
                return GetPrefab(resourcePath) as T;
            case nameof(Texture):
            case nameof(Texture2D):
                return GetTexture(resourcePath) as T;
            case nameof(AudioClip):
                return GetAudioClip(resourcePath) as T;
            case nameof(Material):
                return GetMaterial(resourcePath) as T;
            case nameof(Sprite):
                return GetSprite(resourcePath) as T;
            case nameof(TextAsset):
                return GetTextAsset(resourcePath) as T;
            default:
                // 对于未特殊处理的类型，使用通用加载方式
                return Resources.Load<T>(resourcePath);
        }
    }
    
    #endregion

    #region 缓存管理方法
    
    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public static void ClearAllCache()
    {
        gameObjectCache.Clear();
        textureCache.Clear();
        audioClipCache.Clear();
        materialCache.Clear();
        spriteCache.Clear();
        textAssetCache.Clear();
        
        Debug.Log("All resource caches cleared");
    }
    
    /// <summary>
    /// 清除指定类型的缓存
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    public static void ClearCache<T>() where T : UnityEngine.Object
    {
        switch (typeof(T).Name)
        {
            case nameof(GameObject):
                gameObjectCache.Clear();
                Debug.Log("GameObject cache cleared");
                break;
            case nameof(Texture):
            case nameof(Texture2D):
                textureCache.Clear();
                Debug.Log("Texture cache cleared");
                break;
            case nameof(AudioClip):
                audioClipCache.Clear();
                Debug.Log("AudioClip cache cleared");
                break;
            case nameof(Material):
                materialCache.Clear();
                Debug.Log("Material cache cleared");
                break;
            case nameof(Sprite):
                spriteCache.Clear();
                Debug.Log("Sprite cache cleared");
                break;
            case nameof(TextAsset):
                textAssetCache.Clear();
                Debug.Log("TextAsset cache cleared");
                break;
        }
    }
    
    /// <summary>
    /// 从缓存中移除指定资源
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    /// <param name="resourcePath">资源路径</param>
    public static void RemoveFromCache<T>(string resourcePath) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(resourcePath)) return;
        
        switch (typeof(T).Name)
        {
            case nameof(GameObject):
                gameObjectCache.Remove(resourcePath);
                break;
            case nameof(Texture):
            case nameof(Texture2D):
                textureCache.Remove(resourcePath);
                break;
            case nameof(AudioClip):
                audioClipCache.Remove(resourcePath);
                break;
            case nameof(Material):
                materialCache.Remove(resourcePath);
                break;
            case nameof(Sprite):
                spriteCache.Remove(resourcePath);
                break;
            case nameof(TextAsset):
                textAssetCache.Remove(resourcePath);
                break;
        }
    }
    
    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>缓存统计字符串</returns>
    public static string GetCacheStats()
    {
        return $"Cache Stats - GameObjects: {gameObjectCache.Count}, " +
               $"Textures: {textureCache.Count}, " +
               $"AudioClips: {audioClipCache.Count}, " +
               $"Materials: {materialCache.Count}, " +
               $"Sprites: {spriteCache.Count}, " +
               $"TextAssets: {textAssetCache.Count}";
    }
    
    #endregion
}