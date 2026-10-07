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

            var box = AgentUIStyle.CreateRect("Box", panel, Vector2.zero, new Vector2(p.width, 160f));
            AgentUIStyle.AddFramedBackground(box, AgentUIStyle.PanelFill, AgentUIStyle.PanelBorder, 2f);
            // 左侧蓝灰色竖条
            var bar = AgentUIStyle.CreateRect("Accent", box, Vector2.zero, new Vector2(6f, 0f));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.offsetMin = new Vector2(2f, 2f);
            bar.offsetMax = new Vector2(8f, -2f);
            AgentUIStyle.AddImage(bar, AgentUIStyle.BlueGray);

            var titleRect = AgentUIStyle.CreateRect("Title", box, Vector2.zero, new Vector2(100f, 40f));
            var title = AgentUIStyle.AddText(titleRect, "", 28, AgentUIStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var bodyRect = AgentUIStyle.CreateRect("Body", box, Vector2.zero, new Vector2(100f, 100f));
            var body = AgentUIStyle.AddText(bodyRect, "", 24, AgentUIStyle.Gray, TextAnchor.UpperLeft);
            body.lineSpacing = 1.15f;

            p.panel = panel;
            p.box = box;
            p.titleLabel = title;
            p.bodyLabel = body;
            p.group = group;
            p.stackBelow = FindAgentMessageRect();
            host.SetActive(true);
            return p;
        }

        // Agent 剧情弹窗（NarrativeSceneBuilder 建的 DialogueHUD/AgentMessage）；详情和它同时出现时放在它下方
        static RectTransform FindAgentMessageRect()
        {
            var subtitle = Object.FindAnyObjectByType<Ghost.Narrative.SubtitlePanel>(FindObjectsInactive.Include);
            return subtitle != null && subtitle.agentRoot != null ? subtitle.agentRoot.transform as RectTransform : null;
        }

        // ---- 询问框 ----

        public static AgentQueryDialog BuildQueryDialog(Transform parent, Camera camera, Vector3 position)
        {
            var host = NewInactiveHost("AgentQueryDialog", parent);
            var q = host.AddComponent<AgentQueryDialog>();

            // HUD 屏幕右侧中部。position 参数留给赛后的 World Space 版本
            var canvas = AgentUIStyle.CreateHudCanvas("Canvas", host.transform, QuerySortingOrder);
            var panel = AgentUIStyle.CreateAnchored("Panel", canvas.transform, new Vector2(1f, 0.5f),
                DefaultQueryOffset, new Vector2(q.width, 260f));
            AgentUIStyle.AddFramedBackground(panel, AgentUIStyle.PanelFill, AgentUIStyle.PanelBorder, 3f);

            var tick = AgentUIStyle.CreateRect("Tick", panel, new Vector2(q.padding, -q.padding - 9f), new Vector2(16f, 16f));
            AgentUIStyle.AddImage(tick, AgentUIStyle.BlueGray);
            var headerRect = AgentUIStyle.CreateRect("Header", panel, new Vector2(q.padding + 28f, -q.padding),
                new Vector2(q.width - q.padding * 2f - 28f, 34f));
            var header = AgentUIStyle.AddText(headerRect, q.header, 20, AgentUIStyle.Gray, TextAnchor.MiddleLeft, FontStyle.Bold);

            var questionRect = AgentUIStyle.CreateRect("Question", panel, Vector2.zero, new Vector2(100f, 60f));
            var question = AgentUIStyle.AddText(questionRect, "", 30, AgentUIStyle.Ink, TextAnchor.UpperLeft);
            question.lineSpacing = 1.1f;

            // Yes 按钮：深色块 + 反白字，UGUI Button（EventSystem 点击）
            var buttonRect = AgentUIStyle.CreateRect("YesButton", panel, Vector2.zero, new Vector2(q.buttonWidth, q.buttonHeight));
            var buttonImage = AgentUIStyle.AddImage(buttonRect, q.buttonColor);
            buttonImage.raycastTarget = true;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.None;
            var yesRect = AgentUIStyle.CreateStretch("Label", buttonRect);
            var yes = AgentUIStyle.AddText(yesRect, q.yesText, 30, q.buttonTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);

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
