using UnityEngine;
using TMPro;
using ZFrame;
using UnityEngine.UI;
using System;

/// <summary>
/// 主面板的小地图子面板控制器 - 封装小地图的UI逻辑和数据
/// </summary>
public class MiniMapSubPanel
{
    private GameObject root;
    private TMP_Text titleText;
    private Image mapImage;
    private TMP_Text sizeText;

    /// <summary>
/// 绑定的数据
    /// </summary>
    public MiniMapPanelData Data { get; private set; }

    public MiniMapSubPanel(Transform parent)
    {
        root = parent.Find("MiniMap")?.gameObject;
        if (root == null) return;

        // 获取组件引用
        titleText = root.transform.Find("LabelTitle")?.GetComponent<TMP_Text>();
        mapImage = root.transform.Find("Image")?.GetComponent<Image>();
        sizeText = root.transform.Find("LabelSize")?.GetComponent<TMP_Text>();

        // 初始化UI
        UpdateUI();
    }

    /// <summary>
    /// 设置数据并显示
    /// </summary>
    public void Show(MiniMapPanelData data)
    {
        Data = data;
        UpdateUI();
        SetActive(true);
    }

    /// <summary>
    /// 隐藏子面板
    /// </summary>
    public void Hide()
    {
        SetActive(false);
    }

    public bool IsVisiable()
    {
        return root != null && root.activeSelf;
    }

    /// <summary>
    /// 刷新UI显示
    /// </summary>
    public void UpdateUI()
    {
        if (Data == null) return;

        if (titleText != null) titleText.text = Data.Title;
        if (sizeText != null) sizeText.text = Data.SizeText;
    }

    /// <summary>
    /// 设置显示状态
    /// </summary>
    public void SetActive(bool active)
    {
        root?.SetActive(active);
    }

    /// <summary>
    /// 设置小地图纹理
    /// </summary>
    /// <param name="texture">小地图渲染纹理</param>
    public void SetMiniMapTexture(RenderTexture texture)
    {
        if (mapImage != null && texture != null)
        {
            // 从RenderTexture创建Texture2D
            Texture2D texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            
            // 激活并读取RenderTexture
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = texture;
            texture2D.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = previousActive;
            
            // 使用Texture2D创建Sprite
            Sprite sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
            mapImage.sprite = sprite;
        }
    }

    /// <summary>
    /// 销毁时调用，清理事件
    /// </summary>
    public void Destroy()
    {
    }
}