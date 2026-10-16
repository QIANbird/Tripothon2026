using Ghost.Agent;
using Ghost.Interaction.XR;
using Ghost.Narrative;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ghost.Core.EditorTools
{
    // VR 版 UI（docs/tasks/vr-migration.md 第 4 步）：给 MainSceneMenu 建好的 HUD Canvas 挂 XRWorldCanvas，
    // 头显运行时换成 World Space，PC 上不改。只在场景构建时调用，不改各 UI 组件的构建代码。
    //   字幕          LazyFollow，头前 1.5 m、视野下方
    //   Agent 聊天栏   跟右手柄（左上方，和"是否批准"卡片错开），只有头像和气泡，没有底板
    //   询问框（YES）  视野正中、头前 0.55 m（比矩阵近），跟头、面向玩家，Yes 走手柄射线（XRUIRayTarget）
    //   详情弹窗       贴在被查看的节点旁边，在这个节点前方 0.1 m、朝向固定
    //   开始 / 结束    LazyFollow 的 3D 面板，按钮走手柄射线；背后一块跟界面同步淡入淡出的黑幕（XRScreenBackdrop），看不到关卡
    //   结局视频       LazyFollow 的大屏
    //   黑屏          挂在相机前 0.1 m
    //   阶段面板 / PC 键位提示 / 准星：不参与（占位、或只在 PC 用）
    public static class XRUIBuilder
    {

        public struct Refs
        {
            public Camera camera;
            public Transform viewAnchor;
            public Transform rightAim;
        }

        public static void Build(Refs refs)
        {
            var head = refs.camera.transform;

            var subtitles = Object.FindAnyObjectByType<SubtitlePanel>(FindObjectsInactive.Include);
            if (subtitles != null)
            {
                // 字幕：视野下方（头前 1.5 m、低 0.4 m ≈ 视线下 15°，10-16 实机调整），不挡中间的植株。
                // VR 下宽 900 px（0.9 m @ 1.5 m ≈ 33°），字号不变（30 px = 3 cm，约 1.1°）
                var w = Add(subtitles.GetComponent<Canvas>(), XRWorldCanvas.Placement.LazyFollow, head, null,
                    new Vector3(0f, -0.4f, 1.5f), 0.001f, subtitles.root.GetComponent<RectTransform>());
                w.contentSizePx = new Vector2(900f, 0f);
                AddLazyFollow(w);
                BuildChatFeed(subtitles.agentFeed, head, refs.rightAim);
            }
            else Debug.LogWarning("[XR UI] 场景里没有 SubtitlePanel，字幕 / 聊天栏未做 VR 处理");

            var blackout = Object.FindAnyObjectByType<ScreenBlackout>(FindObjectsInactive.Include);
            if (blackout != null)
            {
                // 黑屏：挂在相机前 0.1 m（近裁剪面 0.05 m），8000×8000 px × 0.0001 = 0.8 m 见方，覆盖整个视野。
                // 不加 always-on-top：字幕和聊天栏用 ZTest Always 画在它上面；Canvas 排序沿用 PC 的 sortingOrder
                var w = Add(blackout.GetComponent<Canvas>(), XRWorldCanvas.Placement.HeadLocked, head, null,
                    new Vector3(0f, 0f, 0.1f), 0.0001f, null);
                w.sizePx = new Vector2(8000f, 8000f);
                w.alwaysOnTop = false;
            }

            var query = Object.FindAnyObjectByType<AgentQueryDialog>(FindObjectsInactive.Include);
            if (query != null)
            {
                // 带 YES 的询问框：视野正中、头前 0.55 m（比 0.75 m 的矩阵近），跟头、面向玩家。
                // 0.6 mm/px：560 px 宽 ≈ 34 cm（约 34°）。转头超过 8° 就跟上，比字幕跟得紧
                var w = Add(query.GetComponentInChildren<Canvas>(true), XRWorldCanvas.Placement.LazyFollow, head, null,
                    new Vector3(0f, 0f, 0.55f), 0.0006f, query.panel);
                w.contentPivot = new Vector2(0.5f, 0.5f); // 摆放点 = 卡片中心
                AddLazyFollow(w, 3f, 8f, 8f);
                AddRayTarget(query.yesButton.gameObject, query.yesButton, w, 30f);
                // 卡片本身挡射线，按到卡片空白处不会选中后面的节点
                AddRayTarget(query.panel.gameObject, null, w, 10f);
            }

            var detail = Object.FindAnyObjectByType<NodeDetailPopup>(FindObjectsInactive.Include);
            var xrInput = Object.FindAnyObjectByType<XRControllerInput>(FindObjectsInactive.Include);
            if (detail != null)
            {
                // 详情：贴在被查看节点的右侧，平面在这个节点（含选中突起）正面前方 0.1 m、跟着节点，朝向固定。1 mm/px（470 px 宽 ≈ 47 cm）
                var w = Add(detail.GetComponentInChildren<Canvas>(true), XRWorldCanvas.Placement.Node, head, refs.viewAnchor,
                    Vector3.zero, 0.001f, detail.panel);
                w.planeGap = 0.1f;
                w.nodeSource = xrInput;
                w.contentPivot = new Vector2(0f, 0.5f); // 左边缘中点贴着节点，往右展开
                UnityEventTools.AddPersistentListener(w.worldModeChanged, detail.SetWorldLayout);
                if (xrInput == null) Debug.LogWarning("[XR UI] 场景里没有 XRControllerInput，VR 详情面板没有位置");
            }

            foreach (var screen in Object.FindObjectsByType<FlowScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 开始 / 结束界面：头前 2 m 的 16:9 面板（1920 × 0.0012 = 2.3 m 宽，约 60°）
                var w = Add(screen.GetComponent<Canvas>(), XRWorldCanvas.Placement.LazyFollow, head, null,
                    new Vector3(0f, 0f, 2f), 0.0012f, null);
                AddLazyFollow(w);
                AddRayTarget(screen.button.gameObject, screen.button, w, 30f);
                // 整块黑底挡射线：按在界面上不会选中后面的节点（第一个子物体就是 Background）
                if (screen.transform.childCount > 0) AddRayTarget(screen.transform.GetChild(0).gameObject, null, w, 10f);
                BuildScreenBackdrop(screen, head);
            }

            var outro = Object.FindAnyObjectByType<Ghost.Stages.OutroVideoStage>(FindObjectsInactive.Include);
            if (outro != null && outro.screen != null)
            {
                var canvas = outro.screen.GetComponent<Canvas>();
                var w = Add(canvas, XRWorldCanvas.Placement.LazyFollow, head, null, new Vector3(0f, 0f, 2.2f), 0.0012f, null);
                AddLazyFollow(w);
                if (outro.screen.transform.childCount > 0) AddRayTarget(outro.screen.transform.GetChild(0).gameObject, null, w, 10f);
            }
        }

        // 聊天栏：跟右手柄，在手柄左上方（批准卡片在右上方）。右下角是摆放点，消息往上堆、最新一条在最下面靠近手柄。
        // 0.5 mm/px：470 px 宽 ≈ 24 cm，24 px 字 ≈ 12 mm（手柄离眼约 0.5 m，约 1.4°）
        static void BuildChatFeed(AgentChatFeed feed, Transform head, Transform rightAim)
        {
            if (feed == null) return;
            var canvas = feed.GetComponentInParent<Canvas>(true);
            var w = Add(canvas, XRWorldCanvas.Placement.Controller, head, rightAim,
                new Vector3(-0.05f, 0.11f, 0.14f), 0.0005f, feed.feedRoot);
            w.contentPivot = new Vector2(1f, 0f);
        }

        // 开始 / 结束界面背后的黑幕（VR）：和黑屏一样挂在相机前 0.1 m 覆盖整个视野，透明度跟界面走，
        // 开场只看到黑底上的 AWAKE 面板，看不到后面的关卡。排序比界面低一档
        static void BuildScreenBackdrop(FlowScreen screen, Transform head)
        {
            var screenCanvas = screen.GetComponent<Canvas>();
            var canvas = AgentUIStyle.CreateHudCanvas(screen.name + "BackdropXR", null, screenCanvas.sortingOrder - 1);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            AgentUIStyle.AddImage(AgentUIStyle.CreateStretch("Fill", canvas.transform), Color.black).raycastTarget = false;

            var backdrop = canvas.gameObject.AddComponent<XRScreenBackdrop>();
            backdrop.source = screen.group;
            backdrop.group = group;

            var w = Add(canvas, XRWorldCanvas.Placement.HeadLocked, head, null, new Vector3(0f, 0f, 0.1f), 0.0001f, null);
            w.sizePx = new Vector2(8000f, 8000f);
            w.alwaysOnTop = false;
        }

        static XRWorldCanvas Add(Canvas canvas,XRWorldCanvas.Placement placement, Transform head, Transform reference,
            Vector3 offset, float metersPerPixel, RectTransform content)
        {
            var w = canvas.gameObject.AddComponent<XRWorldCanvas>();
            w.canvas = canvas;
            w.placement = placement;
            w.head = head;
            w.reference = reference;
            w.offset = offset;
            w.metersPerPixel = metersPerPixel;
            w.content = content;
            return w;
        }

        // LazyFollow：PC 上关着，进入 World Space 时由 XRWorldCanvas 打开。默认转头超过 20° 或位移超过 0.3 m 后跟上
        static void AddLazyFollow(XRWorldCanvas w, float minAngle = 5f, float maxAngle = 20f, float speed = 4f)
        {
            var follow = w.gameObject.AddComponent<LazyFollow>();
            follow.target = w.head;
            follow.targetOffset = w.offset;
            follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
            follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
            follow.movementSpeed = speed;
            follow.minDistanceAllowed = 0.05f;
            follow.maxDistanceAllowed = 0.3f;
            follow.timeUntilThresholdReachesMaxDistance = 1.5f;
            follow.minAngleAllowed = minAngle;
            follow.maxAngleAllowed = maxAngle;
            follow.timeUntilThresholdReachesMaxAngle = 1.5f;
            follow.snapOnEnable = true;
            follow.enabled = false;
            w.lazyFollow = follow;
        }

        static void AddRayTarget(GameObject go, Button button, XRWorldCanvas world, float thickness)
        {
            var target = go.AddComponent<XRUIRayTarget>();
            target.button = button;
            target.world = world;
            target.thickness = thickness;
        }
    }
}
