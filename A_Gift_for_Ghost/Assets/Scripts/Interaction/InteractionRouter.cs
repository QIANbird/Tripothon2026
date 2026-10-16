using UnityEngine;
using UnityEngine.XR;

namespace Ghost.Interaction
{
    // 场景里玩法层引用的交互发起方（StageContext.pointer 等）。自己不读输入：
    // 按平台在 PC 发起方（PointerInput）和 VR 发起方（XRControllerInput）里选一个"当前发起方"，转发它的事件和状态。
    //   - 头显在运行（XRSettings.isDeviceActive）→ VR；否则 → PC。每帧检查，Link 中途连上也能切过去。
    //   - 不在用的那个发起方 InputEnabled = false，不发事件、不显示射线。
    //   - 阶段写在路由器上的 allowNodeDrag / allowEmptyDrag / InputEnabled 同步到两个发起方。
    // 执行顺序 -100：先于两个发起方（和 TargetRotator -50）同步开关。
    [DefaultExecutionOrder(-100)]
    public class InteractionRouter : InteractionSource
    {
        public enum Mode { Auto, Pc, XR }

        [Tooltip("PC 发起方（PointerInput）")]
        public InteractionSource pc;
        [Tooltip("VR 发起方（XRControllerInput）")]
        public InteractionSource xr;
        [Tooltip("Auto：头显在运行时用 VR，否则用 PC。调试时可以强制指定")]
        public Mode mode = Mode.Auto;

        public InteractionSource Active { get; private set; }
        public bool IsXR => Active != null && Active == xr;

        public override Vector2 PointerPosition => Active != null ? Active.PointerPosition : Vector2.zero;
        public override Ray CurrentRay => Active != null ? Active.CurrentRay : default;
        public override int HoveredNode => Active != null ? Active.HoveredNode : -1;
        public override IInteractable HoveredInteractable => Active != null ? Active.HoveredInteractable : null;
        public override float HoverDistance => Active != null ? Active.HoverDistance : float.PositiveInfinity;
        public override bool IsPressed => Active != null && Active.IsPressed;
        public override bool IsDraggingNode => Active != null && Active.IsDraggingNode;
        public override bool IsDraggingEmpty => Active != null && Active.IsDraggingEmpty;
        public override int InspectingNode => Active != null ? Active.InspectingNode : -1;

        void OnEnable()
        {
            Subscribe(pc, true);
            Subscribe(xr, true);
            SelectActive();
        }

        void OnDisable()
        {
            Subscribe(pc, false);
            Subscribe(xr, false);
        }

        void Update()
        {
            SelectActive();
            Sync(pc);
            Sync(xr);
        }

        protected override void OnInputEnabledChanged(bool enabled)
        {
            Sync(pc);
            Sync(xr);
        }

        void SelectActive()
        {
            bool useXR = mode == Mode.XR || (mode == Mode.Auto && XRSettings.isDeviceActive);
            InteractionSource next = useXR && xr != null ? xr : pc;
            if (next == Active) return;
            Active = next;
            Sync(pc);
            Sync(xr);
        }

        void Sync(InteractionSource source)
        {
            if (source == null) return;
            source.allowNodeDrag = allowNodeDrag;
            source.allowEmptyDrag = allowEmptyDrag;
            source.InputEnabled = InputEnabled && source == Active;
        }

        void Subscribe(InteractionSource s, bool on)
        {
            if (s == null) return;
            if (on)
            {
                s.Tap += OnTap;
                s.DragStart += OnDragStart;
                s.DragOver += OnDragOver;
                s.DragEnd += OnDragEnd;
                s.DragEmpty += OnDragEmpty;
                s.DragEmptyEnd += OnDragEmptyEnd;
                s.HoverChanged += OnHoverChanged;
                s.InteractableTapped += OnInteractableTapped;
                s.TapEmpty += OnTapEmpty;
                s.InspectStart += OnInspectStart;
                s.InspectEnd += OnInspectEnd;
            }
            else
            {
                s.Tap -= OnTap;
                s.DragStart -= OnDragStart;
                s.DragOver -= OnDragOver;
                s.DragEnd -= OnDragEnd;
                s.DragEmpty -= OnDragEmpty;
                s.DragEmptyEnd -= OnDragEmptyEnd;
                s.HoverChanged -= OnHoverChanged;
                s.InteractableTapped -= OnInteractableTapped;
                s.TapEmpty -= OnTapEmpty;
                s.InspectStart -= OnInspectStart;
                s.InspectEnd -= OnInspectEnd;
            }
        }

        // 不在用的发起方被关掉时会补发结束事件（DragEnd / InspectEnd 等），这些照样转发，保证订阅方的状态能收尾
        void OnTap(int id) => RaiseTap(id);
        void OnDragStart(int id) => RaiseDragStart(id);
        void OnDragOver(int id) => RaiseDragOver(id);
        void OnDragEnd() => RaiseDragEnd();
        void OnDragEmpty(Vector2 d) => RaiseDragEmpty(d);
        void OnDragEmptyEnd() => RaiseDragEmptyEnd();
        void OnHoverChanged(int id) => RaiseHoverChanged(id);
        void OnInteractableTapped(IInteractable target) => RaiseInteractableTapped(target);
        void OnTapEmpty() => RaiseTapEmpty();
        void OnInspectStart(int id) => RaiseInspectStart(id);
        void OnInspectEnd() => RaiseInspectEnd();
    }
}
