using UnityEngine;

namespace Ghost.Interaction
{
    // 旋转眼前的目标物体（S3 的网络结构、S4 的盆栽），不动相机（AGENTS.md 第 3 节）。
    // 读 PointerInput 的空白拖拽像素增量：左右拖 = 绕世界竖直轴转（偏航），上下拖 = 朝相机前后倾（俯仰，有上限）。
    // 绕枢轴点转：枢轴 = 目标静止姿态下的本地点 localPivot（植株布局中心 (0, 0.32, 0)），转动时这个点在世界里不动。
    // 手感：角度先累加到"目标角"，实际角度平滑追随，并限制最大角速度；松手后带一点惯性。VR 里不会突然甩动。
    //
    // 用法：
    //   rotator.Enable();                 // 开始接收拖拽（阶段 Enter）
    //   rotator.Disable();                // 停止接收拖拽，已有的平滑运动会走完（阶段 Exit）
    //   rotator.ResetRotation(true);      // 平滑转回静止姿态（false = 立刻）
    // 静止姿态在 Awake 时从 target 记录；之后不要再用别的脚本移动 target（要移动就先 Disable，再调 CaptureRestPose）。
    // 执行顺序 -50：先于 PointerInput 拾取和 NodeMorpher 绘制更新姿态，同一帧里拾取和画面一致。
    // 注意：每帧都会写 target 的位置和旋转，别的脚本不要同时移动 target。
    [DefaultExecutionOrder(-50)]
    public class TargetRotator : MonoBehaviour
    {
        [Tooltip("被旋转的物体（Main 场景里是 Plant，即 NodeMorpher 所在物体）")]
        public Transform target;
        public PointerInput pointer;
        [Tooltip("决定俯仰轴的视角（固定相机）。为空时用 Camera.main，再没有就用世界 X 轴")]
        public Camera viewCamera;
        [Tooltip("枢轴点，target 静止姿态下的本地坐标。植株所有形态共用中心 (0, 0.32, 0)")]
        public Vector3 localPivot = new Vector3(0f, 0.32f, 0f);

        [Header("手感")]
        [Tooltip("每拖动 1 像素转多少度")]
        public float degreesPerPixel = 0.25f;
        [Tooltip("俯仰角上限（度），正负对称")]
        [Range(0f, 80f)] public float maxPitch = 25f;
        [Tooltip("实际角度追随目标角的时间常数（秒），越大越柔和")]
        public float smoothTime = 0.12f;
        [Tooltip("最大角速度（度/秒），VR 舒适度上限")]
        public float maxAngularSpeed = 90f;
        [Tooltip("松手后惯性：按松手前的拖拽速度再多转这么多秒")]
        public float inertiaSeconds = 0.18f;
        [Tooltip("从节点上按下拖动也旋转（PointerInput.allowNodeDrag 需为 true，且这一关不用节点拖拽）")]
        public bool rotateOnNodeDrag = true;

        public bool IsEnabled { get; private set; }
        public float Yaw => yaw;
        public float Pitch => pitch;
        // 还在转（追随目标角或复位中）
        public bool IsMoving => Mathf.Abs(targetYaw - yaw) > 0.01f || Mathf.Abs(targetPitch - pitch) > 0.01f;

        Vector3 restPosition;
        Quaternion restRotation;
        Vector3 worldPivot;
        bool hasRest;

        float yaw, pitch;             // 当前角度
        float targetYaw, targetPitch; // 目标角度
        float yawVel, pitchVel;       // SmoothDamp 用
        Vector2 dragVelocity;         // 拖拽角速度（度/秒），算惯性用
        bool dragging;
        Vector2 lastNodeDragPos;
        bool nodeDragging;

        void Awake()
        {
            if (target == null) target = transform;
            CaptureRestPose();
        }

        // 记录当前姿态为静止姿态（角度清零）。
        // 姿态和枢轴都记录在 target 的父空间里：父物体（PlantFit）按形态缩放 / 平移时，旋转和枢轴自动跟着走，不用重新记录
        public void CaptureRestPose()
        {
            if (target == null) return;
            restPosition = target.localPosition;
            restRotation = target.localRotation;
            worldPivot = restPosition + restRotation * Vector3.Scale(target.localScale, localPivot);
            hasRest = true;
            yaw = pitch = targetYaw = targetPitch = 0f;
            yawVel = pitchVel = 0f;
        }

        public void Enable()
        {
            if (IsEnabled) return;
            IsEnabled = true;
            if (pointer != null)
            {
                pointer.DragEmpty += HandleDrag;
                pointer.DragEmptyEnd += HandleDragEnd;
            }
        }

        public void Disable()
        {
            if (!IsEnabled) return;
            IsEnabled = false;
            if (pointer != null)
            {
                pointer.DragEmpty -= HandleDrag;
                pointer.DragEmptyEnd -= HandleDragEnd;
            }
            dragging = nodeDragging = false;
            dragVelocity = Vector2.zero;
        }

        // 转回静止姿态。偏航先折算到 ±180°，走最近的方向
        public void ResetRotation(bool smooth = true)
        {
            yaw = Mathf.DeltaAngle(0f, yaw);
            targetYaw = 0f;
            targetPitch = 0f;
            dragVelocity = Vector2.zero;
            if (!smooth)
            {
                yaw = pitch = 0f;
                yawVel = pitchVel = 0f;
                Apply();
            }
        }

        // 代码直接加角度（比如以后手柄摇杆），和拖拽一样受俯仰限制和速度限制
        public void AddRotation(float yawDegrees, float pitchDegrees)
        {
            targetYaw += yawDegrees;
            targetPitch = Mathf.Clamp(targetPitch + pitchDegrees, -maxPitch, maxPitch);
        }

        void OnDisable()
        {
            Disable();
        }

        void HandleDrag(Vector2 deltaPixels)
        {
            if (!IsEnabled) return;
            dragging = true;
            ApplyDragDelta(deltaPixels);
        }

        void HandleDragEnd()
        {
            dragging = false;
            AddInertia();
        }

        void ApplyDragDelta(Vector2 deltaPixels)
        {
            // 向右拖：朝向相机的一面往右走（绕 +Y 负方向转）；向上拖：朝向相机的一面往上翻
            float dYaw = -deltaPixels.x * degreesPerPixel;
            float dPitch = deltaPixels.y * degreesPerPixel;
            AddRotation(dYaw, dPitch);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            dragVelocity = Vector2.Lerp(dragVelocity, new Vector2(dYaw, dPitch) / dt, 0.5f);
        }

        void AddInertia()
        {
            if (inertiaSeconds <= 0f) return;
            Vector2 v = Vector2.ClampMagnitude(dragVelocity, maxAngularSpeed);
            AddRotation(v.x * inertiaSeconds, v.y * inertiaSeconds);
            dragVelocity = Vector2.zero;
        }

        void Update()
        {
            if (!hasRest || target == null) return;
            PollNodeDrag();

            // 拖拽停住不动的帧，惯性速度逐渐归零（停下再松手不会再甩）
            if (dragging) dragVelocity = Vector2.MoveTowards(dragVelocity, Vector2.zero, maxAngularSpeed * 8f * Time.deltaTime);

            float maxSpeed = maxAngularSpeed > 0f ? maxAngularSpeed : Mathf.Infinity;
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, smoothTime, maxSpeed);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, smoothTime, maxSpeed);
            Apply();
        }

        // 从节点上按下拖动：PointerInput 只发节点事件，没有像素增量，这里按指针位置自己算
        void PollNodeDrag()
        {
            bool active = IsEnabled && rotateOnNodeDrag && pointer != null && pointer.IsDraggingNode;
            if (active)
            {
                Vector2 pos = pointer.PointerPosition;
                if (nodeDragging)
                {
                    Vector2 delta = pos - lastNodeDragPos;
                    if (delta.sqrMagnitude > 0f) ApplyDragDelta(delta);
                }
                nodeDragging = true;
                dragging = true;
                lastNodeDragPos = pos;
            }
            else if (nodeDragging)
            {
                nodeDragging = false;
                dragging = false;
                if (IsEnabled) AddInertia();
            }
        }

        void Apply()
        {
            Vector3 up = Vector3.up;
            Vector3 right = PitchAxis();
            Quaternion r = Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(yaw, up);
            // 旋转轴是世界方向，换算到父空间（PlantFit 只有均匀缩放和平移，方向不变；保险起见仍按父旋转换算）
            var parent = target.parent;
            if (parent != null)
            {
                Quaternion inv = Quaternion.Inverse(parent.rotation);
                r = inv * r * parent.rotation;
            }
            target.localPosition = worldPivot + r * (restPosition - worldPivot);
            target.localRotation = r * restRotation;
        }

        // 相机水平方向的右侧（相机固定，所以每帧算一次也不贵）
        Vector3 PitchAxis()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return Vector3.right;
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f) return Vector3.right;
            return Vector3.Cross(Vector3.up, forward.normalized);
        }
    }
}
