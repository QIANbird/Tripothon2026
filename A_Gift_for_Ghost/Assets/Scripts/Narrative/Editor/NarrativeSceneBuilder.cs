using Ghost.Agent;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Narrative.EditorTools
{
    // 供场景生成菜单（MainSceneMenu）调用：搭建对白播放器、字幕面板、黑屏，并把开场阶段配好
    public static class NarrativeSceneBuilder
    {
        // 在 stageGo 上加 IntroStage，并在场景里建好它需要的对象
        public static IntroStage AddIntroStage(GameObject stageGo, Transform cameraTransform)
        {
            var player = EnsureDialogue(cameraTransform);
            var stage = stageGo.AddComponent<IntroStage>();
            stage.player = player;
            stage.sequence = NarrativeAssets.EnsureIntroSequence();
            stage.blackout = BuildBlackout(cameraTransform);
            NarrativeAssets.EnsureNodeDetails();
            return stage;
        }

        // 整个场景只建一套 DialoguePlayer + SubtitlePanel。
        // 其他阶段在菜单里拿这个返回值引用，或运行时 FindAnyObjectByType<DialoguePlayer>()
        public static DialoguePlayer EnsureDialogue(Transform cameraTransform)
        {
            var existing = Object.FindAnyObjectByType<DialoguePlayer>();
            if (existing != null) return existing;

            var go = new GameObject("Dialogue");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var player = go.AddComponent<DialoguePlayer>();
            player.audioSource = source;
            BuildSubtitlePanel(player);
            return player;
        }

        static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 排序：黑屏 100 < 字幕 / Agent 弹窗 200（详情 210、询问框 220 在 AgentUIBuilder）
        public const int BlackoutSortingOrder = 100;
        public const int DialogueSortingOrder = 200;

        // 【技术债】比赛期间改为 Screen Space Overlay HUD（1920×1080 参考），赛后改回 World Space（docs/VR_GUIDELINES.md 第 5 节）。
        // 字幕：屏幕下方居中，一行小字（30 px），宽 1400 px，长句折两行；没有深色底板。
        // Agent 弹窗：屏幕左上角，带浅色底板（黑屏时也清楚），署名 + 正文
        static SubtitlePanel BuildSubtitlePanel(DialoguePlayer player)
        {
            var canvas = AgentUIStyle.CreateHudCanvas("DialogueHUD", null, DialogueSortingOrder);
            var canvasRect = (RectTransform)canvas.transform;

            // ---- 字幕 ----
            var subRoot = AgentUIStyle.CreateAnchored("Subtitle", canvasRect, new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(1400f, 110f));
            var speaker = MakeText(subRoot, "Speaker", 22, FontStyle.Normal);
            var speakerRect = speaker.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.sizeDelta = new Vector2(0f, 30f);
            speakerRect.anchoredPosition = Vector2.zero;
            speaker.alignment = TextAnchor.LowerCenter;

            var body = MakeText(subRoot, "Text", 30, FontStyle.Normal);
            var bodyRect = body.rectTransform;
            Stretch(bodyRect);
            bodyRect.offsetMin = new Vector2(0f, 0f);
            bodyRect.offsetMax = new Vector2(0f, -34f);
            body.alignment = TextAnchor.UpperCenter;
            body.lineSpacing = 1.05f;

            // ---- Agent 弹窗 ----
            float w = AgentUIStyle.HudLeftColumnWidth, m = AgentUIStyle.HudMargin;
            var agentRoot = AgentUIStyle.CreateAnchored("AgentMessage", canvasRect, new Vector2(0f, 1f),
                new Vector2(m, -m), new Vector2(w, 140f));
            AgentUIStyle.AddFramedBackground(agentRoot, AgentUIStyle.PanelFill, AgentUIStyle.PanelBorder, 2f);
            var tick = AgentUIStyle.CreateRect("Tick", agentRoot, new Vector2(20f, -24f), new Vector2(12f, 12f));
            AgentUIStyle.AddImage(tick, AgentUIStyle.BlueGray).raycastTarget = false;
            var agentName = MakeText(agentRoot, "Speaker", 22, FontStyle.Bold);
            var anRect = agentName.rectTransform;
            anRect.anchorMin = new Vector2(0f, 1f);
            anRect.anchorMax = new Vector2(1f, 1f);
            anRect.pivot = new Vector2(0f, 1f);
            anRect.anchoredPosition = new Vector2(42f, -14f);
            anRect.sizeDelta = new Vector2(-62f, 32f);
            agentName.color = AgentUIStyle.Gray;
            agentName.alignment = TextAnchor.MiddleLeft;
            var agentText = MakeText(agentRoot, "Text", 28, FontStyle.Normal);
            var atRect = agentText.rectTransform;
            Stretch(atRect);
            atRect.offsetMin = new Vector2(20f, 20f);
            atRect.offsetMax = new Vector2(-20f, -56f);
            agentText.color = AgentUIStyle.Ink;
            agentText.lineSpacing = 1.1f;

            var panel = canvas.gameObject.AddComponent<SubtitlePanel>();
            panel.player = player;
            panel.root = subRoot.gameObject;
            panel.speakerLabel = speaker;
            panel.textLabel = body;
            panel.agentRoot = agentRoot.gameObject;
            panel.agentSpeakerLabel = agentName;
            panel.agentTextLabel = agentText;
            panel.blackout = Object.FindAnyObjectByType<ScreenBlackout>();
            subRoot.gameObject.SetActive(false);
            agentRoot.gameObject.SetActive(false);
            return panel;
        }

        // 黑屏：Screen Space Overlay 全屏黑块（比赛期间 HUD），排在字幕和 Agent 弹窗下面
        static ScreenBlackout BuildBlackout(Transform cam)
        {
            var canvas = AgentUIStyle.CreateHudCanvas("Blackout", null, BlackoutSortingOrder);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            var canvasGo = canvas.gameObject;

            var group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(canvasGo.transform, false);
            Stretch(fill.AddComponent<RectTransform>());
            var image = fill.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var blackout = canvasGo.AddComponent<ScreenBlackout>();
            blackout.group = group;
            // 字幕按黑屏切换黑 / 白字
            var subtitle = Object.FindAnyObjectByType<SubtitlePanel>();
            if (subtitle != null) subtitle.blackout = blackout;
            return blackout;
        }

        static Text MakeText(Transform parent, string name, int size, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
