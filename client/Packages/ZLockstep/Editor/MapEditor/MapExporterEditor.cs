using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

[CustomEditor(typeof(MapExporter))]
public class MapExporterEditor : Editor
{
    private SerializedProperty targetTilemapProp;
    private SerializedProperty savePathProp;
    
    // 预览信息
    private int previewWidth;
    private int previewHeight;
    private int previewTileCount;
    private bool showPreview = true;

    private void OnEnable()
    {
        targetTilemapProp = serializedObject.FindProperty("targetTilemap");
        savePathProp = serializedObject.FindProperty("savePath");
        UpdatePreviewInfo();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        MapExporter exporter = (MapExporter)target;
        
        // 标题
        EditorGUILayout.Space(5);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("地图导出器", titleStyle);
        EditorGUILayout.Space(5);
        
        DrawSeparator();
        
        // 目标 Tilemap
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("目标设置", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(targetTilemapProp, new GUIContent("目标 Tilemap"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            UpdatePreviewInfo();
        }
        
        // 快捷选择按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("从场景选择", GUILayout.Width(100)))
        {
            SelectTilemapFromScene(exporter);
        }
        if (GUILayout.Button("选择子对象", GUILayout.Width(100)))
        {
            SelectTilemapFromChildren(exporter);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 保存路径
        EditorGUILayout.LabelField("导出设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(savePathProp, new GUIContent("保存路径"));
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string currentPath = savePathProp.stringValue;
            string selectedPath = EditorUtility.SaveFilePanel(
                "选择保存位置",
                Path.GetDirectoryName(currentPath),
                Path.GetFileName(currentPath),
                "bytes"
            );
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    savePathProp.stringValue = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    savePathProp.stringValue = selectedPath;
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        DrawSeparator();
        
        // 预览信息折叠
        EditorGUILayout.Space(5);
        showPreview = EditorGUILayout.Foldout(showPreview, "地图信息预览", true);
        
        if (showPreview)
        {
            if (exporter.targetTilemap != null)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"宽度: {previewWidth} 格");
                EditorGUILayout.LabelField($"高度: {previewHeight} 格");
                EditorGUILayout.LabelField($"总格子数: {previewWidth * previewHeight}");
                EditorGUILayout.LabelField($"有效 Tile 数: {previewTileCount}");
                EditorGUILayout.LabelField($"预计文件大小: ~{EstimateFileSize()} bytes");
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
                
                if (GUILayout.Button("刷新预览"))
                {
                    UpdatePreviewInfo();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请先选择目标 Tilemap", MessageType.Info);
            }
        }
        
        EditorGUILayout.Space(10);
        DrawSeparator();
        EditorGUILayout.Space(10);
        
        // 导出按钮
        GUI.enabled = exporter.targetTilemap != null;
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        GUIStyle exportButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 40
        };
        
        if (GUILayout.Button("导 出 地 图", exportButtonStyle))
        {
            exporter.ExportMap();
            UpdatePreviewInfo();
        }
        
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        EditorGUILayout.Space(5);
        
        // 辅助按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("打开导出目录"))
        {
            string directory = Path.GetDirectoryName(exporter.savePath);
            if (Directory.Exists(directory))
            {
                EditorUtility.RevealInFinder(exporter.savePath);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "目录不存在，请先导出一次", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
        
        serializedObject.ApplyModifiedProperties();
    }

    private void UpdatePreviewInfo()
    {
        MapExporter exporter = (MapExporter)target;
        
        if (exporter.targetTilemap == null)
        {
            previewWidth = 0;
            previewHeight = 0;
            previewTileCount = 0;
            return;
        }
        
        exporter.targetTilemap.CompressBounds();
        BoundsInt bounds = exporter.targetTilemap.cellBounds;
        previewWidth = bounds.size.x;
        previewHeight = bounds.size.y;
        
        TileBase[] allTiles = exporter.targetTilemap.GetTilesBlock(bounds);
        previewTileCount = 0;
        foreach (var tile in allTiles)
        {
            if (tile != null) previewTileCount++;
        }
    }

    private int EstimateFileSize()
    {
        // Header: "RTSMAP"(6) + Version(4) + Width(4) + Height(4) = 18
        // Per tile: tType(1) + colFlag(1) + resType(1) = 3
        int headerSize = 18;
        int tileDataSize = previewWidth * previewHeight * 3;
        return headerSize + tileDataSize;
    }

    private void SelectTilemapFromScene(MapExporter exporter)
    {
        Tilemap[] tilemaps = FindObjectsOfType<Tilemap>();
        
        if (tilemaps.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "场景中没有找到 Tilemap", "确定");
            return;
        }
        
        if (tilemaps.Length == 1)
        {
            Undo.RecordObject(exporter, "Set Tilemap");
            exporter.targetTilemap = tilemaps[0];
            EditorUtility.SetDirty(exporter);
            UpdatePreviewInfo();
            return;
        }
        
        GenericMenu menu = new GenericMenu();
        foreach (var tilemap in tilemaps)
        {
            string path = GetGameObjectPath(tilemap.gameObject);
            Tilemap tm = tilemap; // 闭包捕获
            menu.AddItem(new GUIContent(path), false, () =>
            {
                Undo.RecordObject(exporter, "Set Tilemap");
                exporter.targetTilemap = tm;
                EditorUtility.SetDirty(exporter);
                UpdatePreviewInfo();
            });
        }
        menu.ShowAsContext();
    }

    private void SelectTilemapFromChildren(MapExporter exporter)
    {
        Tilemap[] tilemaps = exporter.GetComponentsInChildren<Tilemap>();
        
        if (tilemaps.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "子对象中没有找到 Tilemap", "确定");
            return;
        }
        
        if (tilemaps.Length == 1)
        {
            Undo.RecordObject(exporter, "Set Tilemap");
            exporter.targetTilemap = tilemaps[0];
            EditorUtility.SetDirty(exporter);
            UpdatePreviewInfo();
            return;
        }
        
        GenericMenu menu = new GenericMenu();
        foreach (var tilemap in tilemaps)
        {
            string name = tilemap.gameObject.name;
            Tilemap tm = tilemap;
            menu.AddItem(new GUIContent(name), false, () =>
            {
                Undo.RecordObject(exporter, "Set Tilemap");
                exporter.targetTilemap = tm;
                EditorUtility.SetDirty(exporter);
                UpdatePreviewInfo();
            });
        }
        menu.ShowAsContext();
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(2);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(2);
    }
}

