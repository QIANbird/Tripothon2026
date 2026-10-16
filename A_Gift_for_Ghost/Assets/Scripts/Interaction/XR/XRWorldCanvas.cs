using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ghost.Interaction.XR
{
    // VR 下把一块 HUD Canvas 换成 World Space（docs/tasks/vr-migration.md 第 4 步）。PC 上什么都不改，Canvas 保持构建时的模式。
    // 和 InteractionRouter 一样每帧看 XRSettings.isDeviceActive，头显中途连上 / 断开都能来回切；PC 原来的模式、尺寸、父物体都会还原。
    //
    // 摆放方式：
    //   LazyFollow  跟头（XRI LazyFollow）：转头超出死区后平滑跟上，始终朝向玩家。字幕、Caution 卡、开始 / 结束界面、结局视频
    //   Fixed       固定世界位置：reference（ViewAnchor，名义眼位）+ offset，朝向 reference。聊天栏、询问框
    //   HeadLocked  挂在头上：相机正前方 offset.z 米。黑屏
    //   Controller  跟手柄：reference（右手柄）+ offset（按头的水平朝向算），朝向头。Agent 聊天栏
    //   Node        贴着 nodeSource 最近查看的节点右侧，平面在这个节点正面前方 planeGap 米、朝向固定（不转）。详情弹窗
    //
    // content：Canvas 里锚在屏幕边上的那块内容（比如左上角的聊天栏）。VR 下把它的锚点收到自己的 pivot 上、位置归零，
    //          Canvas 的 pivot 也换成它的 pivot，于是"摆放点" = 这块内容的 pivot。为空时整块 Canvas 按 sizePx 当一块屏幕摆。
    // alwaysOnTop：UI 材质换成 ZTest Always 的一份，不被植株挡住（字幕在植株后方 1.5 m 也能看到）。之后新加的子物体
    //              （聊天气泡）在 hierarchyCount 变化时补上。
    [DisallowMultipleComponent]
    public class XRWorldCanvas : MonoBehaviour
    {
        public enum Placement { LazyFollow, Fixed, HeadLocked, Controller, Node }

        public Canvas canvas;
        public Placement placement = Placement.LazyFollow;
        [Tooltip("玩家头部（主相机）")]
        public Transform head;
        [Tooltip("Fixed / Node：参考点（ViewAnchor）；Controller：手柄（Right Aim）")]
        public Transform reference;
        [Tooltip("摆放偏移（米）。LazyFollow / HeadLocked：相对头；Fixed：相对 reference；Controller：相对手柄，按头的水平朝向算")]
        public Vector3 offset = new Vector3(0f, -0.2f, 1.5f);
        [Tooltip("1 个 Canvas 像素对应多少米")]
        public float metersPerPixel = 0.001f;
        [Tooltip("content 为空时 Canvas 的像素尺寸（整块当屏幕摆）")]
        public Vector2 sizePx = new Vector2(1920f, 1080f);
        public RectTransform content;
        [Tooltip("VR 下 content 改用的像素尺寸（0 = 不改），例如字幕在 VR 里换一个更窄的折行宽度")]
        public Vector2 contentSizePx;
        [Tooltip("VR 下 content 改用的 pivot（负数 = 不改），决定内容从摆放点往哪边展开")]
        public Vector2 contentPivot = new Vector2(-1f, -1f);
        public bool alwaysOnTop = true;
        [Tooltip("World Space 下动态字体的栅格倍率，越大字越清楚")]
        public float dynamicPixelsPerUnit = 3f;
        [Tooltip("Controller：跟随平滑时间（秒）")]
        public float followSmoothTime = 0.08f;
        [Tooltip("Node：提供节点位置的 VR 发起方")]
        public XRControllerInput nodeSource;
        [Tooltip("Node：面板离节点边缘的间隙（米，在面板平面上往右）")]
        public float nodeGap = 0.015f;
        [Tooltip("Node：面板平面在当前选中节点正面前方多远（米）")]
        public float planeGap = 0.1f;
        [Tooltip("Node：面板平面离 reference 至少这么远（米），避免贴到脸上")]
        public float minPlaneDepth = 0.25f;
        [Tooltip("LazyFollow 模式用的组件（构建时加好，PC 上关着）")]
        public LazyFollow lazyFollow;
        [Tooltip("调试：不连头显也按 VR 摆放")]
        public bool forceWorld;

        [Tooltip("切换后调用：true = VR（World Space），false = PC")]
        public UnityEvent<bool> worldModeChanged = new UnityEvent<bool>();

        public bool IsWorld { get; private set; }

        static Material onTopMaterial;
        readonly List<Graphic> graphics = new List<Graphic>();
        int sweptHierarchyCount = -1;
        Vector3 followVelocity;

        // PC 状态（切回时还原）
        RenderMode savedMode;
        Camera savedCamera;
        float savedPlaneDistance;
        Transform savedParent;
        Vector2 savedSize, savedPivot;
        Vector3 savedScale, savedLocalPosition;
        Quaternion savedLocalRotation;
        float savedDynamicPixels;
        Vector2 savedContentMin, savedContentMax, savedContentPosition, savedContentSize, savedContentPivot;

        CanvasScaler Scaler => canvas != null ? canvas.GetComponent<CanvasScaler>() : null;

        void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (lazyFollow != null) lazyFollow.enabled = false;
        }

        // 平时停用的界面（结局视频）打开的这一帧就切好，不先闪一帧 Overlay
        void OnEnable() => Update();

        void Update()
        {
            if (canvas == null) return;
            bool world = forceWorld || XRSettings.isDeviceActive;
            if (world != IsWorld) SetWorld(world);
        }

        void LateUpdate()
        {
            if (!IsWorld) return;
            if (alwaysOnTop && transform.hierarchyCount != sweptHierarchyCount) ApplyOnTop(true);

            switch (placement)
            {
                case Placement.Fixed:
                    PlaceFixed();
                    break;
                case Placement.Controller:
                    PlaceAtController(snap: false);
                    break;
                case Placement.Node:
                    PlaceAtNode();
                    break;
            }
        }

        void SetWorld(bool world)
        {
            IsWorld = world;
            if (world) EnterWorld();
            else ExitWorld();
            worldModeChanged?.Invoke(world);
        }

        void EnterWorld()
        {
            var rect = (RectTransform)canvas.transform;
            savedMode = canvas.renderMode;
            savedCamera = canvas.worldCamera;
            savedPlaneDistance = canvas.planeDistance;
            savedParent = transform.parent;
            savedSize = rect.sizeDelta;
            savedPivot = rect.pivot;
            savedScale = transform.localScale;
            savedLocalPosition = transform.localPosition;
            savedLocalRotation = transform.localRotation;
            var scaler = Scaler;
            if (scaler != null) savedDynamicPixels = scaler.dynamicPixelsPerUnit;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head != null ? head.GetComponent<Camera>() : null;
            if (scaler != null) scaler.dynamicPixelsPerUnit = dynamicPixelsPerUnit;

            if (content != null)
            {
                savedContentMin = content.anchorMin;
                savedContentMax = content.anchorMax;
                savedContentPosition = content.anchoredPosition;
                savedContentSize = content.sizeDelta;
                savedContentPivot = content.pivot;
                if (contentPivot.x >= 0f && contentPivot.y >= 0f) content.pivot = contentPivot;
                content.anchorMin = content.anchorMax = content.pivot;
                content.anchoredPosition = Vector2.zero;
                // 原来横向拉伸的内容（anchorMin.x != anchorMax.x）这里已改成固定锚点，sizeDelta 就是实际尺寸
                if (contentSizePx.x > 0f) content.sizeDelta = new Vector2(contentSizePx.x, content.sizeDelta.y);
                if (contentSizePx.y > 0f) content.sizeDelta = new Vector2(content.sizeDelta.x, contentSizePx.y);
                rect.pivot = content.pivot;
            }
            else rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizePx;
            transform.localScale = Vector3.one * metersPerPixel;

            switch (placement)
            {
                case Placement.LazyFollow:
                    // 先放到目标位置，LazyFollow 在 OnEnable 里也会对齐一次
                    PlaceInFrontOfHead();
                    if (lazyFollow != null)
                    {
                        lazyFollow.target = head;
                        lazyFollow.targetOffset = offset;
                        lazyFollow.enabled = true;
                    }
                    break;
                case Placement.HeadLocked:
                    if (head != null)
                    {
                        transform.SetParent(head, false);
                        transform.localPosition = new Vector3(0f, 0f, offset.z);
                        transform.localRotation = Quaternion.identity;
                    }
                    break;
                case Placement.Fixed:
                    PlaceFixed();
                    break;
                case Placement.Controller:
                    PlaceAtController(snap: true);
                    break;
                case Placement.Node:
                    PlaceAtNode();
                    break;
            }

            if (alwaysOnTop) ApplyOnTop(true);
        }

        void ExitWorld()
        {
            if (lazyFollow != null) lazyFollow.enabled = false;
            if (alwaysOnTop) ApplyOnTop(false);

            var rect = (RectTransform)canvas.transform;
            if (transform.parent != savedParent) transform.SetParent(savedParent, false);
            transform.localPosition = savedLocalPosition;
            transform.localRotation = savedLocalRotation;
            transform.localScale = savedScale;
            rect.pivot = savedPivot;
            rect.sizeDelta = savedSize;
            if (content != null)
            {
                content.anchorMin = savedContentMin;
                content.anchorMax = savedContentMax;
                content.anchoredPosition = savedContentPosition;
                content.sizeDelta = savedContentSize;
                content.pivot = savedContentPivot;
            }
            var scaler = Scaler;
            if (scaler != null) scaler.dynamicPixelsPerUnit = savedDynamicPixels;
            canvas.renderMode = savedMode;
            canvas.worldCamera = savedCamera;
            canvas.planeDistance = savedPlaneDistance;
        }

        // ---- 摆放 ----

        void PlaceInFrontOfHead()
        {
            if (head == null) return;
            Vector3 position = head.position + head.TransformVector(offset);
            transform.SetPositionAndRotation(position, Facing(position, head.position));
        }

        void PlaceFixed()
        {
            Transform r = reference != null ? reference : head;
            if (r == null) return;
            Vector3 position = r.TransformPoint(offset);
            transform.SetPositionAndRotation(position, Facing(position, r.position));
        }

        void PlaceAtController(bool snap)
        {
            if (reference == null) return;
            Vector3 forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            Vector3 target = reference.position + Quaternion.LookRotation(forward.normalized, Vector3.up) * offset;
            if (snap || followSmoothTime <= 0f)
            {
                transform.position = target;
                followVelocity = Vector3.zero;
            }
            else transform.position = Vector3.SmoothDamp(transform.position, target, ref followVelocity, followSmoothTime);
            Vector3 eye = head != null ? head.position : reference.position - forward;
            transform.rotation = Facing(transform.position, eye);
        }

        // 面板在一个平面上：和 reference（ViewAnchor）的朝向平行，在当前查看的节点（含选中突起）正面前方 planeGap 米，每帧跟着节点。
        // 位置 = 从眼睛看向节点的视线和这个平面的交点，再往右让出节点的投影半径：看上去紧挨着节点，朝向固定不转。
        // 节点不可用时停在原处
        void PlaceAtNode()
        {
            if (nodeSource == null || head == null || reference == null) return;
            if (!nodeSource.TryGetNodeSphere(nodeSource.LastInspectedNode, out Vector3 center, out float radius)) return;
            Vector3 normal = reference.forward;
            float nodeDepth = Vector3.Dot(center - reference.position, normal) - radius;
            float depth = Mathf.Max(minPlaneDepth, nodeDepth - planeGap);
            Vector3 planePoint = reference.position + normal * depth;
            Vector3 eye = head.position;
            Vector3 dir = center - eye;
            float denom = Vector3.Dot(dir, normal);
            if (denom < 1e-4f) return; // 节点在眼睛后面或和平面平行
            float t = Vector3.Dot(planePoint - eye, normal) / denom;
            Vector3 hit = eye + dir * t;
            // 节点半径投到平面上：节点比平面远时变小、近时变大，面板始终贴着节点看得到的边缘
            Vector3 position = hit + reference.right * (radius * t + nodeGap);
            transform.SetPositionAndRotation(position, reference.rotation);
        }

        // World Space Canvas 的正面朝 -forward：forward = 从眼睛指向面板，保持竖直
        static Quaternion Facing(Vector3 position, Vector3 eye)
        {
            Vector3 forward = position - eye;
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        // ---- 不被场景遮挡 ----

        static Material OnTopMaterial
        {
            get
            {
                if (onTopMaterial == null)
                {
                    onTopMaterial = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "UI Always On Top (XR)" };
                    onTopMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                }
                return onTopMaterial;
            }
        }

        void ApplyOnTop(bool on)
        {
            canvas.GetComponentsInChildren(true, graphics);
            var mat = OnTopMaterial;
            foreach (var g in graphics)
            {
                if (on && g.material == g.defaultMaterial) g.material = mat;
                else if (!on && g.material == mat) g.material = null;
            }
            graphics.Clear();
            sweptHierarchyCount = transform.hierarchyCount;
        }
    }
}
