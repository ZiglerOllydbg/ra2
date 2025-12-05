#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

public class MapExporter : MonoBehaviour
{
    public Tilemap targetTilemap;
    public string savePath = "Assets/Resources/Maps/level01.bytes";

    [ContextMenu("Export Map to Binary")]
    public void ExportMap()
    {
        targetTilemap.CompressBounds(); // 压缩边界，去除空白
        BoundsInt bounds = targetTilemap.cellBounds;
        
        // 获取所有 Tile
        TileBase[] allTiles = targetTilemap.GetTilesBlock(bounds);

        using (FileStream fs = new FileStream(savePath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            // 1. 文件头 (Header)
            writer.Write("RTSMAP"); // Magic String
            writer.Write(1);        // Version
            writer.Write(bounds.size.x); // Width
            writer.Write(bounds.size.y); // Height
            
            // 2. 写入具体 Grid 数据
            for (int y = 0; y < bounds.size.y; y++)
            {
                for (int x = 0; x < bounds.size.x; x++)
                {
                    TileBase tile = allTiles[x + y * bounds.size.x];
                    
                    // 默认空数据
                    byte tType = 0;
                    byte colFlag = 0;
                    byte resType = 0;

                    if (tile is GameTile gameTile)
                    {
                        tType = gameTile.terrainType;
                        colFlag = gameTile.isWalkable ? (byte)0 : (byte)1;
                        resType = gameTile.resourceType;
                        // ... 其他属性
                    }

                    // 写入单个格子的数据
                    writer.Write(tType);
                    writer.Write(colFlag);
                    writer.Write(resType);
                }
            }
        }
        
        Debug.Log($"Map Exported: Size {bounds.size.x}x{bounds.size.y}");
        AssetDatabase.Refresh();
    }
}
#endif