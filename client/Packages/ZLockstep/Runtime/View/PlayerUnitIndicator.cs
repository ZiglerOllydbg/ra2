using UnityEngine;

namespace ZLockstep.View
{
    /// <summary>
    /// 玩家单位指示器组件
    /// 用于区分本地玩家单位和敌方单位的视觉表现
    /// </summary>
    public class PlayerUnitIndicator : MonoBehaviour
    {
        [Header("颜色设置")]
        [Tooltip("本地玩家单位颜色")]
        [SerializeField] private Color localPlayerColor = Color.green;
        
        [Tooltip("其他玩家单位颜色")]
        [SerializeField] private Color otherPlayerColor = Color.red;

        private Renderer[] _renderers;
        private int _localPlayerId = -1;
        private int _unitPlayerId = -1;

        private void Awake()
        {
            // 获取所有渲染器组件
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// 初始化指示器
        /// </summary>
        /// <param name="localPlayerId">本地玩家ID</param>
        /// <param name="unitPlayerId">单位所属玩家ID</param>
        public void Initialize(int localPlayerId, int unitPlayerId)
        {
            _localPlayerId = localPlayerId;
            _unitPlayerId = unitPlayerId;
            
            UpdateVisualIndication();
        }

        /// <summary>
        /// 更新视觉指示
        /// </summary>
        public void UpdateVisualIndication()
        {
            if (_localPlayerId == -1 || _unitPlayerId == -1)
                return;

            bool isLocalPlayer = _unitPlayerId == _localPlayerId;
            Color targetColor = isLocalPlayer ? localPlayerColor : otherPlayerColor;
            
            ApplyColorToRenderers(targetColor);
        }

        /// <summary>
        /// 应用颜色到所有渲染器
        /// </summary>
        /// <param name="color">目标颜色</param>
        private void ApplyColorToRenderers(Color color)
        {
            if (_renderers == null)
                return;

            foreach (var renderer in _renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = color;
                }
            }
        }

        /// <summary>
        /// 设置本地玩家颜色
        /// </summary>
        public void SetLocalPlayerColor(Color color)
        {
            localPlayerColor = color;
            if (_localPlayerId != -1 && _unitPlayerId != -1 && _unitPlayerId == _localPlayerId)
            {
                ApplyColorToRenderers(localPlayerColor);
            }
        }

        /// <summary>
        /// 设置其他玩家颜色
        /// </summary>
        public void SetOtherPlayerColor(Color color)
        {
            otherPlayerColor = color;
            if (_localPlayerId != -1 && _unitPlayerId != -1 && _unitPlayerId != _localPlayerId)
            {
                ApplyColorToRenderers(otherPlayerColor);
            }
        }
    }
}