using UnityEngine;

namespace ZLockstep.View
{
    /// <summary>
    /// 玩家单位指示器组件
    /// 用于区分本地玩家单位和敌方单位的视觉表现
    /// </summary>
    public class PlayerUnitIndicator : MonoBehaviour
    {
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

            Color targetColor = GetCampColor(_unitPlayerId);
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
        /// 根据阵营ID获取对应颜色
        /// </summary>
        /// <param name="campId">阵营ID</param>
        /// <returns>阵营对应的颜色</returns>
        private Color GetCampColor(int campId)
        {
            switch (campId)
            {
                case 1:  // Red
                    return Color.red;
                case 2:  // Blue
                    return Color.blue;
                case 3:  // Green
                    return Color.green;
                case 4:  // Yellow
                    return Color.yellow;
                case 5:  // Orange
                    return new Color(1.0f, 0.5f, 0.0f); // 橙色
                case 6:  // Purple
                    return new Color(0.5f, 0.0f, 1.0f); // 紫色
                case 7:  // Pink
                    return new Color(1.0f, 0.4f, 0.7f); // 粉色
                case 8:  // Brown
                    return new Color(0.6f, 0.3f, 0.0f); // 棕色
                default: // 默认使用白色
                    return Color.white;
            }
        }
    }
}