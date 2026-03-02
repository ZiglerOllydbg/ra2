using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using TMPro;
using System.Collections.Generic;
using ZLockstep.Simulation.ECS.Components;
using ZLockstep.Simulation.ECS;
using ZLockstep.View;
using ZLib;

/// <summary>
/// 血量面板 - 显示单位血量信息
/// </summary>
[UIModel(
    panelID = "HealthPanel",
    panelPath = "HealthPanel",
    panelName = "血量面板",
    panelUIDepthType = ClientUIDepthTypeID.GameTop
)]
public class HealthPanel : BasePanel
{
    // 血量条预制体引用
    private GameObject greenHpPrefab;
    private GameObject redHpPrefab;
    
    // 存储所有血量条实例的字典，key为实体ID
    private Dictionary<int, HealthBarInstance> healthBars = new Dictionary<int, HealthBarInstance>();

    private int _ticker = 0;
    
    // 血量条实例类
    private class HealthBarInstance
    {
        public GameObject gameObject;
        public Image fillImage;
        public int entityId;
        public bool isSelf;
    }
    
    public HealthPanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew) 
        : base(_processor, _modelData, _disableNew)
    {
    }

    protected override void OnBecameVisible()
    {
        base.OnBecameVisible();
        
        // 获取血量条预制体引用
        greenHpPrefab = PanelObject.transform.Find("GreenHp")?.gameObject;
        redHpPrefab = PanelObject.transform.Find("RedHp")?.gameObject;
        
        // 确保预制体存在且初始隐藏
        if (greenHpPrefab != null)
        {
            greenHpPrefab.SetActive(false);
        }
        if (redHpPrefab != null)
        {
            redHpPrefab.SetActive(false);
        }

        _ticker = Tick.SetInterval(() => {
            UpdateAllHealthBars();
        }, 0.05f);
    }

    protected override void AddEvent()
    {
        base.AddEvent();
        // 可以在这里添加事件监听
    }

    protected override void RemoveEvent()
    {
        base.RemoveEvent();
        // 可以在这里移除事件监听
    }
    
    /// <summary>
    /// 更新指定实体的血量显示
    /// </summary>
    /// <param name="entityId">实体ID</param>
    /// <param name="isSelf">是否为己方</param>
    /// <param name="currentHealth">当前血量</param>
    /// <param name="maxHealth">最大血量</param>
    public void UpdateHealth(int entityId, bool isSelf)
    {
        // 查找或创建血量条实例
        HealthBarInstance healthBarInstance;
        if (!healthBars.TryGetValue(entityId, out healthBarInstance))
        {
            // 创建新的血量条实例
            healthBarInstance = CreateHealthBar(entityId, isSelf);
            healthBars[entityId] = healthBarInstance;
        }
        
        // 更新血量显示
        if (healthBarInstance.fillImage != null)
        {
            healthBarInstance.fillImage.fillAmount = 1.0f;
        }
    }

    public void UpdateAllHealthBars()
    {
        foreach (var healthBar in healthBars.Values)
        {
            UpdateHealthBar(healthBar.entityId);
        }
    }

    /// <summary>
    /// 更新血量条的位置
    /// </summary>
    public void UpdateHealthBar(int entityId)
    {
        Ra2Demo ra2Demo = GameObject.FindObjectOfType<Ra2Demo>();
        if (ra2Demo == null)
        {
            return;
        }

        ComponentManager componentManager = ra2Demo.GetBattleGame().World.ComponentManager;

        Entity entity = new Entity(entityId);
        if (!componentManager.HasComponent<TransformComponent>(entity))
        {
            return;
        }

        var transformComponent = componentManager.GetComponent<TransformComponent>(entity);
        Vector3 worldPosition = transformComponent.Position.ToVector3();

        // 获取主相机，将 3D 坐标转换为屏幕坐标
        Camera mainCamera = Camera.main;
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

        // 获取单位血量组件
        if (!componentManager.HasComponent<HealthComponent>(entity))
        {
            return;
        }
        var healthComponent = componentManager.GetComponent<HealthComponent>(entity);

        if (healthBars.TryGetValue(entityId, out HealthBarInstance healthBarInstance))
        {
            if (healthBarInstance.gameObject != null)
            {
                // 添加向上的偏移量，使血条显示在单位上方
                float offsetY = 50f; // 向上偏移 50 像素
                healthBarInstance.gameObject.transform.position = new Vector3(screenPosition.x, screenPosition.y + offsetY, 0);
            }

            if (healthBarInstance.fillImage != null)
            {
                healthBarInstance.fillImage.fillAmount = healthComponent.HealthPercent.ToFloat();
            }
        }
    }
    
    /// <summary>
    /// 隐藏指定实体的血量条
    /// </summary>
    /// <param name="entityId">实体ID</param>
    public void HideHealthBar(int entityId)
    {
        if (healthBars.TryGetValue(entityId, out HealthBarInstance healthBarInstance))
        {
            if (healthBarInstance.gameObject != null)
            {
                healthBarInstance.gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// 显示指定实体的血量条
    /// </summary>
    /// <param name="entityId">实体ID</param>
    public void ShowHealthBar(int entityId)
    {
        if (healthBars.TryGetValue(entityId, out HealthBarInstance healthBarInstance))
        {
            if (healthBarInstance.gameObject != null)
            {
                healthBarInstance.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary>
    /// 创建血量条实例
    /// </summary>
    /// <param name="entityId">实体ID</param>
    /// <param name="isSelf">是否为己方</param>
    /// <returns>血量条实例</returns>
    private HealthBarInstance CreateHealthBar(int entityId, bool isSelf)
    {
        // 选择对应的预制体
        GameObject prefab = isSelf ? redHpPrefab : greenHpPrefab;
        if (prefab == null)
        {
            Debug.LogError($"血量条预制体未找到！isSelf: {isSelf}");
            return null;
        }
        
        // 创建实例
        GameObject healthBarGO = GameObject.Instantiate(prefab, PanelObject.transform);
        healthBarGO.name = $"HealthBar_{(isSelf ? "Green" : "Red")}_{entityId}";
        healthBarGO.SetActive(true);
        
        // 获取Fill图像组件
        Image fillImage = healthBarGO.transform.Find("Fill")?.GetComponent<Image>();
        if (fillImage == null)
        {
            Debug.LogError("无法找到Fill图像组件！");
        }
        
        // 创建实例对象
        HealthBarInstance instance = new HealthBarInstance
        {
            gameObject = healthBarGO,
            fillImage = fillImage,
            entityId = entityId,
            isSelf = isSelf
        };
        
        return instance;
    }
    
    /// <summary>
    /// 清理所有血量条
    /// </summary>
    public void ClearAllHealthBars()
    {
        foreach (var kvp in healthBars)
        {
            if (kvp.Value.gameObject != null)
            {
                GameObject.Destroy(kvp.Value.gameObject);
            }
        }
        healthBars.Clear();
    }
    
    protected override void OnBecameInvisible()
    {
        base.OnBecameInvisible();
        // 面板隐藏时清理所有血量条
        ClearAllHealthBars();

        // 停止计时器
        Tick.ClearInterval(_ticker);
        _ticker = 0;
    }
}