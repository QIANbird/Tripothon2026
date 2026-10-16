using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ghost.Interaction.XR
{
    // 把 World Space Canvas 上的一块 UI 接到手柄射线上（Collider 桥接，docs/tasks/vr-migration.md 第 4 步）：
    // VR 模式下按 RectTransform 大小开一个 BoxCollider，XRControllerInput 的物理射线就能打到它。
    //   button 不为空：扳机短按 → OnTap → button.onClick（和 PC 上鼠标点同一个 Button）
    //   approveKey 勾上：扳机不点它，改成按 A 确认（开始界面 Awake、询问框 Yes），见 TryApprove
    //   button 为空：只挡射线（开始 / 结束界面的黑底、结局视频），后面的节点选不中
    // PC 上、Canvas 不在 World Space、或 CanvasGroup 不挡射线 / 按钮不可点时，Collider 关掉。
    [RequireComponent(typeof(RectTransform))]
    public class XRUIRayTarget : MonoBehaviour, IInteractable
    {
        public Button button;
        [Tooltip("所在 Canvas 的 XRWorldCanvas（为空时在父物体里找）")]
        public XRWorldCanvas world;
        [Tooltip("Collider 的厚度（Canvas 像素）。按钮比它背后的底板厚，射线先打到按钮")]
        public float thickness = 10f;
        [Tooltip("勾上后扳机不点这个按钮，改由 A 键确认（XRControllerInput 按 A 时先调 TryApprove）")]
        public bool approveKey;

        // 所有 approveKey 按钮（启用中的）
        static readonly List<XRUIRayTarget> approveTargets = new List<XRUIRayTarget>();

        BoxCollider box;
        RectTransform rect;
        bool hovered;

        void Awake()
        {
            rect = (RectTransform)transform;
            if (world == null) world = GetComponentInParent<XRWorldCanvas>(true);
            box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled = false;
        }

        void OnEnable()
        {
            if (approveKey) approveTargets.Add(this);
        }

        void OnDisable()
        {
            approveTargets.Remove(this);
            if (box != null) box.enabled = false;
            SetHover(false);
        }

        void LateUpdate()
        {
            bool active = world != null && world.IsWorld && Blocks();
            if (active)
            {
                Rect r = rect.rect;
                box.center = new Vector3(r.center.x, r.center.y, 0f);
                box.size = new Vector3(r.width, r.height, thickness);
            }
            if (box.enabled != active) box.enabled = active;
            if (!active) SetHover(false);
        }

        bool Blocks()
        {
            // CanvasGroup 链上任何一层不挡射线（淡出中、隐藏的界面）都不挡
            var groups = GetComponentsInParent<CanvasGroup>();
            foreach (var g in groups)
            {
                if (!g.blocksRaycasts || g.alpha < 0.01f) return false;
                if (g.ignoreParentGroups) break;
            }
            return button == null || button.IsInteractable();
        }

        bool Clickable => button != null && box != null && box.enabled && button.IsInteractable();

        public void OnTap()
        {
            if (approveKey) return; // 只能按 A 确认
            if (Clickable) button.onClick.Invoke();
        }

        // A 键：当前显示着的 approveKey 按钮（Collider 开着 = World Space 且界面可点）点一下。点到了返回 true，A 不再做别的
        public static bool TryApprove()
        {
            for (int i = approveTargets.Count - 1; i >= 0; i--)
            {
                var t = approveTargets[i];
                if (t == null || !t.Clickable) continue;
                t.button.onClick.Invoke();
                return true;
            }
            return false;
        }

        public void OnHoverEnter() => SetHover(true);
        public void OnHoverExit() => SetHover(false);

        // 悬停：给按钮发 UGUI 的 PointerEnter / Exit，Button 自己的高亮色和按钮上的 EventTrigger（询问框的悬停变色）都会生效
        void SetHover(bool on)
        {
            if (on == hovered || button == null) return;
            hovered = on;
            var es = EventSystem.current;
            if (es == null) return;
            var data = new PointerEventData(es);
            if (on) ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
            else ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
        }
    }
}
