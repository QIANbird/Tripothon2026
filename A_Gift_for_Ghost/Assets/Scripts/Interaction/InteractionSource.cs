using System;
using UnityEngine;

namespace Ghost.Interaction
{
    // 交互发起方的公共契约：PC 的 PointerInput（鼠标射线）和 VR 的 XRControllerInput（右手柄射线）都继承它，
    // 事件集合和签名完全一样。玩法层（Stage、StageContext、TargetRotator、DialogueAdvanceInput）只引用这个类型，
    // 场景里实际挂的是 InteractionRouter，它按平台选一个发起方并转发事件。
    //
    // 事件语义（各发起方按自己的手势映射过来）：
    //   Tap(id)            批准 / 点击一个节点
    //   DragStart/Over/End 从节点出发的拖拽连线（S2）
    //   DragEmpty(delta)   旋转用的原始增量（PC：空白处拖拽的像素；VR：摇杆换算成等效像素），轴过滤由 Stage / TargetRotator 决定
    //   TapEmpty           推进对白
    //   InspectStart/End   查看详情
    public abstract class InteractionSource : MonoBehaviour
    {
        [Tooltip("允许从节点出发拖拽（S2）。关掉时从节点出发的拖拽什么也不发")]
        public bool allowNodeDrag = true;
        [Tooltip("允许发出旋转增量（S3/S4 旋转物体）")]
        public bool allowEmptyDrag = true;

        // ---- 事件 ----
        public event Action<int> Tap;
        public event Action<int> DragStart;
        public event Action<int> DragOver;
        public event Action DragEnd;
        public event Action<Vector2> DragEmpty;
        public event Action DragEmptyEnd;
        // 射线下的节点变化（-1 = 没有节点）。只用于高亮
        public event Action<int> HoverChanged;
        public event Action<IInteractable> InteractableTapped;
        // 推进对白（PC：空白处点击；VR：没有选中节点时按 A）
        public event Action TapEmpty;
        // 查看详情开始 / 结束。InspectEnd 只在之前发过 InspectStart 时发出
        public event Action<int> InspectStart;
        public event Action InspectEnd;

        // ---- 状态 ----
        // 屏幕指针位置（像素）。VR 发起方没有屏幕指针，恒为 0
        public virtual Vector2 PointerPosition { get; protected set; }
        // 当前射线（世界空间）
        public virtual Ray CurrentRay { get; protected set; }
        public virtual int HoveredNode { get; protected set; } = -1;
        public virtual IInteractable HoveredInteractable { get; protected set; }
        // 射线最近命中点的距离（没有命中时为无穷大）
        public virtual float HoverDistance { get; protected set; } = float.PositiveInfinity;
        public virtual bool IsPressed { get; protected set; }
        public virtual bool IsDraggingNode => false;
        public virtual bool IsDraggingEmpty => false;
        // 正在查看详情的节点（-1 = 没有）
        public virtual int InspectingNode { get; protected set; } = -1;

        // 阶段用来整体开关输入。关闭时结束正在进行的手势（发出 DragEnd / DragEmptyEnd / InspectEnd）并清掉悬停
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                if (inputEnabled == value) return;
                inputEnabled = value;
                OnInputEnabledChanged(value);
            }
        }

        bool inputEnabled = true;

        protected virtual void OnInputEnabledChanged(bool enabled) { }

        // 事件只能在声明的类里触发，子类通过这些方法发出
        protected void RaiseTap(int id) => Tap?.Invoke(id);
        protected void RaiseDragStart(int id) => DragStart?.Invoke(id);
        protected void RaiseDragOver(int id) => DragOver?.Invoke(id);
        protected void RaiseDragEnd() => DragEnd?.Invoke();
        protected void RaiseDragEmpty(Vector2 delta) => DragEmpty?.Invoke(delta);
        protected void RaiseDragEmptyEnd() => DragEmptyEnd?.Invoke();
        protected void RaiseHoverChanged(int id) => HoverChanged?.Invoke(id);
        protected void RaiseInteractableTapped(IInteractable target) => InteractableTapped?.Invoke(target);
        protected void RaiseTapEmpty() => TapEmpty?.Invoke();
        protected void RaiseInspectStart(int id) => InspectStart?.Invoke(id);
        protected void RaiseInspectEnd() => InspectEnd?.Invoke();

        // 射线同时测节点和普通物体，返回较近的那个。物体更近时 nodeId = -1（被挡住）
        protected static void RaycastTargets(NodePicker picker, Ray ray, LayerMask physicsMask, float maxDistance,
            out int nodeId, out IInteractable interactable, out float distance)
        {
            nodeId = -1;
            interactable = null;
            distance = float.PositiveInfinity;

            float nodeDistance = float.PositiveInfinity;
            int node = picker != null ? picker.Pick(ray, out nodeDistance) : -1;
            if (node >= 0 && nodeDistance > maxDistance) node = -1;

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, physicsMask, QueryTriggerInteraction.Collide)
                && (node < 0 || hit.distance < nodeDistance))
            {
                interactable = hit.collider.GetComponentInParent<IInteractable>();
                distance = hit.distance;
                return;
            }
            if (node >= 0)
            {
                nodeId = node;
                distance = nodeDistance;
            }
        }

        // 被销毁的 MonoBehaviour 不再回调
        protected static bool IsAlive(IInteractable interactable)
        {
            if (interactable == null) return false;
            return !(interactable is UnityEngine.Object obj) || obj != null;
        }
    }
}
