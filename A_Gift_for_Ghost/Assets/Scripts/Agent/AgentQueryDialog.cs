using System;
using Ghost.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // AI 询问框（World Space）：一句提问 + 一个 Yes 按钮。
    // Yes 按钮是 InteractableButton + 贴合大小的 BoxCollider，由 G2 的 PointerInput 射线点击
    // （以后 VR 换成手柄射线发起方，这里不用改）。点 Yes 后先隐藏，再回调 onYes 并发 Answered。
    public class AgentQueryDialog : MonoBehaviour
    {
        [Header("引用（AgentUIBuilder 会填好）")]
        public RectTransform panel;
        public Text headerLabel;
        public Text questionLabel;
        public InteractableButton yesButton;
        public BoxCollider yesCollider;
        public Image yesBackground;
        public Text yesLabel;

        [Header("文字")]
        public string header = "AGENT · QUERY";
        public string yesText = "YES";

        [Header("尺寸（像素，1000 px = 1 m）")]
        public float width = 620f;
        public float padding = 36f;
        public float headerHeight = 56f;
        public float buttonHeight = 96f;
        public float buttonWidth = 220f;
        public float gap = 28f;

        [Header("颜色")]
        public Color buttonColor = AgentUIStyle.Ink;
        public Color buttonHoverColor = AgentUIStyle.BlueGray;
        public Color buttonTextColor = new Color(0.96f, 0.96f, 0.97f);

        [Tooltip("显示后多少秒内不接受点击，防止上一关的点击误触")]
        public float inputDelay = 0.3f;

        // 玩家点了 Yes（参数为提问文字）
        public event Action<string> Answered;

        public bool Visible => panel != null && panel.gameObject.activeSelf;
        public string Question { get; private set; }

        Action pending;
        float acceptAt;

        void Awake()
        {
            if (panel == null || questionLabel == null || yesButton == null || yesCollider == null)
            {
                Debug.LogError("[Agent] AgentQueryDialog 缺少界面引用，请用 AgentUIBuilder 生成", this);
                enabled = false;
                return;
            }
            if (headerLabel != null) headerLabel.text = header;
            if (yesLabel != null) yesLabel.text = yesText;
            yesButton.onTap.AddListener(OnYes);
            yesButton.onHoverEnter.AddListener(() => SetHover(true));
            yesButton.onHoverExit.AddListener(() => SetHover(false));
            panel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (yesButton != null) yesButton.onTap.RemoveListener(OnYes);
        }

        // 显示提问。再次调用会替换当前的提问和回调
        public void Ask(string question, Action onYes)
        {
            if (!enabled) return;
            Question = question ?? "";
            pending = onYes;
            questionLabel.text = Question;
            panel.gameObject.SetActive(true);
            SetHover(false);
            Resize();
            acceptAt = Time.time + inputDelay;
        }

        public void Hide()
        {
            pending = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        // 代码直接确认（测试、或以后键盘 / 手柄确认用），与点击 Yes 效果相同
        public void ConfirmYes() => Answer(true);

        void OnYes() => Answer(false);

        void Answer(bool ignoreDelay)
        {
            if (!Visible) return;
            if (!ignoreDelay && Time.time < acceptAt) return;
            var callback = pending;
            string q = Question;
            Hide();
            callback?.Invoke();
            Answered?.Invoke(q);
        }

        void SetHover(bool hover)
        {
            if (yesBackground != null) yesBackground.color = hover ? buttonHoverColor : buttonColor;
        }

        // 按提问行数调整高度；按钮在底部居中，BoxCollider 跟按钮一样大。
        // 构建时（编辑器里）也调一次，场景里看到的布局和运行时一致
        public void Resize()
        {
            float inner = width - padding * 2f;
            var qRect = questionLabel.rectTransform;
            qRect.anchoredPosition = new Vector2(padding, -padding - headerHeight);
            qRect.sizeDelta = new Vector2(inner, 10f);
            float qHeight = Mathf.Max(questionLabel.fontSize * 1.3f, questionLabel.preferredHeight);
            qRect.sizeDelta = new Vector2(inner, qHeight);

            float height = padding + headerHeight + qHeight + gap + buttonHeight + padding;
            panel.sizeDelta = new Vector2(width, height);

            var bRect = (RectTransform)yesButton.transform;
            bRect.anchorMin = bRect.anchorMax = new Vector2(0.5f, 0f);
            bRect.pivot = new Vector2(0.5f, 0f);
            bRect.anchoredPosition = new Vector2(0f, padding);
            bRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            // Collider 用按钮自身的本地坐标（像素，物体缩放 0.001 由 Canvas 继承），中心在矩形中心
            yesCollider.size = new Vector3(buttonWidth, buttonHeight, 20f);
            yesCollider.center = new Vector3(0f, buttonHeight * 0.5f, 0f);
        }
    }
}
