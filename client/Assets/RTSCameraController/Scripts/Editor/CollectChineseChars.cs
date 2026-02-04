// Assets/Editor/CollectChineseChars.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 条件编译：只有在定义了TMP_PRESENT时才引入TMPro命名空间
#if TMP_PRESENT
using TMPro;
#endif

public class CollectChineseChars
{
    [MenuItem("Tools/Collect All Chinese Characters")]
    static void DoCollect()
    {
        var chars = new HashSet<char>();

        // 扫描所有已加载场景中的 UI
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            CollectFromGameObject(root, chars);
        }

        // 扫描所有 Prefab（可选，较慢）
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                CollectFromGameObject(prefab, chars);
            }
        }

        // 保存结果
        string result = new string(chars.ToArray());
        string outputPath = Application.dataPath + "/Resources/Fonts/CollectedChineseChars.txt";
        File.WriteAllText(outputPath, result);
        AssetDatabase.Refresh();
        Debug.Log($"Collected {chars.Count} Chinese characters to {outputPath}");
    }

    static void CollectFromGameObject(GameObject go, HashSet<char> chars)
    {
        // 旧版 UI.Text
        var texts = go.GetComponentsInChildren<Text>(true);
        foreach (var t in texts)
        {
            ExtractChinese(t.text, chars);
        }

        // TMP - 使用反射方式获取，兼容性更好
        CollectTMPText(go, chars);
    }

    static void CollectTMPText(GameObject go, HashSet<char> chars)
    {
#if TMP_PRESENT
        // 如果直接引用可用，优先使用直接方式
        var tmps = go.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmps)
        {
            ExtractChinese(t.text, chars);
        }
#else
        // 使用反射方式获取 TMP_Text 组件
        var tmpType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        if (tmpType != null)
        {
            var components = go.GetComponentsInChildren(tmpType, true);
            var textProperty = tmpType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            
            if (textProperty != null)
            {
                foreach (var component in components)
                {
                    var textValue = textProperty.GetValue(component) as string;
                    if (!string.IsNullOrEmpty(textValue))
                    {
                        ExtractChinese(textValue, chars);
                    }
                }
            }
        }
#endif
    }

    static void ExtractChinese(string text, HashSet<char> chars)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (char c in text)
        {
            if (c >= 0x4e00 && c <= 0x9fff)
            {
                chars.Add(c);
            }
        }
    }
}