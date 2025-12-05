using UnityEngine;

public class MapAreaVisualizer : MonoBehaviour
{
    [Header("区域设置")]
    [Tooltip("地图的宽(X)和高(Z)")]
    public Vector2 areaSize = new Vector2(300, 300);

    [Header("显示设置")]
    public Color wireColor = Color.yellow;      // 边框颜色
    [Range(0, 1)] 
    public float fillAlpha = 0.2f;              // 填充透明度
    [Header("是否只在选中该物体时显示")]
    public bool showOnlyWhenSelected = false;   // 是否只在选中该物体时显示

    // 当物体被选中时绘制（无论 showOnlyWhenSelected 是否开启，选中时通常都希望能看到）
    private void OnDrawGizmosSelected()
    {
        if (showOnlyWhenSelected)
        {
            DrawMapGizmo();
        }
    }

    // 常驻绘制（如果不选中物体也能看到）
    private void OnDrawGizmos()
    {
        if (!showOnlyWhenSelected)
        {
            DrawMapGizmo();
        }
    }

    private void DrawMapGizmo()
    {
        // 1. 设置颜色
        Gizmos.color = wireColor;

        // 2. 确定中心点（以当前挂载脚本的物体为中心）
        Vector3 center = transform.position;

        // 3. 确定尺寸
        // 注意：因为是 XZ 平面，所以 Vector3 的 y 轴设为一个很小的值（比如 1 或 0.1），
        // areaSize.y 实际上对应的是 3D 世界的 Z 轴
        Vector3 size = new Vector3(areaSize.x, 1f, areaSize.y);

        // 4. 绘制线框（边框）
        Gizmos.DrawWireCube(center, size);

        // 5. 绘制半透明填充（可选，方便看区域覆盖）
        Gizmos.color = new Color(wireColor.r, wireColor.g, wireColor.b, fillAlpha);
        Gizmos.DrawCube(center, size);
    }
}