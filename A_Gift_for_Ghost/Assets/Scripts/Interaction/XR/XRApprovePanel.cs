using UnityEngine;

namespace Ghost.Interaction.XR
{
    // 选中节点后手柄旁的"是否批准"面板（World Space Canvas，由 MainSceneMenu 生成内容）。
    // 只负责显示和跟随：位置 = 沿手柄射线前方 offset.z 米，再往世界竖直方向偏 offset.y（默认在射线下方）、往射线的水平右侧偏 offset.x，
    // 朝向玩家头部，手柄怎么转面板都不翻。不读输入，由 XRControllerInput 控制显示。
    public class XRApprovePanel : MonoBehaviour
    {
        [Tooltip("面板根物体（World Space Canvas）")]
        public GameObject root;
        [Tooltip("相对手柄的偏移（米）：z = 沿射线往前，y = 世界竖直（负 = 射线下方），x = 射线的水平右侧")]
        public Vector3 offset = new Vector3(0.08f, 0.11f, 0.05f);
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
                Vector3 toPanel = transform.position - head.position;
                if (toPanel.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(toPanel, Vector3.up);
            }
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        Vector3 TargetPosition(Transform controller, Transform head)
        {
            Vector3 forward = controller.forward;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 1e-6f)
                right = head != null ? Vector3.ProjectOnPlane(head.right, Vector3.up) : Vector3.right;
            right.Normalize();
            return controller.position
                   + right * offset.x
                   + up * offset.y
                   + forward * offset.z;
        }
    }
}
