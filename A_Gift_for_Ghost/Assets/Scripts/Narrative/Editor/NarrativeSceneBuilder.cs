using Ghost.Agent;
using Ghost.Agent.EditorTools;
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
            // 对白来自台词表 INTRO 段（DialogueCsvImporter 生成的 Script/INTRO_*.asset）；找不到时退回旧的开场资产
            stage.sequence = LoadScript("INTRO_001");
            if (stage.sequence == null) stage.sequence = NarrativeAssets.EnsureIntroSequence();
            stage.afterClickSequence = LoadScript("INTRO_009");
            stage.blackout = BuildBlackout(cameraTransform);
            stage.subtitles = Object.FindAnyObjectByType<SubtitlePanel>();
            NarrativeAssets.EnsureNodeDetails();
            return stage;
        }

        static DialogueSequence LoadScript(string key)
        {
            var seq = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueSequence>($"{DialogueCsvImporter.OutFolder}/{key}.asset");
            if (seq == null) Debug.LogWarning($"[Dialogue] 找不到台词表对白 {key}，先用菜单 Ghost/Narrative/Import Dialogue CSV 导入");
            return seq;
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
        // Agent 聊天栏：屏幕左上角，聊天气泡 / Caution 卡向下堆叠（docs/tasks/agent-chat-bubbles.md）
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

            // ---- Agent 聊天栏（左上锚定，消息向下堆叠，AgentChatFeed）----
            float w = AgentUIStyle.HudLeftColumnWidth, m = AgentUIStyle.HudMargin;
            var feedRoot = AgentUIStyle.CreateAnchored("AgentFeed", canvasRect, new Vector2(0f, 1f),
                new Vector2(m, -m), new Vector2(w, 0f));
            var feed = feedRoot.gameObject.AddComponent<AgentChatFeed>();
            feed.feedRoot = feedRoot;
            feed.itemParent = feedRoot;
            feed.width = w;
            AgentChatArtLoader.Assign(feed);

            var panel = canvas.gameObject.AddComponent<SubtitlePanel>();
            panel.player = player;
            panel.root = subRoot.gameObject;
            panel.speakerLabel = speaker;
            panel.textLabel = body;
            panel.agentFeed = feed;
            panel.blackout = Object.FindAnyObjectByType<ScreenBlackout>();
            subRoot.gameObject.SetActive(false);
            return panel;
        }

        // 黑屏：Screen Space Overlay 全屏黑块（比赛期间 HUD），排在字幕和 Agent 弹窗下面
        static ScreenBlackout BuildBlackout(Transform cam)
        {
            var canvas = AgentUIStyle.CreateHudCanvas("Blackout", null, BlackoutSortingOrder);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            // Screen Space Camera：在相机里画、排在后处理之前，黑屏上也能看到画面压暗边（Overlay 会盖住后处理）
            UseCameraSpace(canvas, cam);
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

        // 平面贴近近裁剪面（相机 near = 0.05 m），黑屏时场景物体不会穿到前面
        public static void UseCameraSpace(Canvas canvas, Transform cam)
        {
            var camera = cam != null ? cam.GetComponent<Camera>() : null;
            if (camera == null)
            {
                Debug.LogWarning("[Narrative] 黑屏没找到相机，保留 Screen Space Overlay（黑屏上看不到压暗边）");
                return;
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.02f;
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
