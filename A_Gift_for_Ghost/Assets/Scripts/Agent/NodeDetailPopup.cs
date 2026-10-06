using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // 节点详情弹窗（比赛期间为 HUD，固定在屏幕左侧 Agent 栏）。
    // 行为：右键按下 Show → 按住期间一直显示 → 松开 Release 后停留 lingerSeconds → CanvasGroup 淡出后隐藏。
    // 和 Agent 剧情弹窗（AgentMessagePanel）同时出现时，放在它下方，不叠在一起（见 stackBelow）。
    // 不挡指针射线（文字和底板都不接收 UI 射线）。
    // 【技术债】赛后改回 World Space（AGENTS.md 第 5 节）
    public class NodeDetailPopup : MonoBehaviour
    {
        [Header("引用（AgentUIBuilder 会填好）")]
        public RectTransform panel;
        public RectTransform box;
        public Text titleLabel;
        public Text bodyLabel;
        public CanvasGroup group;
        [Tooltip("可选：显示时如果它可见，就把详情放在它下方")]
        public RectTransform stackBelow;

        [Header("摆放（参考分辨率像素，左上角锚定）")]
        public Vector2 topLeft = new Vector2(AgentUIStyle.HudMargin, -AgentUIStyle.HudMargin);
        public float stackGap = 16f;

        [Header("尺寸（像素）")]
        public float width = AgentUIStyle.HudLeftColumnWidth;
        public float padding = 20f;
        public float titleHeight = 40f;
        public float titleBodyGap = 6f;

        [Header("行为")]
        [Tooltip("松开右键后停留多久再淡出（秒），2–3 s")]
        [Range(0f, 5f)] public float lingerSeconds = 2.5f;
        [Tooltip("淡出时长（秒）")]
        public float fadeSeconds = 0.4f;

        public bool Visible => panel != null && panel.gameObject.activeSelf;
        public int NodeId { get; private set; } = -1;
        // 正在按住（Release 之前）
        public bool Held { get; private set; }

        float releaseAt = -1f;

        void Awake()
        {
            if (panel == null || box == null || titleLabel == null || bodyLabel == null)
            {
                Debug.LogError("[Agent] NodeDetailPopup 缺少界面引用，请用 AgentUIBuilder 生成", this);
                enabled = false;
                return;
            }
            if (group == null) group = panel.GetComponent<CanvasGroup>();
            panel.gameObject.SetActive(false);
        }

        // 显示节点 nodeId 的详情（右键按下时调用）。之后调 Release 开始倒计时
        public void Show(int nodeId, string title, string body)
        {
            if (!enabled) return;
            NodeId = nodeId;
            Held = true;
            releaseAt = -1f;
            panel.gameObject.SetActive(true);
            if (group != null) group.alpha = 1f;
            SetText(title, body);
            Place();
        }

        // 右键松开：停留 lingerSeconds 后淡出
        public void Release()
        {
            if (!Visible || !Held) return;
            Held = false;
            releaseAt = Time.time;
        }

        public void Hide()
        {
            NodeId = -1;
            Held = false;
            releaseAt = -1f;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        // 只改文字（比如同一节点的进度在变）
        public void SetText(string title, string body)
        {
            titleLabel.text = title ?? "";
            bodyLabel.text = body ?? "";
            Resize();
        }

        void LateUpdate()
        {
            if (!Visible) return;
            Place();
            if (Held || releaseAt < 0f) return;
            float t = Time.time - releaseAt - lingerSeconds;
            if (t < 0f) return;
            float a = fadeSeconds > 0f ? 1f - t / fadeSeconds : 0f;
            if (a <= 0f) { Hide(); return; }
            if (group != null) group.alpha = a;
        }

        // 左上角；Agent 剧情弹窗可见时放到它下方
        void Place()
        {
            Vector2 pos = topLeft;
            if (stackBelow != null && stackBelow.gameObject.activeInHierarchy)
                pos.y = stackBelow.anchoredPosition.y - stackBelow.rect.height - stackGap;
            panel.anchoredPosition = pos;
        }

        // 高度随正文行数变化
        void Resize()
        {
            float inner = width - padding * 2f;
            var titleRect = titleLabel.rectTransform;
            titleRect.anchoredPosition = new Vector2(padding, -padding);
            titleRect.sizeDelta = new Vector2(inner, titleHeight);

            var bodyRect = bodyLabel.rectTransform;
            bodyRect.anchoredPosition = new Vector2(padding, -padding - titleHeight - titleBodyGap);
            bodyRect.sizeDelta = new Vector2(inner, 10f);
            float bodyHeight = string.IsNullOrEmpty(bodyLabel.text) ? 0f : bodyLabel.preferredHeight;
            bodyRect.sizeDelta = new Vector2(inner, bodyHeight);

            float height = padding * 2f + titleHeight + (bodyHeight > 0f ? titleBodyGap + bodyHeight : 0f);
            box.sizeDelta = new Vector2(width, height);
            panel.sizeDelta = new Vector2(width, height);
        }
    }
}
