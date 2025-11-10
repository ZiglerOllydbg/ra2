using UnityEngine;

public class OutlineComponent : MonoBehaviour
{
    [Header("Outline Settings")]
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 0.02f;
    
    // 存储原始材质，用于恢复
    private Material[] _originalMaterials;
    // 描边材质
    private static Material _sharedOutlineMaterial;
    
    public Color OutlineColor
    {
        get => outlineColor;
        set
        {
            outlineColor = value;
            UpdateOutlineMaterial();
        }
    }
    
    public float OutlineWidth
    {
        get => outlineWidth;
        set
        {
            outlineWidth = value;
            UpdateOutlineMaterial();
        }
    }
    
    private void Awake()
    {
        // 初始化共享描边材质（如果尚未创建）
        if (_sharedOutlineMaterial == null)
        {
            // 创建一个简单的发光材质作为描边
            _sharedOutlineMaterial = new Material(Shader.Find("Standard"));
            _sharedOutlineMaterial.SetColor("_EmissionColor", outlineColor);
            _sharedOutlineMaterial.SetFloat("_Metallic", 0f);
            _sharedOutlineMaterial.SetFloat("_Glossiness", 0f);
            _sharedOutlineMaterial.EnableKeyword("_EMISSION");
        }
    }
    
    private void UpdateOutlineMaterial()
    {
        if (_sharedOutlineMaterial != null)
        {
            _sharedOutlineMaterial.SetColor("_EmissionColor", outlineColor * 2); // 增强发光效果
        }
    }
    
    private void OnEnable()
    {
        ApplyOutlineMaterial();
    }
    
    private void OnDisable()
    {
        RestoreOriginalMaterials();
    }
    
    private void ApplyOutlineMaterial()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        
        // 保存原始材质
        _originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                _originalMaterials[i] = renderers[i].material;
                // 应用描边材质
                if (_sharedOutlineMaterial != null)
                {
                    renderers[i].material = _sharedOutlineMaterial;
                }
            }
        }
    }
    
    private void RestoreOriginalMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0 || _originalMaterials == null) return;
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (i < _originalMaterials.Length && renderers[i] != null && _originalMaterials[i] != null)
            {
                renderers[i].material = _originalMaterials[i];
            }
        }
        
        _originalMaterials = null;
    }
}