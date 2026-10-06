using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // AI 询问框：一句提问 + 一个 Yes 按钮。点 Yes 后先隐藏，再回调 onYes 并发 Answered。
    // 【技术债】比赛期间是 Screen Space HUD，Yes 是 UGUI Button（EventSystem + InputSystemUIInputModule，走 UI 动作表）；
    // 赛后改回 World Space，VR 版用 XR UI 射线点同一个 Button（AGENTS.md 第 5 节）。
    public class AgentQueryDialog : MonoBehaviour
    {
        [Header("引用（AgentUIBuilder 会填好）")]
        public RectTransform panel;
        public Text headerLabel;
        public Text questionLabel;
        public Button yesButton;
        public Image yesBackground;
        public Text yesLabel;

        [Header("文字")]
        public string header = "AGENT · QUERY";
        public string yesText = "YES";

        [Header("尺寸（HUD 参考分辨率像素）")]
        public float width = 560f;
        public float padding = 28f;
        public float headerHeight = 40f;
        public float buttonHeight = 64f;
        public float buttonWidth = 180f;
        public float gap = 22f;

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
            if (panel == null || questionLabel == null || yesButton == null)
            {
                Debug.LogError("[Agent] AgentQueryDialog 缺少界面引用，请用 AgentUIBuilder 生成", this);
                enabled = false;
                return;
            }
            if (headerLabel != null) headerLabel.text = header;
            if (yesLabel != null) yesLabel.text = yesText;
            yesButton.onClick.AddListener(OnYes);
            // 悬停变色：按钮上挂 EventTrigger
            var trigger = yesButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = yesButton.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => SetHover(true));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetHover(false));
            panel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (yesButton != null) yesButton.onClick.RemoveListener(OnYes);
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
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

        // 按提问行数调整高度；按钮在底部居中。
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
        }
    }
}
