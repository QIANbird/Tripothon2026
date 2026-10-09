using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Player
{
    // 第一人称移动（Pick 阶段）：CharacterController 走路 + 转头 + 蹲下。只读 Player 动作表（Move / Look / Crouch），不读设备。
    // 没用 Starter Assets：它依赖 Cinemachine 和自己的输入封装，和本项目"只读 Action"的结构不合。
    // 结构（docs/VR_GUIDELINES.md 第 3 节）：本物体 = 身体（偏航 + 移动），cameraPivot = 头（俯仰 + 眼高）。VR 迁移时整体换成 XR Origin。
    // 舒适度：步行速度 ~2 m/s、加减速平滑、无 head bob；蹲下时眼高平滑过渡。
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonMotor : MonoBehaviour
    {
        [Tooltip("头：俯仰和眼高作用在它上面，相机是它的子物体")]
        public Transform cameraPivot;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Player";

        [Header("移动")]
        [Tooltip("步行速度（米/秒），VR 舒适范围 1.5–3")]
        public float walkSpeed = 2f;
        [Tooltip("蹲下时的速度（米/秒）")]
        public float crouchSpeed = 1f;
        [Tooltip("加速 / 减速的时间常数（秒）")]
        public float speedSmoothTime = 0.12f;
        public float gravity = -9.81f;

        [Header("视角")]
        [Tooltip("每单位 Look 输入（鼠标为像素）转多少度")]
        public float lookSensitivity = 0.1f;
        [Tooltip("俯仰下限（度，负数 = 低头）")]
        public float minPitch = -75f;
        [Tooltip("俯仰上限（度）")]
        public float maxPitch = 70f;

        [Header("身高")]
        [Tooltip("站立眼高（米）")]
        public float standEyeHeight = 1.6f;
        [Tooltip("蹲下眼高（米）")]
        public float crouchEyeHeight = 1.0f;
        [Tooltip("眼高过渡的时间常数（秒）")]
        public float crouchSmoothTime = 0.15f;
        [Tooltip("头顶到眼睛的距离（米），决定胶囊体高度")]
        public float headClearance = 0.12f;
        [Tooltip("蹲下是按住（true）还是切换（false）")]
        public bool holdToCrouch = false;

        public bool IsCrouching { get; private set; }
        public float Pitch => pitch;
        public float EyeHeight => eyeHeight;

        CharacterController controller;
        InputActionMap map;
        InputAction moveAction, lookAction, crouchAction;
        float pitch;
        float eyeHeight, eyeVel;
        Vector3 planarVelocity, planarAccel;
        float verticalSpeed;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            // PlaceAt 可能在物体第一次启用（Awake）之前就调用过，不要覆盖
            if (eyeHeight <= 0f) eyeHeight = standEyeHeight;
        }

        void OnEnable()
        {
            if (actions == null || cameraPivot == null)
            {
                Debug.LogError("[Player] FirstPersonMotor 缺少 actions 或 cameraPivot", this);
                enabled = false;
                return;
            }
            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
            lookAction = map.FindAction("Look", throwIfNotFound: true);
            crouchAction = map.FindAction("Crouch", throwIfNotFound: false);
            map.Enable();
        }

        void OnDisable()
        {
            // 其他脚本可能共用同一个资产，这里只关掉 Player 表
            if (map != null) map.Disable();
            map = null;
            planarVelocity = planarAccel = Vector3.zero;
            verticalSpeed = 0f;
        }

        // 把身体和头对齐到某个世界姿态（眼睛位置 + 朝向）。进入阶段时用来接上固定相机，画面不跳。
        // 俯仰按 min/max 截断，滚转丢弃
        public void PlaceAt(Vector3 eyePosition, Quaternion viewRotation, bool crouched = false)
        {
            IsCrouching = crouched;
            eyeHeight = crouched ? crouchEyeHeight : standEyeHeight;
            eyeVel = 0f;
            Vector3 euler = viewRotation.eulerAngles;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, -euler.x), minPitch, maxPitch);
            planarVelocity = planarAccel = Vector3.zero;
            verticalSpeed = 0f;

            // CharacterController 会覆盖直接设置的位置，先关掉再挪
            if (controller == null) controller = GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(eyePosition - Vector3.up * (eyeHeight - controller.skinWidth), Quaternion.Euler(0f, euler.y, 0f));
            controller.enabled = wasEnabled;
            ApplyHeight();
            ApplyPitch();
        }

        void Update()
        {
            if (map == null) return;
            float dt = Time.deltaTime;

            // 转头：偏航转身体，俯仰转头
            Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;
            transform.Rotate(0f, look.x, 0f, Space.World);
            pitch = Mathf.Clamp(pitch + look.y, minPitch, maxPitch);
            ApplyPitch();

            // 蹲下
            if (crouchAction != null)
            {
                if (holdToCrouch) IsCrouching = crouchAction.IsPressed();
                else if (crouchAction.WasPressedThisFrame()) IsCrouching = !IsCrouching;
            }
            float targetEye = IsCrouching ? crouchEyeHeight : standEyeHeight;
            // 站起来时头顶有东西就保持蹲着
            if (!IsCrouching && eyeHeight < standEyeHeight - 0.01f && HeadBlocked(standEyeHeight)) targetEye = eyeHeight;
            eyeHeight = Mathf.SmoothDamp(eyeHeight, targetEye, ref eyeVel, crouchSmoothTime);
            ApplyHeight();

            // 走路：相对身体朝向，平滑加减速
            Vector2 move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            float speed = IsCrouching ? crouchSpeed : walkSpeed;
            Vector3 wish = (transform.right * move.x + transform.forward * move.y) * speed;
            planarVelocity = Vector3.SmoothDamp(planarVelocity, wish, ref planarAccel, speedSmoothTime);

            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed += gravity * dt;
            controller.Move((planarVelocity + Vector3.up * verticalSpeed) * dt);
        }

        void ApplyPitch()
        {
            cameraPivot.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        }

        // 胶囊体高度跟眼高走，底部贴地
        void ApplyHeight()
        {
            float height = Mathf.Max(controller.radius * 2f, eyeHeight + headClearance);
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
            // CharacterController 停在离地 skinWidth 的高度，眼高从地面算，减掉这段，进关时画面不跳
            cameraPivot.localPosition = Vector3.up * (eyeHeight - controller.skinWidth);
        }

        bool HeadBlocked(float toEyeHeight)
        {
            float r = controller.radius * 0.95f;
            Vector3 from = transform.position + Vector3.up * (eyeHeight + headClearance - r);
            float distance = toEyeHeight - eyeHeight;
            return Physics.SphereCast(from, r, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
