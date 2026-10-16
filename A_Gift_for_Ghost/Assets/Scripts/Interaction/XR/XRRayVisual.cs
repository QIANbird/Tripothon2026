using UnityEngine;

namespace Ghost.Interaction.XR
{
    // 手柄射线的显示，和 XRControllerInput 分开（对应 PC 的 PointerCursor）：LineRenderer 从手柄画到命中点，
    // 指向节点或可交互物时变亮，否则画一段短的暗线。发起方不在用（PC 模式或阶段关掉输入）时隐藏。
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(100)] // 在发起方 Update 之后读本帧射线
    public class XRRayVisual : MonoBehaviour
    {
        public XRControllerInput source;
        [Tooltip("没有命中目标时射线的长度（米）")]
        public float idleLength = 1.2f;
        public Color idleColor = new Color(1f, 1f, 1f, 0.25f);
        public Color hoverColor = new Color(1f, 1f, 1f, 0.9f);
        public Color selectedColor = new Color(0.9f, 1f, 0.45f, 0.95f);
        [Tooltip("射线末端退回一点，避免插进方块")]
        public float surfaceOffset = 0.005f;

        LineRenderer line;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
        }

        void LateUpdate()
        {
            bool active = source != null && source.isActiveAndEnabled && source.InputEnabled;
            if (line.enabled != active) line.enabled = active;
            if (!active) return;

            Ray ray = source.CurrentRay;
            bool onTarget = source.HoveredNode >= 0 || source.HoveredInteractable != null;
            float length = onTarget && !float.IsInfinity(source.HoverDistance)
                ? Mathf.Max(0f, source.HoverDistance - surfaceOffset)
                : idleLength;

            Color color = !onTarget ? idleColor
                : source.HoveredNode >= 0 && source.HoveredNode == source.SelectedNode ? selectedColor
                : hoverColor;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, onTarget ? color.a : 0f);
            line.SetPosition(0, ray.origin);
            line.SetPosition(1, ray.origin + ray.direction * length);
        }
    }
}
