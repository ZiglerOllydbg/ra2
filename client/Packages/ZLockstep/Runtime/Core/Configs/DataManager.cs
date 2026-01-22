using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class DataManager
{
    // 缓存已加载的数据
    private static Dictionary<string, object> _cachedData = new Dictionary<string, object>();

    /// <summary>
    /// 根据ID和类型获取配置数据
    /// </summary>
    /// <typeparam name="T">配置类类型</typeparam>
    /// <param name="id">配置ID</param>
    /// <returns>配置实例</returns>
    public static T Get<T>(string id) where T : class
    {
        Type type = typeof(T);
        string cacheKey = type.FullName;
        
        // 检查是否已经加载了对应类型的数据
        if (!_cachedData.ContainsKey(cacheKey))
        {
            LoadDataForType<T>();
        }

        var typeCache = _cachedData[cacheKey] as Dictionary<string, T>;
        
        if (typeCache != null && typeCache.ContainsKey(id))
        {
            return typeCache[id];
        }
        
        return null;
    }
    
    /// <summary>
    /// 根据条件查询单个实例
    /// </summary>
    /// <typeparam name="T">配置类类型</typeparam>
    /// <param name="predicate">查询条件函数</param>
    /// <returns>符合条件的第一个实例</returns>
    public static T GetBy<T>(Func<T, bool> predicate) where T : class
    {
        // 加载数据（如果尚未加载）
        Type type = typeof(T);
        string cacheKey = type.FullName;
        
        if (!_cachedData.ContainsKey(cacheKey))
        {
            LoadDataForType<T>();
        }

        var typeCache = _cachedData[cacheKey] as Dictionary<string, T>;
        
        if (typeCache != null)
        {
            foreach (var kvp in typeCache)
            {
                if (predicate(kvp.Value))
                {
                    return kvp.Value;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// 根据多条件查询单个实例
    /// </summary>
    /// <typeparam name="T">配置类类型</typeparam>
    /// <param name="predicates">多个查询条件函数</param>
    /// <returns>符合条件的第一个实例</returns>
    public static T GetBy<T>(params Func<T, bool>[] predicates) where T : class
    {
        return GetListBy<T>(predicates).FirstOrDefault();
    }

    /// <summary>
    /// 根据条件查询实例列表
    /// </summary>
    /// <typeparam name="T">配置类类型</typeparam>
    /// <param name="predicate">查询条件函数</param>
    /// <returns>符合条件的实例列表</returns>
    public static List<T> GetListBy<T>(Func<T, bool> predicate) where T : class
    {
        var result = new List<T>();
        
        Type type = typeof(T);
        string cacheKey = type.FullName;
        
        if (!_cachedData.ContainsKey(cacheKey))
        {
            LoadDataForType<T>();
        }

        var typeCache = _cachedData[cacheKey] as Dictionary<string, T>;
        
        if (typeCache != null)
        {
            foreach (var kvp in typeCache)
            {
                if (predicate(kvp.Value))
                {
                    result.Add(kvp.Value);
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// 根据多条件查询实例列表
    /// </summary>
    /// <typeparam name="T">配置类类型</typeparam>
    /// <param name="predicates">多个查询条件函数</param>
    /// <returns>符合条件的实例列表</returns>
    public static List<T> GetListBy<T>(params Func<T, bool>[] predicates) where T : class
    {
        return GetListBy<T>((T item) => {
            foreach (var predicate in predicates)
            {
                if (!predicate(item))
                {
                    return false;
                }
            }
            return true;
        });
    }
    
    private static void LoadDataForType<T>() where T : class
    {
        Type type = typeof(T);
        
        // 通过反射获取JsonFileName常量值
        FieldInfo jsonFileNameField = type.GetField("JsonFileName", BindingFlags.Public | BindingFlags.Static);
        if (jsonFileNameField == null)
        {
            Debug.LogError($"类型 {type.Name} 不包含 JsonFileName 常量");
            return;
        }
        
        string jsonFileName = jsonFileNameField.GetValue(null) as string;
        if (string.IsNullOrEmpty(jsonFileName))
        {
            Debug.LogError($"类型 {type.Name} 的 JsonFileName 常量为空");
            return;
        }
        
        // 移除.json扩展名以便Resources加载
        string fileNameWithoutExt = jsonFileName.EndsWith(".json") ? 
            jsonFileName.Substring(0, jsonFileName.Length - 5) : jsonFileName;
        string resourcePath = $"Data/Json/{fileNameWithoutExt}";
        
        // 加载JSON文件
        TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
        if (textAsset == null)
        {
            Debug.LogError($"无法找到资源: {resourcePath}");
            return;
        }
        
        // 解析JSON数据
        ParseAndCacheData<T>(textAsset.text, type);
    }
    
    private static void ParseAndCacheData<T>(string jsonString, Type type) where T : class
    {
        // 使用Newtonsoft.Json解析完整JSON结构
        JObject jsonObject = JObject.Parse(jsonString);
        
        // 获取类型名称作为数据的key
        string typeName = type.Name;
        
        if (jsonObject.ContainsKey(typeName))
        {
            var typeData = (JObject)jsonObject[typeName];
            var result = new Dictionary<string, T>();
            
            foreach (var kvp in typeData)
            {
                string id = kvp.Key;
                var objData = kvp.Value;
                
                // 将JToken转换为T类型的对象
                T obj = objData.ToObject<T>() as T;
                result[id] = obj;
            }
            
            _cachedData[type.FullName] = result;
        }
    }
}