using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Narrative
{
    // 对白显示（比赛期间为 Screen Space HUD，【技术债】赛后改回 World Space）。按策划规则把台词分到两处：
    //   字幕（屏幕下方一行小字，可折两行）：EmotionalFemale（署名"亲切的声音"）、Protagonist（暂按字幕、无署名，待策划确认）。
    //     浅色背景用黑字；黑屏（blackout 不透明）时用白字。不加深色底板。
    //   Agent 聊天栏（屏幕左侧，AgentChatFeed）：MechanicalFemale 和 Agent 都归这一类，不显示署名。
    //     每句是一条新消息，向下堆叠，最上面那条播完 3 s 后淡出。通道 AgentCaution = Caution 标签卡，其余 = 聊天气泡。
    //   台词的 channel 不是 Auto 时，按台词指定的通道显示，例如亲切的声音出现在 Agent 聊天栏里。
    //   固定消息（Pin）：栏里常驻的一条聊天气泡（例如 S4 的除虫进度），再次 Pin 只改文字，Unpin 才淡出。
    //   离开阶段时 StageContext.ResetShared 会 Unpin 并清空聊天栏。
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
        // 字幕描边 / 外发光只在需要的阶段打开（目前只有 PICK，由 PickStage 进出时开关）；默认关
        public bool OutlineEnabled { get; set; }

        [Tooltip("描边和外发光的整体透明度（乘在两个颜色的 alpha 上）")]
        [Range(0f, 1f)] public float outlineOpacity = 1f;
        [Tooltip("字幕白色描边（浅色背景时显示，黑屏时淡掉）")]
        public Color outlineColor = new Color(1f, 1f, 1f, 0.95f);
        public Vector2 outlineDistance = new Vector2(1.5f, -1.5f);
        [Tooltip("外发光：更大、更淡的一圈白色描边")]
        public Color glowColor = new Color(1f, 1f, 1f, 0.45f);
        [Tooltip("外发光的距离决定描边最外圈有多宽；想让描边变细主要调这个")]
        public Vector2 glowDistance = new Vector2(2f, -2f);

        [Header("Agent 聊天栏（屏幕左侧）")]
        public AgentChatFeed agentFeed;

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
        bool pinned;
        float revealStart;
        bool revealing;
        bool agentLineActive; // 当前这句显示在 Agent 聊天栏
        Outline textOutline, textGlow, speakerOutline;

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
            player.Stopped += HandleStopped;
            if (agentFeed != null) agentFeed.typewriterCharsPerSecond = typewriterCharsPerSecond;
            EnsureOutlines();
            HideSubtitle();
        }

        void OnDisable()
        {
            if (player == null) return;
            player.LineStarted -= Show;
            player.LineFinished -= HideLine;
            player.Stopped -= HandleStopped;
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
                // 黑屏时字已经是白的，描边 / 发光跟着淡掉
                float k = OutlineEnabled ? (1f - dark) * outlineOpacity : 0f;
                // 每帧同步，Play 中改 Inspector 立刻生效
                if (textOutline != null)
                {
                    textOutline.effectColor = Fade(outlineColor, k);
                    textOutline.effectDistance = outlineDistance;
                }
                if (textGlow != null)
                {
                    textGlow.effectColor = Fade(glowColor, k);
                    textGlow.effectDistance = glowDistance;
                }
                if (speakerOutline != null)
                {
                    speakerOutline.effectColor = Fade(outlineColor, k * 0.8f);
                    speakerOutline.effectDistance = outlineDistance;
                }
            }

            // 字幕的打字机；Agent 聊天栏自己打字
            if (!revealing) return;
            int count = Mathf.FloorToInt((Time.time - revealStart) * typewriterCharsPerSecond);
            if (count >= fullText.Length)
            {
                textLabel.text = fullText;
                revealing = false;
            }
            else textLabel.text = fullText.Substring(0, Mathf.Max(0, count));
        }

        public void Show(DialogueLine line)
        {
            HideSubtitle();
            agentLineActive = false;
            var style = StyleOf(line.speaker);
            bool caution = line.channel == LineChannel.AgentCaution;
            var channel = line.channel == LineChannel.Subtitle ? Channel.Subtitle
                : line.channel == LineChannel.AgentPopup || caution ? Channel.AgentPopup
                : style.channel;
            string text = line.text ?? "";

            if (channel == Channel.AgentPopup && agentFeed != null)
            {
                agentLineActive = true;
                agentFeed.Push(caution ? AgentChatFeed.Kind.Caution : AgentChatFeed.Kind.Chat, text);
                return;
            }

            if (speakerLabel != null)
            {
                speakerLabel.text = style.displayName ?? "";
                speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(style.displayName));
            }
            fullText = text;
            revealing = typewriterCharsPerSecond > 0f && fullText.Length > 0;
            revealStart = Time.time;
            textLabel.text = revealing ? "" : fullText;
            root.SetActive(true);
        }

        // 固定一条 Agent 聊天栏消息（再次调用 = 更新文字）
        public void Pin(Speaker speaker, string text)
        {
            if (agentFeed == null) return;
            pinned = true;
            agentFeed.Pin(text ?? "");
        }

        public void Unpin()
        {
            if (!pinned) return;
            pinned = false;
            if (agentFeed != null) agentFeed.Unpin();
        }

        public bool HasPinned => pinned;

        // 跳关时清空 Agent 聊天栏（不做动画）
        public void ClearAgentFeed()
        {
            pinned = false;
            agentLineActive = false;
            if (agentFeed != null) agentFeed.ClearImmediate(keepPinned: false);
        }

        // 正在显示一句台词（字幕或 Agent 弹窗）
        public bool IsShowingLine => root.activeSelf || agentLineActive;
        // 打字机还没打完
        public bool IsRevealing => agentLineActive ? agentFeed != null && agentFeed.IsRevealing : revealing;

        // 立刻显示整句
        public void CompleteReveal()
        {
            if (agentLineActive)
            {
                if (agentFeed != null) agentFeed.CompleteReveal();
                return;
            }
            if (!revealing) return;
            revealing = false;
            textLabel.text = fullText;
        }

        // 没有对白在播时玩家要求推进（例如开场"点击屏幕任意处"继续）
        public event System.Action AdvancedWhileIdle;

        // 玩家要求推进：打字中先补全整句，已经打完就跳到下一句；没有对白在播时发 AdvancedWhileIdle
        public void Advance()
        {
            if (player == null) return;
            if (!player.IsPlaying)
            {
                AdvancedWhileIdle?.Invoke();
                return;
            }
            if (!IsShowingLine) return;
            if (IsRevealing) CompleteReveal();
            else player.Skip();
        }

        // 收起字幕；Agent 聊天栏里已有的消息按自己的时间淡出（跳关用 ClearAgentFeed）
        public void Hide()
        {
            HideSubtitle();
            if (agentLineActive && agentFeed != null) agentFeed.MarkFinished();
            agentLineActive = false;
        }

        void HideSubtitle()
        {
            revealing = false;
            textLabel.text = "";
            root.SetActive(false);
        }

        void HideLine(DialogueLine line) => Hide();

        // 被 Stop / Play 打断：字幕收起；当前 Agent 台词按已播完计时，栏里已有消息留着
        void HandleStopped()
        {
            Hide();
        }

        // 白色描边 + 外发光（两层 Outline：里层实、外层淡）。场景里没有时运行时补上，不用重建场景
        void EnsureOutlines()
        {
            if (textOutline != null) return;
            var existing = textLabel.GetComponents<Outline>();
            textOutline = existing.Length > 0 ? existing[0] : textLabel.gameObject.AddComponent<Outline>();
            textGlow = existing.Length > 1 ? existing[1] : textLabel.gameObject.AddComponent<Outline>();
            textOutline.effectDistance = outlineDistance;
            textGlow.effectDistance = glowDistance;
            if (speakerLabel != null)
            {
                speakerOutline = speakerLabel.GetComponent<Outline>();
                if (speakerOutline == null) speakerOutline = speakerLabel.gameObject.AddComponent<Outline>();
                speakerOutline.effectDistance = outlineDistance;
            }
        }

        static Color Fade(Color c, float k) => new Color(c.r, c.g, c.b, c.a * k);

        SpeakerStyle StyleOf(Speaker speaker)
        {
            foreach (var s in styles)
                if (s.speaker == speaker) return s;
            return new SpeakerStyle { speaker = speaker, channel = Channel.Subtitle, displayName = "" };
        }
    }
}
