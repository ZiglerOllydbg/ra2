using Edu100.Enum;
using System;
using UnityEngine;
using UnityEngine.UI;
using ZFrame;
using ZLib;

/// <summary>
/// 统一管理界面 管理生存周期
/// @author Ollydbg
/// @date 2018-2-23
/// </summary>
public abstract class BasePanel
{

    #region 加载完成通知

    /// <summary>
    /// 加载完成通知
    /// </summary>
    public Action<bool> OnLoadComplete;

    /// <summary>
    /// 界面打开回调
    /// </summary>
    public Action onDisplayCallback;

    #endregion

    #region 面板的一些通用信息

    /// <summary>
    /// 面板消息监听
    /// </summary>
    protected IDispathMessage processor;
    /// <summary>
    /// 定义了Panel的元数据
    /// </summary>
    public UIModelData ModelData { get; }
    /// <summary>
    /// 当前面板上绑定的Unity GameObject
    /// </summary>
    public GameObject PanelObject { get; protected set; }
    /// <summary>
    /// UIObject
    /// </summary>
    //protected UIObject uiObject;

    /// <summary>
    /// 是否已经销毁掉了
    /// </summary>
    protected bool isDisposed = false;
    /// <summary>
    /// 是否在打开状态
    /// </summary>
    public bool IsOpen { get; protected set; }
    /// <summary>
    /// 是否可见
    /// 在加载完毕后 如果还是打开状态就立即可见
    /// </summary>
    public bool IsVisiable { get; protected set; }
    /// <summary>
    /// 是否正在加载中
    /// </summary>
    public bool IsLoading { get; protected set; }
    /// <summary>
    /// 是否有GameObject
    /// </summary>
    public bool IsHasGameObject { get => PanelObject != null; }

    /// <summary>
    /// 界面皮肤SkinID
    /// </summary>
    public PanelSkinID panelSkinID
    {
        private set;
        get;
    } = PanelSkinID.None;
    /// <summary>
    /// 面板深度
    /// </summary>
    protected int depth = 0;

    //[JsonIgnore]
    //[NonSerialized]
    //protected HLData hlData;
    /// <summary>
    /// 抽象一个管理层次或者深度的逻辑
    /// </summary>
    public virtual int UIDepth
    {
        get { return depth; }
        set
        {
            depth = value;

            if (PanelObject)
            {
                var canvas = PanelObject.GetOrAddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = depth;

                PanelObject.GetOrAddComponent<GraphicRaycaster>();
            }
        }
    }

    #endregion

    #region 构造方法

    /// <summary>
    /// 构造 BasePanel
    /// </summary>
    /// <param name="_processor"></param>
    /// <param name="_modelData"></param>
    /// <param name="_disableNew"></param>
    public BasePanel(IDispathMessage _processor, UIModelData _modelData, DisableNew _disableNew)
    {
        if (_disableNew == null)
            throw new Exception("Panel need Create By New extend method!");

        this.processor = _processor ?? throw new Exception("Panel need a Processor!");
        this.ModelData = _modelData ?? throw new Exception("Panel need have a modelData create by ModelDataAttribute!");

        OnInit();

        RegisterPanel();
    }

    #endregion

    #region 生命周期方法
    /// <summary>
    /// 指令点击打点数据
    /// </summary>
    //protected CommandClickDot dotMessage;
    /// <summary>
    /// Panel 初始化时调用
    /// </summary>
    protected virtual void OnInit()
    {

    }


    /// <summary>
    /// 在 PanelManager 中注册自己
    /// </summary>
    private void RegisterPanel()
        => PanelManager.instance.AddPanel(this);

    public virtual void Open()
    {
        this.Open(PanelSkinID.None);
    }
    /// <summary>
    /// 打开面板
    /// </summary>
    public virtual void Open(PanelSkinID __panelSkinID = PanelSkinID.None)
    {
        //未打开且不可见
        if (!IsOpen)
        {
            PanelManager.instance.OpenPanel(this);

            IsOpen = true;

            if (IsLoading)
            {
                this.LogWarning($"Panel is loading...PanelID: { ModelData.PanelID }");
                return;
            }

            //Debugger.Log("Panel", $"【窗口】 ModelData.PanelID:{ ModelData.PanelID} __panelSkinID:{__panelSkinID}");
            TryOpen(__panelSkinID);
        }
        else
        {
            if (this.IsVisiable)
                this.OnOpen();
        }
    }
    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }
    /// <summary>
    /// 尝试打开面板
    /// </summary>
    protected virtual void TryOpen(PanelSkinID __panelSkinID = PanelSkinID.None)
    {
        //TODO 打开面板开始计时

        if ((this.panelSkinID != PanelSkinID.None || __panelSkinID != PanelSkinID.None) && this.panelSkinID != __panelSkinID)
        {
            //如果与上一个类型不相同，需要卸载 然后重新加载
            //新传入的皮肤不是空
            //上一个类型
            if (PanelObject != null)
            {
                //这种情况下可以尝试直接打开
                //这里需要假设在Loading状态 走相同的逻辑
                IsLoading = true;

                //先根据配置，归还掉当前的界面,同步销毁UI 
                this.CloseByActionType(true);
            }

            //然后重新加载
            this.LoadUIPrefab(__panelSkinID);
        }
        else
        {
            // 如果仅仅是隐藏模式，直接尝试打开，否则重新加载
            if (PanelObject != null)
            {
                //这种情况下可以尝试直接打开
                //这里需要假设在Loading状态 走相同的逻辑
                IsLoading = true;

                BorrowComplete(this.ModelData.PanelID, __panelSkinID, this.PanelObject);
            }
            else
            {
                LoadUIPrefab(__panelSkinID);
            }
        }

        this.panelSkinID = __panelSkinID;
    }

    /// <summary>
    /// 加载 Prefab
    /// </summary>
    protected virtual void LoadUIPrefab(PanelSkinID __panelSkinID = PanelSkinID.None)
    {
        IsLoading = true;

        PanelManager.instance.BorrowUIRes(this.ModelData.PanelID, __panelSkinID, BorrowComplete);

    }

    /// <summary>
    /// 预加载UI资源
    /// </summary>
    public void PrestrainUIRes(PanelSkinID _panelSkinID, Action<PanelID, PanelSkinID, GameObject> _prestrainComplete)
    {
        this.Log($"【BasePanel】 PrestrainUIRes 预加载资源 panelID:{this.ModelData?.PanelID}  _panelSkinID:{_panelSkinID}  _prestrainComplete:{_prestrainComplete}");

        if ((this.panelSkinID != PanelSkinID.None || _panelSkinID != PanelSkinID.None) && this.panelSkinID != _panelSkinID)
        {
            //如果与上一个类型不相同，需要卸载 然后重新加载
            //新传入的皮肤不是空
            //上一个类型
            if (PanelObject != null)
            {
                //这种情况下可以尝试直接打开
                //先根据配置，归还掉当前的界面,同步销毁UI 
                this.CloseByActionType(true);
            }

            //这里需要假设在Loading状态 走相同的逻辑
            //IsLoading = true;

            PanelManager.instance.BorrowUIRes(this.ModelData.PanelID, _panelSkinID, PrestrainBorrowComplete);
        }
        else
        {
            //这里需要假设在Loading状态 走相同的逻辑
            //IsLoading = true;
            // 如果仅仅是隐藏模式，直接尝试打开，否则重新加载
            if (PanelObject != null)
            {
                //这种情况下可以尝试直接打开
                PrestrainBorrowComplete(this.ModelData.PanelID, _panelSkinID, this.PanelObject);
            }
            else
            {
                //然后重新加载
                PanelManager.instance.BorrowUIRes(this.ModelData.PanelID, _panelSkinID, PrestrainBorrowComplete);
            }
        }

        this.panelSkinID = _panelSkinID;
        ///
        void PrestrainBorrowComplete(PanelID __panelID, PanelSkinID __panelSkinID, GameObject __go)
        {
            this.Log($"【BasePanel】 PrestrainBorrowComplete 预加载资源，给回调");

            this.PanelObject = __go;

            this.PanelObject.SetActive(false);

            this.SetParent(PanelManager.instance.GetParentByPanelDepthType(this.ModelData.PanelUIDepthType));

            _prestrainComplete?.Invoke(__panelID, __panelSkinID, __go);
        }
    }

    /// <summary>
    /// 父类提供warmUp接口
    /// 核心实现思路是不改变打开关闭状态的情况下 准备好资源
    /// </summary>
    public void WarmUp()
    {
        ///warmup的UI自动切换为隐藏模式
        this.ModelData.RemoveActionType = PanelRemoveActionType.JustHide;

        if (!this.IsVisiable)
        {
            TryOpen();
        }
    }


    /// <summary>
    /// 借完
    /// </summary>
    /// <param name="_panelID"></param>
    /// <param name="_go"></param>
    protected virtual void BorrowComplete(PanelID _panelID, PanelSkinID __panelSkinID, GameObject _go)
    {
        if (_go)
        {
            this.PanelObject = _go;

            var rectTransform = this.PanelObject?.GetComponent<RectTransform>();

            if (rectTransform) //发现有部分UI会自动修改这里的数据，强制纠正过来了。
            {
                rectTransform.offsetMax = Vector2.zero;

                rectTransform.offsetMin = Vector2.zero;
            }
            this.SetParent(PanelManager.instance.GetParentByPanelDepthType(this.ModelData.PanelUIDepthType));

            OnLoaded();

            InitializePanelData();
        }
        else
        {
            //加载失败的情况
            IsLoading = false;

            OnLoadComplete?.Invoke(false);

            Destroy();
        }
    }

    /// <summary>
    /// 加载完毕
    /// </summary>
    protected virtual void OnLoaded()
    {
        if (!IsLoading)
        {
            this.LogWarning("Wrong open state, load down with out loading state!");
            return;
        }

        IsLoading = false;

        //如果这个时候发现还是打开状态
        if (this.IsOpen)
        {
            SetUIReadyThenGetPanelComponent();

            ReallyOpen();
        }
        else
        {
            CloseByActionType();
        }
    }

    /// <summary>
    /// 让UI准备好 并且获取GameObject上的组件
    /// </summary>
    protected void SetUIReadyThenGetPanelComponent()
        => PanelObject.SetActive(true);

    /// <summary>
    /// 初始化一些面板数据
    /// </summary>
    private void InitializePanelData()
    {
        if (this.PanelObject == null) return;

        //TODO 未来这里添加一些面板名字、Icon或者动画相关的数据，如果需要有的话
    }

    /// <summary>
    /// 返回去 UI
    /// </summary>
    /// <returns></returns>
    protected GameObject GiveBackUI()
    {
        var go = this.PanelObject;

        this.PanelObject = null;

        return go;
    }

    /// <summary>
    /// 真正打开 UI
    /// </summary>
    protected virtual void ReallyOpen()
    {
        if (!IsVisiable)
        {
            //TODO UI面板打开计时停止

            this.UIDepth = this.UIDepth;

            //Get UIObject Component
            //if (PanelObject != null)
                //uiObject = PanelObject.GetComponent<UIObject>();

            //需要手动设置下,将画布下的比例调整到 4:3 的
            this.CanvasRectTransAdapt();

            IsVisiable = true;

            UpdateData();

            //TODO PlayOpenAnimation


            this.BecameVisible();

            //加载完成给外面一个通知
            OnLoadComplete?.Invoke(true);
        }
    }

    /// <summary>
    /// 刷新数据
    /// </summary>
    /// <returns></returns>
    public virtual bool UpdateData() => true;

    /// <summary>
    /// Panel显示触发
    /// </summary>
    protected void BecameVisible()
    {
        OnBecameVisible();

        OnInternational();

        PlayOpenSound();

        AddEvent();
        this.OnOpen();

        onDisplayCallback?.Invoke();
    }

    /// <summary>
    /// 国际化多语言设置
    /// </summary>
    protected virtual void OnInternational()
    {

    }

    /// <summary>
    /// 在Panel真正显示之前触发
    /// </summary>
    protected virtual void OnBecameVisible()
    {
        this.Log("OnBecameVisible >> " + ModelData.PanelID);
    }

    /// <summary>
    /// 添加事件
    /// </summary>
    protected virtual void AddEvent()
    { }

    /// <summary>
    /// 移除事件
    /// </summary>
    protected virtual void RemoveEvent()
    { }

    /// <summary>
    /// 窗口开启声音
    /// </summary>
    protected virtual void PlayOpenSound()
    { }

    /// <summary>
    /// 窗口关闭声音
    /// </summary>
    protected virtual void PlayCloseSound()
    { }

    /// <summary>
    /// Panel 关闭时触发
    /// </summary>
    protected void BecameInvisible()
    {
        this.OnClose();
        OnBecameInvisible();

        PlayCloseSound();

        RemoveEvent();
    }

    /// <summary>
    /// 在Panel真正关闭前触发
    /// </summary>
    protected virtual void OnBecameInvisible()
    {
        this.Log("OnBecameInvisible << " + ModelData.PanelID);
    }

    public virtual void Close()
    {
        this.Log("BasePanel Close << " + ModelData.PanelID);
        this.Close(false);
    }

    /// <summary>
    /// 带参数 ，是否立即销毁当前UI资源
    /// </summary>
    /// <param name="__isSynchronousDestory">是否立即销毁当前UI资源</param>
    public virtual void Close(bool __isSynchronousDestory = false)
    {
        if (IsOpen)
        {
            IsVisiable = false;

            IsOpen = false;

            this.OnLoadComplete = null;

            //TODO 清理缓存池
            BecameInvisible();

            CloseByActionType(__isSynchronousDestory);

            PanelManager.instance.ClosePanel(this);
        }
        else
        {
#if UNITY_EDITOR
            this.LogWarning($"Don't close on closing state! PanelID: { ModelData.PanelID }");
#endif
        }
    }

    /// <summary>
    /// 根据配置类型决定操作
    /// </summary>
    private void CloseByActionType(bool __isSynchronousDestory = false)
    {
        switch (ModelData.RemoveActionType)
        {
            case PanelRemoveActionType.Default:

                if (PanelObject != null)
                    PanelManager.instance.GiveBackUIRes(this.ModelData.PanelID, this.panelSkinID, this.GiveBackUI(), __isSynchronousDestory);
                else
                    this.LogWarning($"PanelObject was null when the panel closed by removeActionType: { ModelData.RemoveActionType }");

                break;
            case PanelRemoveActionType.JustHide:

                if (PanelObject != null)
                {
                    PanelObject.SetActive(false);
                    //如果需要同步销毁之前的旧皮肤，
                    PanelManager.instance.DestoryPanelGameObject(__isSynchronousDestory, PanelObject);
                }
                else
                    this.LogWarning($"PanelObject was null when the panel closed by removeActionType: { ModelData.RemoveActionType }");

                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 销毁面板，从管理器中删除
    /// </summary>
    public virtual void Destroy()
    {
        PanelManager.instance.RemovePanel(this);

        //TODO 需要考虑哪些要销毁

        if (IsOpen)
            Close();

        if (PanelObject != null)
            PanelManager.instance.GiveBackUIRes(this.ModelData.PanelID, this.panelSkinID, this.GiveBackUI());


        isDisposed = true;

        OnDestory();
    }

    /// <summary>
    /// 析构时调用
    /// </summary>
    protected virtual void OnDestory()
    { }

    #endregion

    /// <summary>
    /// 设置 Parent
    /// </summary>
    /// <param name="_parent"></param>
    public void SetParent(Transform _parent)
        => this.PanelObject.transform.SetParent(_parent, false);

    #region 重载比较操作符 允许释放 参考 Unity GameObject 的释放方式

    /// <summary>
    /// 比较两个 Panel 对象是否相同
    /// </summary>
    /// <param name="_panelA"></param>
    /// <param name="_panelB"></param>
    /// <returns></returns>
    private static bool CompareObjects(BasePanel _panelA, BasePanel _panelB)
    {
        bool flag1 = object.Equals(_panelA, null);

        bool flag2 = object.Equals(_panelB, null);

        //两个都为空，返回 true
        if (flag1 && flag2) return true;

        //panelA 不为空 panelB为空 但是 panelA 销毁掉了 就相等
        if (!flag1)
        {
            if (flag2)
            {
                if (_panelA.isDisposed)
                    return true;
            }
        }

        //panelB 一样
        if (!flag2)
        {
            if (flag1)
            {
                if (_panelB.isDisposed)
                    return true;
            }
        }

        //这种情况是panelA和PanelB都没有被销毁 也有可能为空 直接比对即可
        return object.ReferenceEquals(_panelA, _panelB);
    }

    /// <summary>
    /// 重载操作符判定是否相等
    /// </summary>
    /// <param name="_panelA"></param>
    /// <param name="_panelB"></param>
    /// <returns></returns>
    public static bool operator ==(BasePanel _panelA, BasePanel _panelB)
        => CompareObjects(_panelA, _panelB);

    /// <summary>
    /// 重载操作符判定是否相等
    /// </summary>
    /// <param name="_panelA"></param>
    /// <param name="_panelB"></param>
    /// <returns></returns>
    public static bool operator !=(BasePanel _panelA, BasePanel _panelB)
        => !CompareObjects(_panelA, _panelB);

    /// <summary>
    /// 重载强制转化为 bool
    /// </summary>
    /// <param name="_panel"></param>
    public static implicit operator bool(BasePanel _panel)
        => !CompareObjects(_panel, null);

    /// <summary>
    /// 判断是否相同
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj == null) return false;

        if (obj is BasePanel panel)
        {
            return panel == null ? false : CompareObjects(this, panel);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    #endregion
}
