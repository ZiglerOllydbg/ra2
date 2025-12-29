using UnityEngine;

public class OutlineComponent : MonoBehaviour
{
        [Header("Outline Settings")]
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 0.02f;
    
    public Color OutlineColor
    {
        get => outlineColor;
        set
        {
            outlineColor = value;
        }
    }
    
    public float OutlineWidth
    {
        get => outlineWidth;
        set
        {
            outlineWidth = value;
        }
    }

    private void Awake()
    {
        HideCircle();
    }
    
    private void OnEnable()
    {

    }
    
    private void OnDisable()
    {

    }
   
    /// <summary>
    /// 显示名为Circle的子组件
    /// </summary>
    public void ShowCircle()
    {
        Transform circleTransform = FindDeepChild(transform, "Circle");
        if (circleTransform != null)
        {
            circleTransform.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 隐藏名为Circle的子组件
    /// </summary>
    public void HideCircle()
    {
        Transform circleTransform = FindDeepChild(transform, "Circle");
        if (circleTransform != null)
        {
            circleTransform.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 深度查找子对象（递归查找所有层级）
    /// </summary>
    /// <param name="parent">父级变换组件</param>
    /// <param name="name">要查找的对象名称</param>
    /// <returns>找到的变换组件或null</returns>
    private Transform FindDeepChild(Transform parent, string name)
    {
        // 先在直接子对象中查找
        Transform result = parent.Find(name);
        if (result != null)
            return result;

        // 如果没找到，则递归查找每个子对象的子对象
        foreach (Transform child in parent)
        {
            result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }

        return null;
    }
}