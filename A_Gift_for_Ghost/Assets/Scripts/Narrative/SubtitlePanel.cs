using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Narrative
{
    // 对白显示（比赛期间为 Screen Space HUD，【技术债】赛后改回 World Space）。按策划规则把台词分到两处：
    //   字幕（屏幕下方一行小字，可折两行）：EmotionalFemale（署名"亲切的声音"）、Protagonist（暂按字幕、无署名，待策划确认）。
    //     浅色背景用黑字；黑屏（blackout 不透明）时用白字。不加深色底板。
    //   Agent 弹窗（屏幕左侧，带底板）：MechanicalFemale 和 Agent 都归这一类，署名"没有温度的声音"。
    //   台词的 channel 不是 Auto 时，按台词指定的通道显示（署名仍按说话人），例如亲切的声音出现在 Agent 弹窗里。
    // 订阅 DialoguePlayer，不读输入；推进由输入端调 Advance()。
    public class SubtitlePanel : MonoBehaviour
    {
        public enum Channel { Subtitle, AgentPopup }

        [System.Serializable]
        public struct SpeakerStyle
        {
            public Speaker speaker;
            public Channel channel;
            [Tooltip("署名；为空时不显示名字")]
            public string displayName;
        }

        public DialoguePlayer player;

        [Header("字幕（屏幕下方）")]
        [Tooltip("字幕根物体，没有台词时隐藏")]
        public GameObject root;
        public Text speakerLabel;
        public Text textLabel;
        [Tooltip("可选：黑屏，不透明时字幕用白字")]
        public ScreenBlackout blackout;
        public Color lightBackgroundText = new Color(0.08f, 0.09f, 0.11f);
        public Color darkBackgroundText = new Color(0.95f, 0.95f, 0.95f);

        [Header("Agent 弹窗（屏幕左侧）")]
        public GameObject agentRoot;
        public Text agentSpeakerLabel;
        public Text agentTextLabel;

        [Tooltip("打字机效果的速度（字 / 秒）；0 = 一次显示整句")]
        public float typewriterCharsPerSecond = 30f;

        public SpeakerStyle[] styles =
        {
            new SpeakerStyle { speaker = Speaker.EmotionalFemale, channel = Channel.Subtitle, displayName = "亲切的声音" },
            new SpeakerStyle { speaker = Speaker.MechanicalFemale, channel = Channel.AgentPopup, displayName = "没有温度的声音" },
            new SpeakerStyle { speaker = Speaker.Agent, channel = Channel.AgentPopup, displayName = "没有温度的声音" },
            // 【待策划确认】主角暂按字幕样式，不加署名
            new SpeakerStyle { speaker = Speaker.Protagonist, channel = Channel.Subtitle, displayName = "" },
        };

        string fullText = "";
        float revealStart;
        bool revealing;
        Text activeLabel;

        void OnEnable()
        {
            if (player == null || root == null || textLabel == null)
            {
                Debug.LogError("[Dialogue] SubtitlePanel 缺少 player / root / textLabel", this);
                enabled = false;
                return;
            }
            player.LineStarted += Show;
            player.LineFinished += HideLine;
            player.Stopped += Hide;
            Hide();
        }

        void OnDisable()
        {
            if (player == null) return;
            player.LineStarted -= Show;
            player.LineFinished -= HideLine;
            player.Stopped -= Hide;
        }

        void Update()
        {
            // 字幕颜色跟随黑屏（黑屏淡入淡出过程中也平滑变化）
            if (root.activeSelf)
            {
                float dark = blackout != null && blackout.group != null ? blackout.group.alpha : 0f;
                Color c = Color.Lerp(lightBackgroundText, darkBackgroundText, dark);
                textLabel.color = c;
                if (speakerLabel != null) speakerLabel.color = new Color(c.r, c.g, c.b, 0.7f);
            }

            if (!revealing || activeLabel == null) return;
            int count = Mathf.FloorToInt((Time.time - revealStart) * typewriterCharsPerSecond);
            if (count >= fullText.Length)
            {
                activeLabel.text = fullText;
                revealing = false;
            }
            else activeLabel.text = fullText.Substring(0, Mathf.Max(0, count));
        }

        public void Show(DialogueLine line)
        {
            Hide();
            var style = StyleOf(line.speaker);
            var channel = line.channel == LineChannel.Subtitle ? Channel.Subtitle
                : line.channel == LineChannel.AgentPopup ? Channel.AgentPopup
                : style.channel;
            bool popup = channel == Channel.AgentPopup && agentRoot != null && agentTextLabel != null;
            var nameLabel = popup ? agentSpeakerLabel : speakerLabel;
            activeLabel = popup ? agentTextLabel : textLabel;
            if (nameLabel != null)
            {
                nameLabel.text = style.displayName ?? "";
                nameLabel.gameObject.SetActive(!string.IsNullOrEmpty(style.displayName));
            }
            fullText = line.text ?? "";
            revealing = typewriterCharsPerSecond > 0f && fullText.Length > 0;
            revealStart = Time.time;
            activeLabel.text = revealing ? "" : fullText;
            (popup ? agentRoot : root).SetActive(true);
            if (popup) ResizeAgentPopup();
        }

        // 正在显示一句台词（字幕或 Agent 弹窗）
        public bool IsShowingLine => root.activeSelf || (agentRoot != null && agentRoot.activeSelf);
        // 打字机还没打完
        public bool IsRevealing => revealing;

        // 立刻显示整句
        public void CompleteReveal()
        {
            if (!revealing) return;
            revealing = false;
            if (activeLabel != null) activeLabel.text = fullText;
        }

        // 玩家要求推进：打字中先补全整句，已经打完就跳到下一句
        public void Advance()
        {
            if (!IsShowingLine || player == null || !player.IsPlaying) return;
            if (revealing) CompleteReveal();
            else player.Skip();
        }

        public void Hide()
        {
            revealing = false;
            textLabel.text = "";
            root.SetActive(false);
            if (agentTextLabel != null) agentTextLabel.text = "";
            if (agentRoot != null) agentRoot.SetActive(false);
        }

        void HideLine(DialogueLine line) => Hide();

        // Agent 弹窗高度按整句（不是打字机进度）算，避免打字时弹窗一直变高、把下方详情弹窗挤来挤去
        void ResizeAgentPopup()
        {
            var rect = agentRoot.transform as RectTransform;
            var body = agentTextLabel.rectTransform;
            if (rect == null) return;
            string shown = agentTextLabel.text;
            agentTextLabel.text = fullText;
            float bodyH = agentTextLabel.preferredHeight;
            agentTextLabel.text = shown;
            float top = -body.offsetMax.y, bottom = body.offsetMin.y;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, top + bodyH + bottom);
        }

        SpeakerStyle StyleOf(Speaker speaker)
        {
            foreach (var s in styles)
                if (s.speaker == speaker) return s;
            return new SpeakerStyle { speaker = speaker, channel = Channel.Subtitle, displayName = "" };
        }
    }
}
