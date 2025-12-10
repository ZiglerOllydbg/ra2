using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using TWC.editor;

namespace TWC.Actions
{
    [ActionCategoryAttribute(Category = ActionCategoryAttribute.CategoryTypes.Generators)]
    [ActionNameAttribute(Name = "Square")] // 在菜单中显示的名字
    public class Square : TWCBlueprintAction, ITWCAction
    {
        public bool randomPosition;
        public int positionX, positionY;

        // 这里用 extent 表示“中心到边的距离”
        // 实际边长 = (extent * 2) + 1
        public int extent;

        private TWCGUILayout guiLayout;


        public ITWCAction Clone()
        {
            var _r = new Square();

            _r.extent = this.extent;
            _r.randomPosition = this.randomPosition;
            _r.positionX = this.positionX;
            _r.positionY = this.positionY;

            return _r;
        }

        public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
        {
            // Make sure to set the seed from TileWorldCreator
            UnityEngine.Random.InitState(_twc.currentSeed);

            var _position = new Vector2Int(positionX, positionY);
            var mapWidth = map.GetLength(0);
            var mapHeight = map.GetLength(1);

            if (randomPosition)
            {
                _position = new Vector2Int(Random.Range(0, mapWidth), Random.Range(0, mapHeight));
            }

            try
            {
                // 计算正方形的边界 (Bounds)
                // 使用 Mathf.Max 和 Min 防止超出地图边界导致报错
                int startX = Mathf.Max(0, _position.x - extent);
                int endX = Mathf.Min(mapWidth - 1, _position.x + extent);

                int startY = Mathf.Max(0, _position.y - extent);
                int endY = Mathf.Min(mapHeight - 1, _position.y + extent);

                // 只需要遍历正方形范围内的格子即可，比遍历全图更高效
                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        map[x, y] = true;
                    }
                }
            }
            catch { }


            return map;
        }


#if UNITY_EDITOR
        public override void DrawGUI(Rect _rect, int _layerIndex, TileWorldCreatorAsset _asset, TileWorldCreator _twc)
        {
            using (guiLayout = new TWCGUILayout(_rect))
            {
                guiLayout.Add();
                // 界面上显示清晰的标签
                extent = EditorGUI.IntField(guiLayout.rect, "Dist to Edge", extent);

                guiLayout.Add();
                randomPosition = EditorGUI.Toggle(guiLayout.rect, "Random position", randomPosition);

                if (!randomPosition)
                {
                    guiLayout.Add();
                    positionX = EditorGUI.IntField(guiLayout.rect, "Position X:", positionX);
                    guiLayout.Add();
                    positionY = EditorGUI.IntField(guiLayout.rect, "Position Y:", positionY);
                }
            }
        }
#endif

        public float GetGUIHeight()
        {
            if (guiLayout != null)
            {
                return guiLayout.height;
            }
            else
            {
                return 18;
            }
        }
    }
}