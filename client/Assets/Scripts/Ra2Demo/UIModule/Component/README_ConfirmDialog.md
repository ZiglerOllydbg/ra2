# ConfirmDialogComponent - 通用确认对话框组件（非 MonoBehaviour 版本）

## 概述
`ConfirmDialogComponent` 是一个轻量级的纯 C# 确认对话框组件，用于处理各种需要用户确认的场景。
**不继承自 MonoBehaviour**，因此更轻量、更灵活，可以在任何 C# 类中使用。

## 功能特性
- ✅ 纯 C# 实现，不依赖 MonoBehaviour
- ✅ 支持自定义确认和取消回调
- ✅ 支持动态设置消息文本
- ✅ 自动管理事件注册和注销
- ✅ 防止内存泄漏的回调清理机制
- ✅ 轻量级设计，性能更优

## 使用方法

### 1. 创建并初始化组件
```csharp
// 在面板类中声明字段
private ConfirmDialogComponent confirmDialog;

// 在 OnBecameVisible 中初始化
protected override void OnBecameVisible()
{
    base.OnBecameVisible();
    
    var confirmPanelTransform = PanelObject.transform.Find("Confirm");
    if (confirmPanelTransform != null)
    {
        confirmDialog = new ConfirmDialogComponent();
        confirmDialog.Initialize(confirmPanelTransform);
    }
}
```

### 2. 显示对话框
```csharp
// 简单用法（仅确认回调）
confirmDialog.Show(() => {
    Debug.Log("用户确认了操作");
    // 执行确认逻辑
});

// 完整用法（确认 + 取消 + 消息）
confirmDialog.Show(
    onConfirm: () => {
        Debug.Log("用户确认了");
        Frame.DispatchEvent(new RestartGameEvent());
    },
    onCancel: () => {
        Debug.Log("用户取消了");
    },
    message: "确定要执行此操作吗？"
);
```

### 3. 隐藏对话框
```csharp
confirmDialog.Hide();
```

### 4. 单独设置消息文本
```csharp
confirmDialog.SetMessage("这是一条新消息");
```

### 5. 清理事件监听
```csharp
// 在 OnBecameInvisible 中调用
protected override void OnBecameInvisible()
{
    base.OnBecameInvisible();
    
    if (confirmDialog != null)
    {
        confirmDialog.UnregisterEvents();
    }
}
```

## API 参考

### 公共方法
- `void Initialize(Transform parent)` - 初始化组件，获取 UI 引用
- `void Show(Action onConfirm, Action onCancel = null, string message = null)` - 显示对话框
- `void Hide()` - 隐藏对话框并清理回调
- `void SetMessage(string message)` - 设置消息文本
- `void UnregisterEvents()` - 移除事件监听

### 注意事项
1. ⚠️ **必须在面板销毁前调用 `UnregisterEvents()` 清理事件**
2. ⚠️ 每次调用 `Show()` 会覆盖之前的回调
3. ⚠️ 调用 `Hide()` 会自动清理回调委托
4. ⚠️ 必须在 `Initialize()` 后才能使用

## 设计优势

### 相比 MonoBehaviour 版本的优势
- 🚀 **更轻量**：不依赖 Unity 组件系统，内存占用更小
- ⚡ **性能更好**：没有 MonoBehaviour 的生命周期开销
- 🔧 **更灵活**：可以在任何 C# 类中使用，不限于 MonoBehaviour
- 📦 **易测试**：纯 C# 类更容易进行单元测试
- 🎯 **职责单一**：专注于对话框逻辑，不涉及 Unity 生命周期

### 通用设计原则
- 📦 **组件化设计**：可独立复用，不依赖特定面板
- 🔒 **内存安全**：自动清理回调，防止内存泄漏
- 🎯 **职责分离**：UI 组件负责展示，业务逻辑由调用方提供
- ♻️ **高度复用**：一次编写，多处使用