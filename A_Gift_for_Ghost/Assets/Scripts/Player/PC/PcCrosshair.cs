using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Player.PC
{
    // PC 专用：屏幕中心准星（docs/VR_GUIDELINES.md 第 5 节：独立组件，VR 时换成手柄射线指示）。
    // 准星图形由场景构建时生成；highlighted 由交互发起方设置（第 4 阶段：对准可摘的果实时变色、放大）。
    public class PcCrosshair : MonoBehaviour
    {
        public Graphic dot;
        public Color normalColor = new Color(0.12f, 0.13f, 0.15f, 0.9f);
        public Color highlightColor = new Color(1f, 0.72f, 0.15f, 1f);
        [Tooltip("高亮时的放大倍数")]
        public float highlightScale = 1.6f;

        bool highlighted;

        public bool Highlighted
        {
            get => highlighted;
            set
            {
                highlighted = value;
                Apply();
            }
        }

        void OnEnable()
        {
            Apply();
        }

        void Apply()
        {
            if (dot == null) return;
            dot.color = highlighted ? highlightColor : normalColor;
            dot.rectTransform.localScale = Vector3.one * (highlighted ? highlightScale : 1f);
        }
    }
}
