using UnityEngine;
using UnityEngine.Events;

namespace Ghost.Interaction
{
    // 通用按钮：挂在带 Collider 的物体上（World Space Canvas 上的按钮要另加一个 BoxCollider 贴合按钮大小），
    // 被点击时触发 onTap。G4 的 Yes 按钮可以直接用它。
    public class InteractableButton : MonoBehaviour, IInteractable
    {
        public UnityEvent onTap = new UnityEvent();
        public UnityEvent onHoverEnter = new UnityEvent();
        public UnityEvent onHoverExit = new UnityEvent();

        [Tooltip("关掉后不响应点击（物体仍然挡射线）")]
        public bool interactable = true;

        public bool IsHovered { get; private set; }

        public void OnTap()
        {
            if (interactable && isActiveAndEnabled) onTap.Invoke();
        }

        public void OnHoverEnter()
        {
            IsHovered = true;
            if (interactable && isActiveAndEnabled) onHoverEnter.Invoke();
        }

        public void OnHoverExit()
        {
            IsHovered = false;
            onHoverExit.Invoke();
        }
    }
}
