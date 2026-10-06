using System.Text;
using UnityEngine;

namespace Ghost.Interaction
{
    // 调试用：把 PointerInput 的事件打印到 Console（点击的节点 id + 部位、拖拽经过的节点序列、空白拖拽的位移）。
    // 正式流程里取消勾选 logEvents 或者禁用这个组件即可（G6 起建议关掉）。
    public class PointerDebugLogger : MonoBehaviour
    {
        public PointerInput pointer;
        public NodePicker picker;
        public bool logEvents = true;
        [Tooltip("空白拖拽时每帧打印位移（量大，默认只在结束时打印总量）")]
        public bool logEveryEmptyDelta = false;
        public bool logHover = false;

        readonly StringBuilder dragPath = new StringBuilder();
        Vector2 emptyTotal;
        int emptyFrames;

        void OnEnable()
        {
            if (pointer == null) { enabled = false; return; }
            pointer.Tap += OnTap;
            pointer.DragStart += OnDragStart;
            pointer.DragOver += OnDragOver;
            pointer.DragEnd += OnDragEnd;
            pointer.DragEmpty += OnDragEmpty;
            pointer.DragEmptyEnd += OnDragEmptyEnd;
            pointer.HoverChanged += OnHover;
            pointer.InteractableTapped += OnInteractable;
        }

        void OnDisable()
        {
            if (pointer == null) return;
            pointer.Tap -= OnTap;
            pointer.DragStart -= OnDragStart;
            pointer.DragOver -= OnDragOver;
            pointer.DragEnd -= OnDragEnd;
            pointer.DragEmpty -= OnDragEmpty;
            pointer.DragEmptyEnd -= OnDragEmptyEnd;
            pointer.HoverChanged -= OnHover;
            pointer.InteractableTapped -= OnInteractable;
        }

        string Describe(int id)
        {
            var organ = picker != null ? picker.OrganOf(id) : null;
            return organ.HasValue ? $"{id}({organ.Value})" : id.ToString();
        }

        void OnTap(int id)
        {
            if (logEvents) Debug.Log($"[Pointer] Tap node {Describe(id)}");
        }

        void OnDragStart(int id)
        {
            dragPath.Clear().Append(Describe(id));
            if (logEvents) Debug.Log($"[Pointer] DragStart node {Describe(id)}");
        }

        void OnDragOver(int id)
        {
            dragPath.Append(" > ").Append(Describe(id));
            if (logEvents) Debug.Log($"[Pointer] DragOver node {Describe(id)}");
        }

        void OnDragEnd()
        {
            if (logEvents) Debug.Log($"[Pointer] DragEnd path: {dragPath}");
        }

        void OnDragEmpty(Vector2 delta)
        {
            emptyTotal += delta;
            emptyFrames++;
            if (logEvents && logEveryEmptyDelta) Debug.Log($"[Pointer] DragEmpty delta {delta}");
        }

        void OnDragEmptyEnd()
        {
            if (logEvents) Debug.Log($"[Pointer] DragEmptyEnd total {emptyTotal} over {emptyFrames} frames");
            emptyTotal = Vector2.zero;
            emptyFrames = 0;
        }

        void OnHover(int id)
        {
            if (logEvents && logHover) Debug.Log($"[Pointer] Hover {(id >= 0 ? Describe(id) : "none")}");
        }

        void OnInteractable(IInteractable target)
        {
            if (logEvents) Debug.Log($"[Pointer] Tap interactable {(target as Component)?.name ?? target.ToString()}");
        }
    }
}
