using UnityEngine;

namespace Ghost.Player
{
    // 玩家整体的开关（Pick 阶段用）。根物体平时停用；Activate 时把身体对齐到当前相机姿态，再把相机挂到头上，画面不跳。
    // Deactivate 时相机回到原来的父物体和本地姿态（固定机位），玩家根物体停用。
    // 不读输入、不读 Camera.main：相机由场景构建时填进来。PC 专用的东西（光标锁定、准星）挂在根物体下，跟着一起开关。
    public class PlayerRig : MonoBehaviour
    {
        public FirstPersonMotor motor;
        [Tooltip("场景里的主相机。Activate 时挂到 motor.cameraPivot 下")]
        public Transform viewCamera;

        public bool IsActive { get; private set; }

        Transform savedParent;
        Vector3 savedLocalPosition;
        Quaternion savedLocalRotation;

        // 平时停用：根物体就是本物体
        void Awake()
        {
            if (!IsActive) gameObject.SetActive(false);
        }

        public void Activate()
        {
            if (IsActive) return;
            if (motor == null || viewCamera == null || motor.cameraPivot == null)
            {
                Debug.LogError("[Player] PlayerRig 缺少 motor / viewCamera / cameraPivot", this);
                return;
            }
            IsActive = true;
            savedParent = viewCamera.parent;
            savedLocalPosition = viewCamera.localPosition;
            savedLocalRotation = viewCamera.localRotation;

            motor.PlaceAt(viewCamera.position, viewCamera.rotation);
            // 俯仰被截断时，相机朝向以头为准（固定机位是水平正视，不会被截断）
            viewCamera.SetParent(motor.cameraPivot, false);
            viewCamera.localPosition = Vector3.zero;
            viewCamera.localRotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            if (viewCamera != null)
            {
                viewCamera.SetParent(savedParent, false);
                viewCamera.localPosition = savedLocalPosition;
                viewCamera.localRotation = savedLocalRotation;
            }
            gameObject.SetActive(false);
        }
    }
}
