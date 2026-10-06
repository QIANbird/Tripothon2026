using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Narrative
{
    // World Space 字幕面板：订阅 DialoguePlayer，显示说话人名字和台词，颜色按说话人区分。
    // 面板本身放在场景里玩家前方（由场景生成菜单摆放），不跟随鼠标、不依赖悬停。
    // 文字用 UGUI Text + 内置动态字体（和 StagePanel 一样），中文由系统字体补字。
    public class SubtitlePanel : MonoBehaviour
    {
        [System.Serializable]
        public struct SpeakerStyle
        {
            public Speaker speaker;
            [Tooltip("字幕上显示的名字；为空时不显示名字行")]
            public string displayName;
            public Color color;
        }

        public DialoguePlayer player;
        [Tooltip("整个面板的根（含底板），没有台词时隐藏")]
        public GameObject root;
        public Text speakerLabel;
        public Text textLabel;
        [Tooltip("打字机效果的速度（字 / 秒）；0 = 一次显示整句")]
        public float typewriterCharsPerSecond = 30f;

        public SpeakerStyle[] styles =
        {
            new SpeakerStyle { speaker = Speaker.EmotionalFemale, displayName = "她", color = new Color(1f, 0.80f, 0.60f) },
            new SpeakerStyle { speaker = Speaker.MechanicalFemale, displayName = "系统", color = new Color(0.62f, 0.74f, 0.86f) },
            new SpeakerStyle { speaker = Speaker.Protagonist, displayName = "我", color = new Color(0.95f, 0.95f, 0.95f) },
            new SpeakerStyle { speaker = Speaker.Agent, displayName = "Agent", color = new Color(0.55f, 0.88f, 0.80f) },
        };

        string fullText = "";
        float revealStart;
        bool revealing;

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
            var style = StyleOf(line.speaker);
            if (speakerLabel != null)
            {
                speakerLabel.text = style.displayName ?? "";
                speakerLabel.color = style.color;
                speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(style.displayName));
            }
            textLabel.color = style.color;
            fullText = line.text ?? "";
            revealing = typewriterCharsPerSecond > 0f && fullText.Length > 0;
            revealStart = Time.time;
            textLabel.text = revealing ? "" : fullText;
            root.SetActive(true);
        }

        public void Hide()
        {
            revealing = false;
            textLabel.text = "";
            root.SetActive(false);
        }

        void HideLine(DialogueLine line) => Hide();

        SpeakerStyle StyleOf(Speaker speaker)
        {
            foreach (var s in styles)
                if (s.speaker == speaker) return s;
            return new SpeakerStyle { speaker = speaker, displayName = speaker.ToString(), color = Color.white };
        }
    }
}
