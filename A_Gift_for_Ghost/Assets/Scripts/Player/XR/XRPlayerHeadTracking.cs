using UnityEngine;
using UnityEngine.XR;

namespace Ghost.Player.XR
{
    // Pick 阶段的 VR 眼高：头显在运行时，相机的本地位置就是头显到地面的实际高度（Floor 模式），
    // FirstPersonMotor 不能再把相机抬到 1.6 m 的眼高上（否则两者叠加，玩家像飘在 3 m 高空）。
    // 这里每帧告诉 motor：头是否由头显跟踪、实际眼高多少。motor 据此把相机挂点放在脚下、不再做鼠标俯仰。
    // 挂在一直启用的物体上（XR Origin），PlayerRoot 平时是停用的。PC 上 headTracked = false，行为不变。
    public class XRPlayerHeadTracking : MonoBehaviour
    {
        public FirstPersonMotor motor;
        [Tooltip("主相机（Tracked Pose Driver 跟头显）")]
        public Transform head;
        [Tooltip("XR Origin/Camera Offset。Floor 模式下本地高度为 0，Device 模式下是 CameraYOffset；头显高度要加上它才是离地眼高")]
        public Transform cameraOffset;

        void Update()
        {
            if (motor == null || head == null) return;
            bool tracked = XRSettings.isDeviceActive && head.localPosition.sqrMagnitude > 1e-6f;
            motor.headTracked = tracked;
            if (!tracked) return;
            float floorOffset = cameraOffset != null ? cameraOffset.localPosition.y : 0f;
            motor.trackedFloorOffset = floorOffset;
            motor.trackedEyeHeight = Mathf.Max(0.3f, floorOffset + head.localPosition.y);
            motor.trackedHeadLocalPosition = head.localPosition;
            motor.trackedHeadLocalRotation = head.localRotation;
        }
    }
}
