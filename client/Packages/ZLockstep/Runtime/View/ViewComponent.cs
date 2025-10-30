using UnityEngine;
using ZLockstep.Simulation.ECS;

namespace ZLockstep.View
{
    /// <summary>
    /// 表现层绑定组件（引用Unity GameObject）
    /// 注意：这是唯一引用Unity的组件，只在表现层使用
    /// 不参与逻辑计算，仅用于显示
    /// </summary>
    public class ViewComponent : IComponent
    {
        /// <summary>
        /// 关联的Unity GameObject
        /// </summary>
        public GameObject GameObject;

        /// <summary>
        /// Transform缓存（性能优化）
        /// </summary>
        public Transform Transform;

        /// <summary>
        /// 动画控制器（如果有）
        /// </summary>
        public Animator Animator;

        /// <summary>
        /// 渲染器（如果有）
        /// </summary>
        public Renderer Renderer;

        // --- 插值相关（用于平滑表现） ---

        /// <summary>
        /// 上一帧的逻辑位置
        /// </summary>
        public Vector3 LastLogicPosition;

        /// <summary>
        /// 上一帧的逻辑旋转
        /// </summary>
        public Quaternion LastLogicRotation;

        /// <summary>
        /// 是否启用插值
        /// </summary>
        public bool EnableInterpolation;

        /// <summary>
        /// 创建ViewComponent
        /// </summary>
        public static ViewComponent Create(GameObject gameObject, bool enableInterpolation = false)
        {
            var view = new ViewComponent
            {
                GameObject = gameObject,
                Transform = gameObject.transform,
                Animator = gameObject.GetComponent<Animator>(),
                Renderer = gameObject.GetComponent<Renderer>(),
                EnableInterpolation = enableInterpolation,
                LastLogicPosition = gameObject.transform.position,
                LastLogicRotation = gameObject.transform.rotation
            };

            return view;
        }

        /// <summary>
        /// 销毁GameObject
        /// </summary>
        public void Destroy()
        {
            if (GameObject != null)
            {
                Object.Destroy(GameObject);
                GameObject = null;
                Transform = null;
                Animator = null;
                Renderer = null;
            }
        }
    }
}

