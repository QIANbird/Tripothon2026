using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // 用代码生成三个 Agent 界面的完整层级（Canvas、文字、按钮），并填好组件引用。编辑器菜单和运行时都能调用。
    // 【技术债】比赛期间：详情弹窗、询问框、任务面板都是 Screen Space Overlay HUD（1920×1080 参考），
    // 赛后改回 World Space（docs/VR_GUIDELINES.md 第 5 节）。任务面板暂不使用（StageContext.showTaskPanel = false）。
    //
    //   var ui = AgentUIBuilder.BuildAll(null, camera);
    //   ui.taskPanel.SetTasks(...); ui.query.Ask("...", () => ...);
    public static class AgentUIBuilder
    {
        public struct AgentUI
        {
            public GameObject root;
            public AgentTaskPanel taskPanel;
            public NodeDetailPopup detailPopup;
            public AgentQueryDialog query;
        }

        // 叠放顺序：字幕 / Agent 剧情弹窗 200（NarrativeSceneBuilder）< 任务面板 205 < 详情 210 < 询问框 220
        public const int TaskPanelSortingOrder = 205;
        public const int PopupSortingOrder = 210;
        public const int QuerySortingOrder = 220;

        // 询问框：屏幕右侧中部（左侧栏留给 Agent 信息 / 详情）
        public static readonly Vector2 DefaultQueryOffset = new Vector2(-AgentUIStyle.HudMargin, 0f);

        public static AgentUI BuildAll(Transform parent, Camera camera)
        {
            var root = new GameObject("AgentUI");
            if (parent != null) root.transform.SetParent(parent, false);
            var ui = new AgentUI { root = root };
            ui.taskPanel = BuildTaskPanel(root.transform, camera, Vector3.zero);
            ui.detailPopup = BuildDetailPopup(root.transform, camera);
            ui.query = BuildQueryDialog(root.transform, camera, Vector3.zero);
            EnsureEventSystem();
            return ui;
        }

        // HUD 按钮需要 EventSystem + InputSystemUIInputModule（新输入系统，默认 UI 动作表）
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---- 任务面板 ----

        public static AgentTaskPanel BuildTaskPanel(Transform parent, Camera camera, Vector3 position)
        {
            var host = NewInactiveHost("AgentTaskPanel", parent);
            var p = host.AddComponent<AgentTaskPanel>();

            // HUD 左上角（和详情、Agent 剧情弹窗同一栏；暂时隐藏不用）。position 参数留给赛后的 World Space 版本
            var canvas = AgentUIStyle.CreateHudCanvas("Canvas", host.transform, TaskPanelSortingOrder);
            var panel = AgentUIStyle.CreateAnchored("Panel", canvas.transform, new Vector2(0f, 1f),
                new Vector2(AgentUIStyle.HudMargin, -AgentUIStyle.HudMargin), new Vector2(p.width, 400f));
            AgentUIStyle.AddFramedBackground(panel, AgentUIStyle.PanelFill, AgentUIStyle.PanelBorder, 2f);

            // 左上角的小方块 + 标题行
            var tick = AgentUIStyle.CreateRect("Tick", panel, new Vector2(p.padding, -p.padding - 9f), new Vector2(16f, 16f));
            AgentUIStyle.AddImage(tick, AgentUIStyle.BlueGray);
            var headerRect = AgentUIStyle.CreateRect("Header", panel, new Vector2(p.padding + 28f, -p.padding),
                new Vector2(300f, 34f));
            var header = AgentUIStyle.AddText(headerRect, p.header, 24, AgentUIStyle.Gray, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 右上角模式标签：深色小块 + 反白字
            const float chipW = 190f, chipH = 38f;
            var chip = AgentUIStyle.CreateRect("ModeChip", panel, new Vector2(p.width - p.padding - chipW, -p.padding + 2f),
                new Vector2(chipW, chipH));
            var chipImage = AgentUIStyle.AddImage(chip, AgentUIStyle.Ink);
            var modeRect = AgentUIStyle.CreateStretch("Mode", chip);
            var mode = AgentUIStyle.AddText(modeRect, p.modeText, 22, AgentUIStyle.PanelFill, TextAnchor.MiddleCenter, FontStyle.Bold);

            var titleRect = AgentUIStyle.CreateRect("Title", panel, new Vector2(p.padding, -p.padding - 44f),
                new Vector2(p.width - p.padding * 2f, 52f));
            var title = AgentUIStyle.AddText(titleRect, "", 40, AgentUIStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

            var content = AgentUIStyle.CreateRect("Content", panel, new Vector2(p.padding, -p.headerHeight),
                new Vector2(p.width - p.padding * 2f, 0f));

            p.panel = panel;
            p.headerLabel = header;
            p.modeLabel = mode;
            p.modeChip = chipImage;
            p.titleLabel = title;
            p.content = content;
            host.SetActive(true);
            return p;
        }

        // ---- 节点详情弹窗 ----

        public static NodeDetailPopup BuildDetailPopup(Transform parent, Camera camera)
        {
            var host = NewInactiveHost("NodeDetailPopup", parent);
            var p = host.AddComponent<NodeDetailPopup>();

            var canvas = AgentUIStyle.CreateHudCanvas("Canvas", host.transform, PopupSortingOrder);
            var panel = AgentUIStyle.CreateAnchored("Panel", canvas.transform, new Vector2(0f, 1f),
                p.topLeft, new Vector2(p.width, 160f));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // Agent弹窗_02 样式：头像 + 半透明灰气泡（和聊天栏一致）
            var round = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatRoundSprite);
            var circle = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatCircleSprite);
            var tailSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatTailSprite);
            var iconSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatIconSprite);
            const float avatarSize = 56f;
            var avatar = AgentUIStyle.CreateRect("Avatar", panel, Vector2.zero, new Vector2(avatarSize, avatarSize));
            AgentUIStyle.AddSprite(avatar, circle, AgentUIStyle.ChatAvatar);
            var icon = AgentUIStyle.CreateStretch("Icon", avatar, avatarSize * 0.22f);
            AgentUIStyle.AddSprite(icon, iconSprite, AgentUIStyle.ChatIcon);
            if (tailSprite != null)
            {
                var tail = AgentUIStyle.CreateRect("Tail", panel, new Vector2(p.bubbleOffset - 10f, -18f), new Vector2(18f, 16f));
                AgentUIStyle.AddSprite(tail, tailSprite, AgentUIStyle.ChatBubble);
            }

            var box = AgentUIStyle.CreateRect("Box", panel, new Vector2(p.bubbleOffset, 0f),
                new Vector2(p.width - p.bubbleOffset, 160f));
            AgentUIStyle.AddSprite(box, round, AgentUIStyle.ChatBubble, sliced: true);

            var titleRect = AgentUIStyle.CreateRect("Title", box, Vector2.zero, new Vector2(100f, 32f));
            var title = AgentUIStyle.AddText(titleRect, "", 24, AgentUIStyle.ChatText, TextAnchor.MiddleLeft, FontStyle.Bold);
            var bodyRect = AgentUIStyle.CreateRect("Body", box, Vector2.zero, new Vector2(100f, 100f));
            var body = AgentUIStyle.AddText(bodyRect, "", 22, AgentUIStyle.ChatText, TextAnchor.UpperLeft);
            body.lineSpacing = 1.1f;

            p.panel = panel;
            p.box = box;
            p.titleLabel = title;
            p.bodyLabel = body;
            p.group = group;
            p.stackBelow = FindAgentMessageRect();
            host.SetActive(true);
            return p;
        }

        // Agent 聊天栏（NarrativeSceneBuilder 建的 DialogueHUD/AgentFeed）；详情放在栏里所有消息的下方
        static RectTransform FindAgentMessageRect()
        {
            var subtitle = Object.FindAnyObjectByType<Ghost.Narrative.SubtitlePanel>(FindObjectsInactive.Include);
            return subtitle != null && subtitle.agentFeed != null ? subtitle.agentFeed.feedRoot : null;
        }

        // ---- 询问框 ----

        public static AgentQueryDialog BuildQueryDialog(Transform parent, Camera camera, Vector3 position)
        {
            var host = NewInactiveHost("AgentQueryDialog", parent);
            var q = host.AddComponent<AgentQueryDialog>();

            // HUD 屏幕右侧中部，Caution 卡样式（和聊天栏的 Agent弹窗_01 一致）+ YES。position 参数留给赛后的 World Space 版本
            var round = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatRoundSprite);
            var circle = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatCircleSprite);
            var iconSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatIconSprite);
            var headerSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatHeaderSprite);
            var canvas = AgentUIStyle.CreateHudCanvas("Canvas", host.transform, QuerySortingOrder);
            var panel = AgentUIStyle.CreateAnchored("Panel", canvas.transform, new Vector2(1f, 0.5f),
                DefaultQueryOffset, new Vector2(q.width, 260f));
            AgentUIStyle.AddSprite(panel, round, AgentUIStyle.CautionBody, sliced: true);

            var headerBar = AgentUIStyle.CreateRect("HeaderBar", panel, Vector2.zero, new Vector2(q.width, q.headerHeight));
            AgentUIStyle.AddSprite(headerBar, headerSprite != null ? headerSprite : round, AgentUIStyle.CautionHeader, sliced: true);
            float a = q.headerHeight - 10f;
            var avatar = AgentUIStyle.CreateRect("Avatar", headerBar, new Vector2(5f, -5f), new Vector2(a, a));
            AgentUIStyle.AddSprite(avatar, circle, AgentUIStyle.ChatAvatar);
            var icon = AgentUIStyle.CreateStretch("Icon", avatar, a * 0.22f);
            AgentUIStyle.AddSprite(icon, iconSprite, AgentUIStyle.ChatIcon);
            var headerRect = AgentUIStyle.CreateRect("Header", headerBar, new Vector2(a + 18f, 0f),
                new Vector2(q.width - a - 36f, q.headerHeight));
            var header = AgentUIStyle.AddText(headerRect, q.header, 24, AgentUIStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

            var questionRect = AgentUIStyle.CreateRect("Question", panel, Vector2.zero, new Vector2(100f, 60f));
            var question = AgentUIStyle.AddText(questionRect, "", 28, AgentUIStyle.Ink, TextAnchor.UpperLeft);
            question.lineSpacing = 1.1f;

            // Yes 按钮：深色圆角块 + 反白字，UGUI Button（EventSystem 点击）
            var buttonRect = AgentUIStyle.CreateRect("YesButton", panel, Vector2.zero, new Vector2(q.buttonWidth, q.buttonHeight));
            var buttonImage = AgentUIStyle.AddSprite(buttonRect, round, q.buttonColor, sliced: true);
            buttonImage.raycastTarget = true;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.None;
            var yesRect = AgentUIStyle.CreateStretch("Label", buttonRect);
            var yes = AgentUIStyle.AddText(yesRect, q.yesText, 28, q.buttonTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            q.panel = panel;
            q.headerLabel = header;
            q.questionLabel = question;
            q.yesButton = button;
            q.yesBackground = buttonImage;
            q.yesLabel = yes;
            question.text = "（AI 询问）";
            q.Resize();
            host.SetActive(true);
            return q;
        }

        // 先停用宿主物体再加组件，保证运行时 Awake 执行时引用已经填好
        static GameObject NewInactiveHost(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
    }
}
