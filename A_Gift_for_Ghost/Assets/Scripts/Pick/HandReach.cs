using System;
using UnityEngine;

namespace Ghost.Pick
{
    // 驱动手臂 IK 目标（Animation Rigging 的 Two Bone IK），在待机、果实、嘴边三个姿态之间插值；摘下后把果实挂到手上。
    // 姿态用"掌心抓握点（FruitHolder）要去的位置"描述。抓握点不在 IK 链上（它跟着手模型的摆放和滚转走），
    // 所以 IK 目标 = 姿态位置 + 修正量，修正量每帧按"抓握点实际位置 - 姿态位置"反馈收敛，几帧内对齐。
    // 举到嘴边时前臂自动滚转，让掌心朝上（略朝向眼睛），手托着果实送到嘴边。
    // 姿态位置来自 ArmVisualSettings（idleAnchor / mouthAnchor 是头的子物体，每帧同步资产里的数值）。
    // 插值在手臂根（头的子物体）的本地空间里做，玩家走路、转头时手跟着头走。
    // ikTarget 必须是 armRoot 的直接子物体（Rig 只认 Animator 下面的物体），这里只改它的 localPosition。
    // 手臂是占位（胶囊上臂 + Tripo 前臂/手模型，没有骨骼），正式手臂和动画之后替换。不读输入、不读 Camera.main。
    [DefaultExecutionOrder(100)] // 在 FirstPersonMotor 转头之后算目标，同一帧里 IK 用到的是最新的头部姿态
    public class HandReach : MonoBehaviour
    {
        public enum Pose { Idle, Fruit, Mouth }

        [Tooltip("手臂根（Animator + RigBuilder 所在物体，头的子物体，原点在眼睛），插值在它的本地空间里做")]
        public Transform armRoot;
        [Tooltip("Two Bone IK 的目标（手腕）")]
        public Transform ikTarget;
        [Tooltip("肘部骨骼（前臂轴 = 它的 +Z），用来算嘴边的滚转")]
        public Transform elbow;
        [Tooltip("待机时抓握点的位置（头的子物体）")]
        public Transform idleAnchor;
        [Tooltip("嘴边抓握点的位置（头的子物体）")]
        public Transform mouthAnchor;
        [Tooltip("前臂 + 手的摆放（掌心、抓握点、滚转）")]
        public ArmVisualPlacement placement;
        [Tooltip("姿态位置和嘴边滚转的微调（和 ArmVisualPlacement 同一个资产）")]
        public ArmVisualSettings settings;

        [Header("插值")]
        [Tooltip("伸向果实的时长（秒）")]
        public float reachSeconds = 0.35f;
        [Tooltip("摘下后收回待机位的时长（秒）")]
        public float retractSeconds = 0.3f;
        [Tooltip("举到嘴边的时长（秒）")]
        public float raiseSeconds = 0.45f;
        [Tooltip("咬一口时手往嘴里送、再退回的时长（秒）")]
        public float biteSeconds = 0.22f;
        [Tooltip("咬一口时手往嘴的方向送多远（米）")]
        public float bitePush = 0.035f;
        [Tooltip("摘下后果实滑到掌心的时长（秒）")]
        public float attachSeconds = 0.12f;
        [Tooltip("前臂滚转的最大角速度（度/秒）")]
        public float rollSpeed = 420f;

        [Header("IK 修正")]
        [Tooltip("每帧把抓握点误差的多少比例加到修正量里（0–1）")]
        [Range(0.05f, 1f)] public float correctionGain = 0.5f;
        [Tooltip("修正量上限（米）：够不着时不无限累积")]
        public float maxCorrection = 0.5f;

        public Pose CurrentPose { get; private set; } = Pose.Idle;
        // 正在姿态之间过渡（咬一口的前后动也算）
        public bool IsMoving { get; private set; }
        public bool IsPicking => pickTarget != null;
        public PickableFruit HeldFruit { get; private set; }

        // 摘下（果实挂到手上）时发出
        public event Action<PickableFruit> Picked;

        Transform Holder => placement != null && placement.fruitHolder != null ? placement.fruitHolder : ikTarget;

        // 抓握点目标（armRoot 本地）：过渡起点和当前值
        Vector3 fromGoal, currentGoal;
        // IK 目标 = 抓握点目标 + correction（armRoot 本地）
        Vector3 correction;
        bool correctionValid;
        float t, duration;
        float biteT = 1f;
        PickableFruit pickTarget;
        Vector3 fruitFromLocal;
        float attachT = 1f;
        float rollCurrent;

        void OnEnable()
        {
            SnapToIdle();
        }

        void Update()
        {
            if (ikTarget == null || armRoot == null) return;
            float dt = Time.deltaTime;
            SyncAnchors();

            // 1. 抓握点目标：姿态之间插值；咬一口时往眼睛方向送一下
            Vector3 goal = armRoot.InverseTransformPoint(PoseGoalWorld());
            if (IsMoving && biteT >= 1f)
            {
                t += dt / Mathf.Max(duration, 0.01f);
                currentGoal = Vector3.LerpUnclamped(fromGoal, goal, Smooth(Mathf.Clamp01(t)));
                if (t >= 1f)
                {
                    IsMoving = false;
                    OnArrived();
                }
            }
            else currentGoal = goal;

            Vector3 shownGoal = currentGoal;
            if (biteT < 1f)
            {
                biteT += dt / Mathf.Max(biteSeconds, 0.01f);
                // armRoot 原点在眼睛：-goal 就是从果实指向嘴
                Vector3 toMouth = currentGoal.sqrMagnitude > 1e-6f ? -currentGoal.normalized : Vector3.back;
                shownGoal += toMouth * (bitePush * Mathf.Sin(Mathf.Clamp01(biteT) * Mathf.PI));
                if (biteT >= 1f) IsMoving = false;
            }

            // 2. 反馈修正：抓握点上一帧的实际位置和目标之差（IK 在 Update 之后求解，这里读到的是上一帧结果）
            Vector3 holderLocal = armRoot.InverseTransformPoint(Holder.position);
            if (!correctionValid)
            {
                correction = ikTarget.localPosition - holderLocal;
                correctionValid = true;
            }
            else
            {
                correction += (shownGoal - holderLocal) * correctionGain;
                correction = Vector3.ClampMagnitude(correction, maxCorrection);
            }
            ikTarget.localPosition = shownGoal + correction;

            // 3. 前臂滚转：嘴边让掌心朝上（略朝向眼睛），其余姿态回到 0
            if (placement != null)
            {
                float rollTarget = CurrentPose == Pose.Mouth ? MouthRoll() : 0f;
                rollCurrent = Mathf.MoveTowardsAngle(rollCurrent, rollTarget, rollSpeed * dt);
                placement.roll = rollCurrent;
            }

            // 4. 果实贴到抓握点
            if (HeldFruit != null)
            {
                Transform ft = HeldFruit.transform;
                if (attachT < 1f)
                {
                    attachT += dt / Mathf.Max(attachSeconds, 0.01f);
                    ft.localPosition = Vector3.Lerp(fruitFromLocal, HeldFruit.HeldLocalPosition, Smooth(Mathf.Clamp01(attachT)));
                }
                else ft.localPosition = HeldFruit.HeldLocalPosition;
            }
        }

        // 伸手去摘：伸到果实，到了以后把果实挂到手上、收回待机，发 Picked。正在摘或手上已有果实时忽略
        public bool ReachAndPick(PickableFruit fruit)
        {
            if (fruit == null || pickTarget != null || HeldFruit != null) return false;
            pickTarget = fruit;
            fruit.SetPickable(false);
            BeginMove(Pose.Fruit, reachSeconds);
            return true;
        }

        public void GoIdle() => BeginMove(Pose.Idle, retractSeconds);

        public void GoMouth() => BeginMove(Pose.Mouth, raiseSeconds);

        // 咬一口的前后动。正在过渡时忽略
        public void NudgeBite()
        {
            if (IsMoving) return;
            biteT = 0f;
            IsMoving = true;
        }

        // 立刻回到待机，丢开手上的果实引用（果实本身由 PickableFruit.Restore 还原）
        public void ResetPose()
        {
            pickTarget = null;
            HeldFruit = null;
            attachT = 1f;
            SnapToIdle();
        }

        public void SnapToIdle()
        {
            IsMoving = false;
            biteT = 1f;
            CurrentPose = Pose.Idle;
            rollCurrent = 0f;
            if (placement != null) placement.roll = 0f;
            correctionValid = false;
            if (armRoot != null && idleAnchor != null)
            {
                SyncAnchors();
                currentGoal = armRoot.InverseTransformPoint(idleAnchor.position);
            }
        }

        void BeginMove(Pose pose, float seconds)
        {
            if (ikTarget == null || armRoot == null) return;
            fromGoal = currentGoal;
            CurrentPose = pose;
            duration = Mathf.Max(0.01f, seconds);
            t = 0f;
            biteT = 1f;
            IsMoving = true;
        }

        void OnArrived()
        {
            if (CurrentPose != Pose.Fruit || pickTarget == null) return;
            var fruit = pickTarget;
            pickTarget = null;
            HeldFruit = fruit;
            fruit.Detach(Holder);
            fruitFromLocal = fruit.transform.localPosition;
            attachT = 0f;
            BeginMove(Pose.Idle, retractSeconds);
            Picked?.Invoke(fruit);
        }

        void SyncAnchors()
        {
            if (settings == null) return;
            if (idleAnchor != null && idleAnchor.localPosition != settings.idlePalm) idleAnchor.localPosition = settings.idlePalm;
            if (mouthAnchor != null && mouthAnchor.localPosition != settings.mouthPalm) mouthAnchor.localPosition = settings.mouthPalm;
        }

        // 抓握点要去的世界位置
        Vector3 PoseGoalWorld()
        {
            switch (CurrentPose)
            {
                case Pose.Fruit:
                    if (pickTarget != null) return pickTarget.WorldCenter;
                    break;
                case Pose.Mouth:
                    if (mouthAnchor != null) return mouthAnchor.position;
                    break;
            }
            return idleAnchor != null ? idleAnchor.position : Holder.position;
        }

        // 让掌心朝向"上方偏向眼睛"需要的前臂滚转（度）。在前臂轴的垂直面里比较当前（不含滚转）和想要的掌心朝向
        float MouthRoll()
        {
            Transform forearm = elbow != null ? elbow : placement.transform;
            Vector3 axis = forearm.forward;
            Vector3 current = placement.transform.TransformDirection(placement.PalmNormalNoRoll);
            float face = settings != null ? settings.mouthFaceTowardEye : 0.3f;
            Vector3 toEye = (armRoot.position - Holder.position).normalized;
            Vector3 wanted = Vector3.Slerp(armRoot.up, toEye, face);
            Vector3 a = Vector3.ProjectOnPlane(current, axis);
            Vector3 b = Vector3.ProjectOnPlane(wanted, axis);
            float extra = settings != null ? settings.mouthRoll : 0f;
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return extra;
            return Vector3.SignedAngle(a, b, axis) + extra;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
