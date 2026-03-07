using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using ZFrame;

/// <summary>
/// 设置面板的子面板控制器 - 封装设置面板的 UI 逻辑和数据
/// </summary>
public class SettingSubPanel
{
    private GameObject root;
    private TMP_Text titleText;
    private Button closeBtn;
    
    /// <summary>
    /// 绑定的数据
    /// </summary>
    public SubPanelData Data { get; private set; }
    
    /// <summary>
    /// 关闭按钮点击回调（可选，用于通知父面板）
    /// </summary>
    public System.Action OnCloseClick;
    
    // 直接引用 UI 元素（不使用模板）
    private Slider musicSlider;
    // 背景音乐音量文本显示
    private TMP_Text musicValueText;
    // 背景音乐 AudioSource 引用
    private AudioSource bgmAudioSource;
    
    public SettingSubPanel(Transform parent)
    {
        root = parent.Find("Setting")?.gameObject;
        if (root == null) return;
        
        // 获取组件引用
        titleText = root.transform.Find("Title")?.GetComponent<TMP_Text>();
        closeBtn = root.transform.Find("CloseBtn")?.GetComponent<Button>();
        
        closeBtn?.onClick.AddListener(OnClose);
        
        // 获取音乐 Slider
        musicSlider = root.transform.Find("Scroll View/Viewport/Content/Music/Slider")?.GetComponent<Slider>();
        
        // 获取音乐音量文本
        musicValueText = root.transform.Find("Scroll View/Viewport/Content/Music/Title")?.GetComponent<TMP_Text>();

        if (musicSlider != null)
        {
            // 加载保存的音乐音量
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            musicSlider.value = musicVolume; // 转换为 0-100 范围
            zUDebug.Log($"[SettingSubPanel] 背景音乐音量已设置为：{musicVolume * 100:F0}%");
            
            // 更新文本显示
            UpdateMusicValueText(musicVolume);
            
            // 添加值变化监听
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        
        // 获取 BGM 的 AudioSource 组件
        GameObject bgmObject = GameObject.Find("BGM");
        if (bgmObject != null)
        {
            bgmAudioSource = bgmObject.GetComponent<AudioSource>();
            if (bgmAudioSource != null)
            {
                // 获取保存的背景音乐音量
                if (!bgmAudioSource.isPlaying)
                {
                    bgmAudioSource.volume = musicSlider.value;
                    bgmAudioSource.Play();
                }
            }
        }
    }
    
    
    /// <summary>
    /// 设置数据并显示
    /// </summary>
    public void Show(SubPanelData data)
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
    /// 刷新 UI 显示
    /// </summary>
    public void UpdateUI()
    {
        if (Data == null) return;
        
        if (titleText != null) titleText.text = Data.Title;
    }
    
    /// <summary>
    /// 设置显示状态
    /// </summary>
    public void SetActive(bool active)
    {
        root?.SetActive(active);
    }
    
    private void OnClose()
    {
        Hide();
        OnCloseClick?.Invoke();
    }
    
    /// <summary>
    /// 更新音乐音量文本显示
    /// </summary>
    private void UpdateMusicValueText(float value)
    {
        if (musicValueText != null)
        {
            musicValueText.text = $"背景音乐 {value * 100:F0}%";
        }
    }
    
    /// <summary>
    /// 音乐音量变化处理
    /// </summary>
    private void OnMusicVolumeChanged(float value)
    {
        // 保存到 PlayerPrefs
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
        
        // 应用到 AudioSource
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = value;
        }
        
        // 更新文本显示
        UpdateMusicValueText(value);
        
        Debug.Log($"[SettingSubPanel] 背景音乐音量已设置为：{value * 100:F0}%");
    }
    
    /// <summary>
    /// 销毁时调用，清理事件
    /// </summary>
    public void Destroy()
    {
        closeBtn?.onClick.RemoveListener(OnClose);
        OnCloseClick = null;
        
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }
    }
}