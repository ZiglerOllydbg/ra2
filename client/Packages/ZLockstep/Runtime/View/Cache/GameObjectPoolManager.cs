using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameObject 对象池管理器
/// 负责管理 GameObject 的实例化和回收，提高性能
/// </summary>
public static class GameObjectPoolManager
{
    #region 数据结构
    
    /// <summary>
    /// GameObject 对象池数据结构
    /// </summary>
    public class GameObjectPool
    {
        /// <summary>
        /// 可用对象队列
        /// </summary>
        public Queue<GameObject> availableObjects = new Queue<GameObject>();
        
        /// <summary>
        /// 使用中对象列表
        /// </summary>
        public List<GameObject> activeObjects = new List<GameObject>();
        
        /// <summary>
        /// 预制体引用
        /// </summary>
        public GameObject prefab;
        
        /// <summary>
        /// 父节点 Transform
        /// </summary>
        public Transform parent;
        
        /// <summary>
        /// 初始容量
        /// </summary>
        public int initialCapacity;
        
        /// <summary>
        /// 最大容量
        /// </summary>
        public int maxCapacity;
        
        /// <summary>
        /// 当前总对象数
        /// </summary>
        public int TotalCount => availableObjects.Count + activeObjects.Count;
    }
    
    #endregion
    
    #region 成员变量
    
    /// <summary>
    /// GameObject 对象池字典
    /// </summary>
    private static readonly Dictionary<string, GameObjectPool> gameObjectPools = new Dictionary<string, GameObjectPool>();
    
    /// <summary>
    /// GameObject 到预制体路径的映射字典（用于回收时查找对应的池）
    /// </summary>
    private static readonly Dictionary<GameObject, string> gameObjectToPathMap = new Dictionary<GameObject, string>();
    
    /// <summary>
    /// 对象池默认初始容量
    /// </summary>
    public const int DEFAULT_POOL_INITIAL_CAPACITY = 5;
    
    /// <summary>
    /// 对象池最大容量
    /// </summary>
    public const int DEFAULT_POOL_MAX_CAPACITY = 500;
    
    #endregion
    
    #region 对象池获取与创建
    
    /// <summary>
    /// 从对象池获取 GameObject
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <param name="parent">父对象</param>
    /// <param name="position">位置</param>
    /// <param name="rotation">旋转</param>
    /// <returns>GameObject 实例</returns>
    public static GameObject GetFromPool(string prefabPath, Transform parent = null, Vector3? position = null, Quaternion? rotation = null)
    {
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("Prefab path cannot be null or empty");
            return null;
        }

        // 如果对象池不存在，自动创建
        if (!gameObjectPools.TryGetValue(prefabPath, out GameObjectPool pool))
        {
            CreatePool(prefabPath, parent);
            gameObjectPools.TryGetValue(prefabPath, out pool);
        }

        if (pool == null)
        {
            Debug.LogError($"Failed to create pool for: {prefabPath}");
            return null;
        }

        GameObject obj;

        // 从池中获取可用对象
        if (pool.availableObjects.Count > 0)
        {
            obj = pool.availableObjects.Dequeue();
        }
        else
        {
            // 池为空，创建新对象（如果未达到上限）
            if (pool.TotalCount >= pool.maxCapacity)
            {
                Debug.LogWarning($"Object pool capacity exceeded for: {prefabPath}. Consider increasing max capacity.");
                return null;
            }

            obj = GameObject.Instantiate(pool.prefab);
        }

        // 设置对象的父节点、位置和旋转
        obj.transform.SetParent(parent ?? pool.parent);
        obj.transform.position = position ?? Vector3.zero;
        obj.transform.rotation = rotation ?? Quaternion.identity;

        // 激活对象并加入使用中列表
        obj.SetActive(true);
        pool.activeObjects.Add(obj);

        // 记录对象到池的映射（用于回收）
        gameObjectToPathMap[obj] = prefabPath;

        return obj;
    }
    
    /// <summary>
    /// 创建对象池
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <param name="parent">父对象</param>
    /// <param name="initialCapacity">初始容量</param>
    /// <param name="maxCapacity">最大容量</param>
    /// <returns>是否创建成功</returns>
    public static bool CreatePool(string prefabPath, Transform parent = null, int initialCapacity = DEFAULT_POOL_INITIAL_CAPACITY, int maxCapacity = DEFAULT_POOL_MAX_CAPACITY)
    {
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("Prefab path cannot be null or empty");
            return false;
        }

        if (gameObjectPools.ContainsKey(prefabPath))
        {
            Debug.LogWarning($"Pool already exists for: {prefabPath}");
            return false;
        }

        // 加载预制体（使用 ResourceCache）
        GameObject prefab = ResourceCache.GetPrefab(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Failed to load prefab for pool: {prefabPath}");
            return false;
        }

        // 创建对象池
        GameObjectPool pool = new GameObjectPool
        {
            prefab = prefab,
            parent = parent,
            initialCapacity = initialCapacity,
            maxCapacity = maxCapacity
        };

        // 预创建初始对象
        GameObject poolParent = new GameObject($"Pool_{prefabPath}");
        poolParent.transform.SetParent(parent);
        pool.parent = poolParent.transform;

        for (int i = 0; i < initialCapacity; i++)
        {
            GameObject obj = GameObject.Instantiate(prefab, pool.parent);
            obj.SetActive(false);
            pool.availableObjects.Enqueue(obj);
            // 预创建的物体不需要记录映射，因为它们还没被使用
        }

        gameObjectPools[prefabPath] = pool;

        Debug.Log($"Created object pool for {prefabPath} with initial capacity {initialCapacity}, max capacity {maxCapacity}");
        return true;
    }
    
    #endregion
    
    #region 对象回收
    
    /// <summary>
    /// 回收 GameObject 到对象池
    /// </summary>
    /// <param name="obj">要回收的对象</param>
    /// <param name="delay">延迟回收时间（秒），<=0 表示立即回收</param>
    public static void ReturnToPool(GameObject obj, float delay = 0f)
    {
        if (obj == null)
        {
            Debug.LogError("Cannot return null object to pool");
            return;
        }

        // 从映射字典中获取预制体路径
        if (!gameObjectToPathMap.TryGetValue(obj, out string prefabPath))
        {
            Debug.LogWarning($"Object {obj.name} is not tracked by any pool. Destroying instead.");
            GameObject.Destroy(obj);
            return;
        }

        // 查找对应的对象池
        if (!gameObjectPools.TryGetValue(prefabPath, out GameObjectPool pool))
        {
            Debug.LogWarning($"Pool not found for: {prefabPath}. Destroying object instead.");
            GameObject.Destroy(obj);
            return;
        }

        // 从中列表中移除
        if (!pool.activeObjects.Remove(obj))
        {
            Debug.LogWarning($"Object {obj.name} was not in active list. It may have been returned already.");
            return;
        }

        // 延迟回收或立即回收
        if (delay > 0f)
        {
            DelayedReturnToPool(obj, pool, delay);
        }
        else
        {
            ImmediateReturnToPool(obj, pool);
        }
    }
    
    /// <summary>
    /// 立即回收对象到池
    /// </summary>
    private static void ImmediateReturnToPool(GameObject obj, GameObjectPool pool)
    {
        obj.SetActive(false);
        obj.transform.SetParent(pool.parent);
        pool.availableObjects.Enqueue(obj);
        
        // 从映射字典中移除
        gameObjectToPathMap.Remove(obj);
        
        Debug.Log($"Returned {obj.name} to pool. Available: {pool.availableObjects.Count}, Active: {pool.activeObjects.Count}");
    }
    
    /// <summary>
    /// 延迟回收对象到池
    /// </summary>
    private static async void DelayedReturnToPool(GameObject obj, GameObjectPool pool, float delay)
    {
        await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
        
        if (obj != null && pool.activeObjects.Contains(obj))
        {
            ImmediateReturnToPool(obj, pool);
        }
        else if (obj == null)
        {
            // 对象已被销毁，从映射中移除
            gameObjectToPathMap.Remove(obj);
        }
    }
    
    #endregion
    
    #region 对象池预热与管理
    
    /// <summary>
    /// 预创建对象池中的对象
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <param name="count">预创建数量</param>
    public static void PrewarmPool(string prefabPath, int count)
    {
        if (!gameObjectPools.TryGetValue(prefabPath, out GameObjectPool pool))
        {
            Debug.LogWarning($"Pool not found for: {prefabPath}. Creating pool first.");
            CreatePool(prefabPath);
            gameObjectPools.TryGetValue(prefabPath, out pool);
        }

        if (pool == null) return;

        int createdCount = 0;
        while (createdCount < count && pool.TotalCount < pool.maxCapacity)
        {
            GameObject obj = GameObject.Instantiate(pool.prefab, pool.parent);
            obj.SetActive(false);
            pool.availableObjects.Enqueue(obj);
            // 预热的物体不需要记录映射，因为它们还没被使用
            createdCount++;
        }

        Debug.Log($"Prewarmed {createdCount} objects for pool: {prefabPath}");
    }
    
    /// <summary>
    /// 清空对象池
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <param name="destroyAll">是否销毁所有对象（包括使用中的）</param>
    public static void ClearPool(string prefabPath, bool destroyAll = false)
    {
        if (!gameObjectPools.TryGetValue(prefabPath, out GameObjectPool pool))
        {
            Debug.LogWarning($"Pool not found for: {prefabPath}");
            return;
        }

        // 销毁可用对象
        foreach (GameObject obj in pool.availableObjects)
        {
            if (obj != null)
            {
                // 从映射中移除
                gameObjectToPathMap.Remove(obj);
                GameObject.Destroy(obj);
            }
        }
        pool.availableObjects.Clear();

        // 如果需要销毁所有对象（包括使用中的）
        if (destroyAll)
        {
            foreach (GameObject obj in pool.activeObjects)
            {
                if (obj != null)
                {
                    // 从映射中移除
                    gameObjectToPathMap.Remove(obj);
                    GameObject.Destroy(obj);
                }
            }
            pool.activeObjects.Clear();
        }
        else
        {
            // 警告还有对象在使用中
            if (pool.activeObjects.Count > 0)
            {
                Debug.LogWarning($"Clearing pool {prefabPath} but {pool.activeObjects.Count} objects are still active. " +
                               $"They will not be destroyed. Set destroyAll=true to force destroy.");
            }
        }

        // 销毁池父节点
        if (pool.parent != null)
        {
            GameObject.Destroy(pool.parent.gameObject);
        }

        gameObjectPools.Remove(prefabPath);

        Debug.Log($"Cleared object pool for: {prefabPath}");
    }
    
    /// <summary>
    /// 清空所有对象池
    /// </summary>
    /// <param name="destroyAll">是否销毁所有对象（包括使用中的）</param>
    public static void ClearAllPools(bool destroyAll = false)
    {
        List<string> poolPaths = new List<string>(gameObjectPools.Keys);
        foreach (string path in poolPaths)
        {
            ClearPool(path, destroyAll);
        }

        Debug.Log("All object pools cleared");
    }
    
    #endregion
    
    #region 统计信息与查询
    
    /// <summary>
    /// 获取对象池统计信息
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <returns>统计信息字符串</returns>
    public static string GetPoolStats(string prefabPath)
    {
        if (!gameObjectPools.TryGetValue(prefabPath, out GameObjectPool pool))
        {
            return $"Pool not found for: {prefabPath}";
        }

        return $"Pool Stats for {prefabPath} - " +
               $"Available: {pool.availableObjects.Count}, " +
               $"Active: {pool.activeObjects.Count}, " +
               $"Total: {pool.TotalCount}/{pool.maxCapacity}";
    }
    
    /// <summary>
    /// 获取所有对象池的统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public static string GetAllPoolStats()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Object Pool Statistics ===");
        
        foreach (var kvp in gameObjectPools)
        {
            GameObjectPool pool = kvp.Value;
            sb.AppendLine($"Pool: {kvp.Key}");
            sb.AppendLine($"  Available: {pool.availableObjects.Count}, Active: {pool.activeObjects.Count}, Total: {pool.TotalCount}/{pool.maxCapacity}");
        }
        
        sb.AppendLine($"Total Pools: {gameObjectPools.Count}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 检查对象是否在池中
    /// </summary>
    /// <param name="obj">要检查的对象</param>
    /// <returns>是否在池中</returns>
    public static bool IsPooledObject(GameObject obj)
    {
        if (obj == null) return false;
        
        return gameObjectToPathMap.ContainsKey(obj);
    }
    
    /// <summary>
    /// 调整对象池的最大容量
    /// </summary>
    /// <param name="prefabPath">预制体路径</param>
    /// <param name="newMaxCapacity">新的最大容量</param>
    public static void ResizePool(string prefabPath, int newMaxCapacity)
    {
        if (!gameObjectPools.TryGetValue(prefabPath, out GameObjectPool pool))
        {
            Debug.LogWarning($"Pool not found for: {prefabPath}");
            return;
        }

        if (newMaxCapacity <= 0)
        {
            Debug.LogError("New max capacity must be greater than 0");
            return;
        }

        int oldMaxCapacity = pool.maxCapacity;
        pool.maxCapacity = newMaxCapacity;

        Debug.Log($"Resized pool {prefabPath} max capacity from {oldMaxCapacity} to {newMaxCapacity}");
    }
    
    #endregion
}
