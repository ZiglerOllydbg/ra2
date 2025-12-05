using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Tilemaps;

// 1. 在右键菜单中创建该 Asset
[CreateAssetMenu(fileName = "Restricted Brush", menuName = "Brushes/Restricted Brush")]
// 2. 告诉 Unity 这是一个自定义笔刷，并隐藏默认的 GridBrush
[CustomGridBrush(false, true, false, "Restricted Brush")]
public class RestrictedBrush : GridBrush
{
    [Header("地图边界限制 (Cell Coordinates)")]
    [Tooltip("最小坐标 (包含)")]
    public Vector2Int minLimit = new Vector2Int(-15, -15);
    
    [Tooltip("最大坐标 (不包含)")]
    public Vector2Int maxLimit = new Vector2Int(15, 15);

    // -----------------------------------------------------------
    // 1. 单点绘制 / 鼠标拖动绘制 (Paint Tool)
    // -----------------------------------------------------------
    public override void Paint(GridLayout grid, GameObject brushTarget, Vector3Int position)
    {
        // 只有坐标在范围内才允许画
        if (IsPositionValid(position))
        {
            base.Paint(grid, brushTarget, position);
        }
        else
        {
            // 你可以在这里加个Debug，或者仅仅静默失败
            // Debug.LogWarning($"坐标 {position} 超出限制，无法绘制！");
        }
    }

    // -----------------------------------------------------------
    // 2. 框选填充 (Box Fill Tool)
    // -----------------------------------------------------------
    public override void BoxFill(GridLayout grid, GameObject brushTarget, BoundsInt position)
    {
        // position 是用户鼠标拉出来的框
        // 我们需要计算这个框 和 我们的限制范围 的“交集”

        int xMin = Mathf.Max(position.xMin, minLimit.x);
        int xMax = Mathf.Min(position.xMax, maxLimit.x);
        int yMin = Mathf.Max(position.yMin, minLimit.y);
        int yMax = Mathf.Min(position.yMax, maxLimit.y);
        
        // 如果交集无效 (比如用户完全画在外面了)，就不执行
        if (xMin >= xMax || yMin >= yMax)
        {
            Debug.LogWarning("框选区域完全在限制范围之外！");
            return;
        }

        // 重新构建一个被裁切过的 BoundsInt
        // Z轴通常保持默认，或者你可以限制为 0
        BoundsInt clippedBounds = new BoundsInt(
            new Vector3Int(xMin, yMin, position.zMin),
            new Vector3Int(xMax - xMin, yMax - yMin, position.size.z)
        );

        // 调用父类的 BoxFill，但是传入裁切后的范围
        base.BoxFill(grid, brushTarget, clippedBounds);
    }

    // -----------------------------------------------------------
    // 3. 辅助函数：检查单个点是否有效
    // -----------------------------------------------------------
    private bool IsPositionValid(Vector3Int pos)
    {
        return pos.x >= minLimit.x && pos.x < maxLimit.x &&
               pos.y >= minLimit.y && pos.y < maxLimit.y;
    }
    
    // -----------------------------------------------------------
    // 可选：你也可以限制“橡皮擦” (Erase)
    // 如果你不重写 Erase，用户可以擦除地图外的格子（虽然外面本来就没东西）
    // 为了严谨，也可以加上
    // -----------------------------------------------------------
    public override void Erase(GridLayout grid, GameObject brushTarget, Vector3Int position)
    {
        if (IsPositionValid(position))
        {
            base.Erase(grid, brushTarget, position);
        }
    }
}
#endif