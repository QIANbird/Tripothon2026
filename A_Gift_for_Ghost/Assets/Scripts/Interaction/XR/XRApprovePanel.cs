using UnityEngine;

namespace Ghost.Interaction.XR
{
    // 选中节点后手柄旁的"是否批准"面板（World Space Canvas，由 MainSceneMenu 生成内容）。
    // 只负责显示和跟随：位置 = 手柄 + 偏移（手柄的右上方），朝向玩家头部。不读输入，由 XRControllerInput 控制显示。
    public class XRApprovePanel : MonoBehaviour
    {
        [Tooltip("面板根物体（World Space Canvas）")]
        public GameObject root;
        [Tooltip("相对手柄的偏移（米）：x 右、y 上、z 前，按头部的水平朝向算，手柄怎么转面板都不翻")]
        public Vector3 offset = new Vector3(0.06f, 0.09f, 0.04f);
        [Tooltip("跟随平滑时间（秒）")]
        public float followSmoothTime = 0.06f;

        public bool Visible => root != null && root.activeSelf;

        Vector3 velocity;

        void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(Transform controller, Transform head)
        {
            if (root == null || controller == null) return;
            Vector3 target = TargetPosition(controller, head);
            if (!root.activeSelf)
            {
                root.SetActive(true);
                transform.position = target;
                velocity = Vector3.zero;
            }
            else transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, followSmoothTime);

            if (head != null)
            {
                Vector3 forward = transform.position - head.position;
                if (forward.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        Vector3 TargetPosition(Transform controller, Transform head)
        {
            Vector3 forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            Quaternion basis = Quaternion.LookRotation(forward.normalized, Vector3.up);
            return controller.position + basis * offset;
        }
    }
}
