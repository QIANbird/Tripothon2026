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
            BuildSubtitlePanel(player, cameraTransform);
            return player;
        }

        static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 字幕：相机前方 1.2 m、略低于视线，1.4 m × 0.4 m（够放 4 行）；排序在黑屏之上
        static SubtitlePanel BuildSubtitlePanel(DialoguePlayer player, Transform cam)
        {
            var canvasGo = new GameObject("SubtitlePanel");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam.GetComponent<Camera>();
            canvas.sortingOrder = 200;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1400f, 400f);
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            canvasGo.transform.position = cam.position + cam.forward * 1.2f - cam.up * 0.3f;
            canvasGo.transform.rotation = Quaternion.LookRotation(canvasGo.transform.position - cam.position, cam.up);

            // 底板：半透明深色，亮背景下也能看清；没有台词时整个隐藏
            var plate = new GameObject("Plate");
            plate.transform.SetParent(canvasGo.transform, false);
            Stretch(plate.AddComponent<RectTransform>());
            var image = plate.AddComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);
            image.raycastTarget = false;

            var speaker = MakeText(plate.transform, "Speaker", 40, FontStyle.Bold);
            var speakerRect = speaker.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.sizeDelta = new Vector2(-80f, 56f);
            speakerRect.anchoredPosition = new Vector2(0f, -24f);

            var body = MakeText(plate.transform, "Text", 52, FontStyle.Normal);
            var bodyRect = body.rectTransform;
            Stretch(bodyRect);
            bodyRect.offsetMin = new Vector2(40f, 20f);
            bodyRect.offsetMax = new Vector2(-40f, -88f);
            body.lineSpacing = 1.1f;

            var panel = canvasGo.AddComponent<SubtitlePanel>();
            panel.player = player;
            panel.root = plate;
            panel.speakerLabel = speaker;
            panel.textLabel = body;
            plate.SetActive(false);
            return panel;
        }

        // 黑屏：相机的子物体，前方 1.4 m 一块 4 m × 4 m 的黑板（比植株和阶段面板近、比字幕远），盖住整个视野
        static ScreenBlackout BuildBlackout(Transform cam)
        {
            var canvasGo = new GameObject("Blackout");
            canvasGo.transform.SetParent(cam, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam.GetComponent<Camera>();
            canvas.sortingOrder = 100;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4000f, 4000f);
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            canvasGo.transform.localPosition = new Vector3(0f, 0f, 1.4f);
            canvasGo.transform.localRotation = Quaternion.identity;

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
