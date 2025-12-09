using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using TWC; // 引用 TWC

public class TWCMapExporter : MonoBehaviour
{
    [Header("配置")]
    public TWC.TileWorldCreator twc;
    public string targetLayerName = "Base"; // 你的可行走层名字
    public string exportFileName = "MapData"; // 导出文件名

    [ContextMenu("导出二进制地图数据")]
    public void ExportToBinary()
    {
        if (twc == null)
        {
            Debug.LogError("请挂载 TWC 组件！");
            return;
        }

        // 1. 获取蓝图层
        var layer = twc.twcAsset.mapBlueprintLayers.Find(l => l.layerName == targetLayerName);
        if (layer == null)
        {
            Debug.LogError($"找不到名为 '{targetLayerName}' 的蓝图层！");
            return;
        }

        // 2. 准备路径 (导出到 Resources 方便加载，或者 StreamingAssets)
        string path = Application.dataPath + $"/Resources/{exportFileName}.bytes";
        
        // 确保目录存在
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // 3. 开始写入二进制
        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            // --- 文件头 (Header) ---
            // 写入宽度 (X)
            writer.Write(twc.twcAsset.mapWidth);
            // 写入高度 (对应 TWC 的 Z/Length)
            writer.Write(twc.twcAsset.mapHeight);
            // 写入格子大小 (float)
            writer.Write(twc.twcAsset.cellSize);

            // --- 数据体 (Body) ---
            // 按照 SimpleMapManager 的索引逻辑: index = y * width + x
            // 所以外层循环是 y (对应 TWC 的 z)，内层循环是 x
            for (int y = 0; y < twc.twcAsset.mapHeight; y++)
            {
                for (int x = 0; x < twc.twcAsset.mapWidth; x++)
                {
                    // 获取 TWC 数据
                    bool isWalkable = layer.map[x, y];
                    
                    // 写入 1 (true) 或 0 (false)
                    // 使用 byte 存储以节省空间 (比 bool 数组默认序列化更紧凑)
                    writer.Write(isWalkable ? (byte)1 : (byte)0);
                }
            }
        }

        Debug.Log($"<color=green>地图数据导出成功！</color>\n路径: {path}\n大小: {twc.twcAsset.mapWidth}x{twc.twcAsset.mapHeight}");
        
#if UNITY_EDITOR
        AssetDatabase.Refresh(); // 刷新资源以便 Unity 识别新文件
#endif
    }
}