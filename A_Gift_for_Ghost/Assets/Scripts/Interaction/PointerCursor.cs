using UnityEngine;

namespace Ghost.Interaction
{
    // 光标表现，和 PointerInput 分开：PC 版显示系统鼠标光标，可选再放一个世界空间小标记跟随指针命中点。
    // 以后 VR 版整个换成手柄射线的指示，不需要改 PointerInput 或被交互物。
    public class PointerCursor : MonoBehaviour
    {
        public PointerInput pointer;

        [Tooltip("显示系统鼠标光标，并且不锁定")]
        public bool showSystemCursor = true;

        [Tooltip("可选：跟随指针的世界空间标记（不要带 Collider，否则会挡住射线）。为空时只用系统光标")]
        public Transform marker;
        [Tooltip("没有命中任何东西时，标记放在相机前方这个距离")]
        public float idleDistance = 1.5f;
        [Tooltip("标记从命中点往相机方向退一点，避免被物体挡住")]
        public float surfaceOffset = 0.01f;
        [Tooltip("没有指向可交互目标时隐藏标记")]
        public bool hideMarkerWhenIdle = true;

        void OnEnable()
        {
            if (showSystemCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        void LateUpdate()
        {
            if (marker == null || pointer == null) return;

            bool active = pointer.isActiveAndEnabled && pointer.InputEnabled;
            bool onTarget = pointer.HoveredNode >= 0 || pointer.HoveredInteractable != null;
            bool show = active && (onTarget || !hideMarkerWhenIdle);
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;

            Ray ray = pointer.CurrentRay;
            float distance = onTarget && !float.IsInfinity(pointer.HoverDistance)
                ? Mathf.Max(0f, pointer.HoverDistance - surfaceOffset)
                : idleDistance;
            marker.position = ray.GetPoint(distance);
        }
    }
}
