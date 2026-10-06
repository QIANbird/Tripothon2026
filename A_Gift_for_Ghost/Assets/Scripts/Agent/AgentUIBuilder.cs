using Ghost.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // 用代码生成三个 Agent 界面的完整层级（Canvas、文字、按钮、Collider），并填好组件引用。
    // 编辑器菜单（生成场景）和运行时都能调用。默认摆放按主场景的固定相机设计：
    // 相机在 (0, 1.6, -1.9)，植株在原点、台面 0.75 m。
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

        // 任务面板：植株左侧，离相机约 1.5 m（面板顶边位置）
        public static readonly Vector3 DefaultTaskPanelPosition = new Vector3(-0.84f, 1.46f, -0.6f);
        // 询问框：植株右侧，离相机约 1.4 m，在植株前面（Yes 按钮的 Collider 比节点近，不会被挡），不遮住植株和详情弹窗
        public static readonly Vector3 DefaultQueryPosition = new Vector3(0.82f, 1.26f, -0.6f);

        // 叠放顺序：任务面板 0 < 详情弹窗 < 询问框
        public const int PopupSortingOrder = 10;
        public const int QuerySortingOrder = 20;

        public static AgentUI BuildAll(Transform parent, Camera camera)
        {
            var root = new GameObject("AgentUI");
            if (parent != null) root.transform.SetParent(parent, false);
            var ui = new AgentUI { root = root };
            ui.taskPanel = BuildTaskPanel(root.transform, camera, DefaultTaskPanelPosition);
            ui.detailPopup = BuildDetailPopup(root.transform, camera);
            ui.query = BuildQueryDialog(root.transform, camera, DefaultQueryPosition);
            return ui;
        }

        // ---- 任务面板 ----

        public static AgentTaskPanel BuildTaskPanel(Transform parent, Camera camera, Vector3 position)
        {
            var host = NewInactiveHost("AgentTaskPanel", parent);
            var p = host.AddComponent<AgentTaskPanel>();

            var canvas = AgentUIStyle.CreateWorldCanvas("Canvas", host.transform, camera, position,
                new Vector2(p.width, 400f), new Vector2(0.5f, 1f));
            var panel = (RectTransform)canvas.transform;
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

            var canvas = AgentUIStyle.CreateWorldCanvas("Canvas", host.transform, camera, Vector3.zero,
                new Vector2(p.width, 200f), new Vector2(0f, 0.5f));
            var panel = (RectTransform)canvas.transform;
            // 弹窗压在任务面板之上（World Space Canvas 同层时 sortingOrder 生效）
            canvas.sortingOrder = PopupSortingOrder;

            // 引线和锚点方块先建，画在弹窗下面
            var leader = AgentUIStyle.CreateRect("Leader", panel, Vector2.zero, new Vector2(100f, 3f));
            AgentUIStyle.AddImage(leader, AgentUIStyle.BlueGray);
            var dot = AgentUIStyle.CreateRect("AnchorDot", panel, Vector2.zero, new Vector2(18f, 18f));
            AgentUIStyle.AddImage(dot, AgentUIStyle.BlueGray);
            AgentUIStyle.AddImage(AgentUIStyle.CreateStretch("Hole", dot, 4f), AgentUIStyle.PanelFill);

            var box = AgentUIStyle.CreateRect("Box", panel, Vector2.zero, new Vector2(p.width, 200f));
            AgentUIStyle.AddFramedBackground(box, AgentUIStyle.PanelFill, AgentUIStyle.PanelBorder, 2f);
            // 左侧蓝灰色竖条，和任务面板的方块呼应
            var bar = AgentUIStyle.CreateRect("Accent", box, Vector2.zero, new Vector2(6f, 0f));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.offsetMin = new Vector2(2f, 2f);
            bar.offsetMax = new Vector2(8f, -2f);
            AgentUIStyle.AddImage(bar, AgentUIStyle.BlueGray);

            var titleRect = AgentUIStyle.CreateRect("Title", box, Vector2.zero, new Vector2(100f, 48f));
            var title = AgentUIStyle.AddText(titleRect, "", 42, AgentUIStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var bodyRect = AgentUIStyle.CreateRect("Body", box, Vector2.zero, new Vector2(100f, 100f));
            var body = AgentUIStyle.AddText(bodyRect, "", 38, AgentUIStyle.Ink, TextAnchor.UpperLeft);
            body.lineSpacing = 1.1f;

            p.viewCamera = camera;
            p.panel = panel;
            p.box = box;
            p.titleLabel = title;
            p.bodyLabel = body;
            p.leader = leader;
            p.anchorDot = dot;
            host.SetActive(true);
            return p;
        }

        // ---- AI 询问框 ----

        public static AgentQueryDialog BuildQueryDialog(Transform parent, Camera camera, Vector3 position)
        {
            var host = NewInactiveHost("AgentQueryDialog", parent);
            var q = host.AddComponent<AgentQueryDialog>();

            var canvas = AgentUIStyle.CreateWorldCanvas("Canvas", host.transform, camera, position,
                new Vector2(q.width, 320f), new Vector2(0.5f, 0.5f));
            var panel = (RectTransform)canvas.transform;
            canvas.sortingOrder = QuerySortingOrder;
            AgentUIStyle.AddFramedBackground(panel, AgentUIStyle.PanelFill, AgentUIStyle.PanelBorder, 3f);

            var tick = AgentUIStyle.CreateRect("Tick", panel, new Vector2(q.padding, -q.padding - 9f), new Vector2(16f, 16f));
            AgentUIStyle.AddImage(tick, AgentUIStyle.BlueGray);
            var headerRect = AgentUIStyle.CreateRect("Header", panel, new Vector2(q.padding + 28f, -q.padding),
                new Vector2(q.width - q.padding * 2f - 28f, 34f));
            var header = AgentUIStyle.AddText(headerRect, q.header, 24, AgentUIStyle.Gray, TextAnchor.MiddleLeft, FontStyle.Bold);

            var questionRect = AgentUIStyle.CreateRect("Question", panel, Vector2.zero, new Vector2(100f, 60f));
            var question = AgentUIStyle.AddText(questionRect, "", 38, AgentUIStyle.Ink, TextAnchor.UpperLeft);
            question.lineSpacing = 1.1f;

            // Yes 按钮：深色块 + 反白字 + BoxCollider + InteractableButton
            var buttonRect = AgentUIStyle.CreateRect("YesButton", panel, Vector2.zero, new Vector2(q.buttonWidth, q.buttonHeight));
            var buttonImage = AgentUIStyle.AddImage(buttonRect, q.buttonColor);
            var collider = buttonRect.gameObject.AddComponent<BoxCollider>();
            var button = buttonRect.gameObject.AddComponent<InteractableButton>();
            var yesRect = AgentUIStyle.CreateStretch("Label", buttonRect);
            var yes = AgentUIStyle.AddText(yesRect, q.yesText, 40, q.buttonTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            q.panel = panel;
            q.headerLabel = header;
            q.questionLabel = question;
            q.yesButton = button;
            q.yesCollider = collider;
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
