using UnityEngine;
using TMPro;
using ZFrame;
using UnityEngine.UI;

/// <summary>
/// 热键快捷栏子面板控制器 - 封装热键快捷栏的UI逻辑和数据
/// </summary>
public class HotKeySubPanel
{
    private GameObject root;
    private LongPressButton[] hotButtons; // 对应 Hot1, Hot2, Hot3, Hot4 的按钮组件

    /// <summary>
    /// 绑定的数据
    /// </summary>
    public HotKeyPanelData Data { get; private set; }

    public HotKeySubPanel(Transform parent)
    {
        root = parent.Find("HotKeyGroup")?.gameObject;
        if (root == null) return;

        // 获取四个热键按钮组件
        hotButtons = new LongPressButton[4];
        for (int i = 0; i < 4; i++)
        {
            string keyName = $"Hot{i + 1}";
            Transform child = root.transform.Find(keyName);
            if (child != null)
            {
                hotButtons[i] = child.GetComponent<LongPressButton>();
            }
        }

        // 初始化UI
        UpdateUI();

        // 添加点击和长按事件监听
        for (int i = 0; i < hotButtons.Length; i++)
        {
            if (hotButtons[i] != null)
            {
                int index = i; // 避免闭包问题
                hotButtons[i].onClick.AddListener(() => OnHotKeyClicked(index));
                hotButtons[i].onLongPress.AddListener(() => OnHotKeyLongPressed(index));
            }
        }
    }

    /// <summary>
    /// 热键点击事件处理
    /// </summary>
    /// <param name="index">热键索引</param>
    private void OnHotKeyClicked(int index)
    {
        Debug.Log($"热键 {index + 1} 被点击了！");
        // 可以在此处添加具体逻辑，例如选择单位、执行命令等
    }
    
    /// <summary>
    /// 热键长按事件处理
    /// </summary>
    /// <param name="index">热键索引</param>
    private void OnHotKeyLongPressed(int index)
    {
        Debug.Log($"热键 {index + 1} 被长按了！");
        // 可以在此处添加长按逻辑，例如连续执行命令等
    }

    /// <summary>
    /// 设置数据并显示
    /// </summary>
    public void Show(HotKeyPanelData data)
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

        for (int i = 0; i < 4; i++)
        {
            if (hotButtons[i] != null && i < Data.HotKeys.Length)
            {
                hotButtons[i].GetComponentInChildren<TMP_Text>().text = Data.HotKeys[i];
            }
        }
    }

    /// <summary>
    /// 设置显示状态
    /// </summary>
    public void SetActive(bool active)
    {
        root?.SetActive(active);
    }

    /// <summary>
    /// 销毁时调用，清理事件
    /// </summary>
    public void Destroy()
    {
        // 移除所有按钮的点击和长按事件监听
        for (int i = 0; i < hotButtons.Length; i++)
        {
            if (hotButtons[i] != null)
            {
                hotButtons[i].onClick.RemoveAllListeners();
                hotButtons[i].onLongPress.RemoveAllListeners();
            }
        }
    }
}