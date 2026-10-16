using UnityEngine;
using UnityEngine.XR;

namespace Ghost.Player.XR
{
    // VR 构图视点校准：把 ViewAnchor（PlantFit 的名义眼位）挪到头显实际的眼高，并略微低头，
    // 让 S1–S4 的植株落在视线正前方略偏下，而不是按 1.6 m 的名义眼高摆在玩家头顶上方（坐着玩时尤其明显）。
    // 只改 ViewAnchor 的高度和俯仰，不改水平位置和朝向；PlantFit 发现视点变了会平滑重新适配。
    // 头显没在运行（PC）时 ViewAnchor 保持构建时的姿态（1.6 m 水平正视），PC 画面不变。
    //
    // 什么时候重新校准：
    //   - 头显开始跟踪后等 settleSeconds 秒（刚戴上时高度不稳），校准一次；
    //   - 之后实际眼高和锚点相差超过 recalibrateDelta 并持续 recalibrateSeconds 秒（站起 / 坐下）再校准。
    //   转头、前后探头不会触发，植株不追着头跑。
    public class XRViewCalibration : MonoBehaviour
    {
        [Tooltip("XR Origin/ViewAnchor")]
        public Transform viewAnchor;
        [Tooltip("主相机（Tracked Pose Driver 跟头显）")]
        public Transform head;

        [Header("构图")]
        [Tooltip("视点低头的角度（度）。植株中心落在视线下方这么多度，看起来在正前方、不用抬头")]
        [Range(0f, 30f)] public float pitchDown = 10f;
        [Tooltip("在实际眼高上再加减的高度（米），负数 = 植株再低一点")]
        public float heightOffset = 0f;

        [Header("校准时机")]
        public float settleSeconds = 0.5f;
        public float recalibrateDelta = 0.2f;
        public float recalibrateSeconds = 2f;

        Vector3 pcLocalPosition;
        Quaternion pcLocalRotation;
        bool captured;
        bool calibrated;
        float trackedTime;
        float driftTime;

        void Awake()
        {
            Capture();
        }

        void Capture()
        {
            if (captured || viewAnchor == null) return;
            pcLocalPosition = viewAnchor.localPosition;
            pcLocalRotation = viewAnchor.localRotation;
            captured = true;
        }

        void LateUpdate()
        {
            if (viewAnchor == null || head == null) return;
            Capture();

            // 头显没在跑或者还没有姿态（相机本地位置还是 0）：用 PC 机位
            bool tracking = XRSettings.isDeviceActive && head.localPosition.sqrMagnitude > 1e-6f;
            if (!tracking)
            {
                trackedTime = 0f;
                driftTime = 0f;
                if (calibrated)
                {
                    calibrated = false;
                    viewAnchor.localPosition = pcLocalPosition;
                    viewAnchor.localRotation = pcLocalRotation;
                }
                return;
            }

            trackedTime += Time.unscaledDeltaTime;
            if (trackedTime < settleSeconds) return;

            float target = TargetHeight();
            if (!calibrated)
            {
                Calibrate(target);
                return;
            }

            if (Mathf.Abs(target - viewAnchor.position.y) > recalibrateDelta)
            {
                driftTime += Time.unscaledDeltaTime;
                if (driftTime >= recalibrateSeconds) Calibrate(target);
            }
            else driftTime = 0f;
        }

        // 手动重新校准（例如调完 pitchDown / heightOffset 后在 Inspector 的组件菜单里点）
        [ContextMenu("Recalibrate Now")]
        public void RecalibrateNow()
        {
            if (head != null && viewAnchor != null && XRSettings.isDeviceActive) Calibrate(TargetHeight());
        }

        float TargetHeight() => head.position.y + heightOffset;

        void Calibrate(float worldHeight)
        {
            calibrated = true;
            driftTime = 0f;
            // 水平位置和朝向保持构建时的（原机位、朝 +Z），只换高度和俯仰
            Transform parent = viewAnchor.parent;
            Vector3 world = parent != null ? parent.TransformPoint(pcLocalPosition) : pcLocalPosition;
            world.y = worldHeight;
            viewAnchor.position = world;
            viewAnchor.localRotation = pcLocalRotation * Quaternion.Euler(pitchDown, 0f, 0f);
        }

        void OnValidate()
        {
            if (Application.isPlaying && calibrated) RecalibrateNow();
        }
    }
}
