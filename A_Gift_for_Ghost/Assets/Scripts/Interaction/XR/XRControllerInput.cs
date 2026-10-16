using Ghost.Morph;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Interaction.XR
{
    // VR 端的交互发起方（docs/tasks/vr-migration.md 第 3 步）：右手柄射线，和 PointerInput 发同一组事件。
    // 挂在跟随右手柄指向姿态的物体上（XR Origin/Camera Offset/Right Aim，Tracked Pose Driver），射线 = 这个物体的 forward。
    // 选中状态和"是否批准"面板都在这里，玩法层看不到"选中"，只收到 Tap / Inspect / TapEmpty。
    //
    // 手势（读 Gameplay 动作表的 XR 绑定）：
    //   射线指向节点         → 只更新 HoverChanged（射线变亮），不改节点状态
    //   Select（扳机）短按节点 → 选中：节点略放大并朝玩家突起（NodeMorpher.SetPoseOverride），手柄旁显示批准面板
    //                          再按同一个节点 = 取消选中；按另一个节点 = 切换；按空白处 = 取消选中
    //   Select 短按 IInteractable → OnTap() + InteractableTapped（不影响选中）
    //   Select 从节点按住扫过   → DragStart / DragOver / DragEnd（S2 连线，需 allowNodeDrag；旋转开着时不进拖拽，见 rotator）
    //   Approve（A）          → 有选中：Tap(选中节点)，每按一次算一次；没有选中：TapEmpty（推进对白）
    //   Inspect（握键）按住     → 有选中：InspectStart(选中节点)；松开 → InspectEnd
    //   Rotate（右摇杆）       → DragEmpty(等效像素增量)，回中 → DragEmptyEnd。只发原始两轴，轴过滤由 Stage / TargetRotator 决定
    // 发 Tap / InspectStart 时 CurrentRay 暂时换成选中那一刻的射线（S4 按射线找叶片背后的虫子，要和选中时一致）。
    public class XRControllerInput : InteractionSource
    {
        [Tooltip("射线起点和方向（forward）。为空时用本物体")]
        public Transform aim;
        [Tooltip("玩家头部（主相机），选中节点朝它突起；批准面板朝向它")]
        public Transform head;
        public NodePicker picker;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Gameplay";
        [Tooltip("可选：旋转开着时（S3/S4）按住扳机不进节点拖拽。PC 上那两关的节点拖拽只用来转物体，VR 用摇杆转")]
        public TargetRotator rotator;
        [Tooltip("可选：选中后手柄旁的\"是否批准\"面板")]
        public XRApprovePanel approvePanel;

        [Header("射线")]
        public float maxDistance = 20f;
        [Tooltip("普通物体射线检测的层")]
        public LayerMask physicsMask = ~0;

        [Header("拖拽（S2）")]
        [Tooltip("按住扳机后射线转过这么多度算拖拽，否则算短按")]
        public float dragThresholdDegrees = 4f;
        [Tooltip("拖拽经过节点时按这个角度步长细分采样，手腕甩得快也不漏节点")]
        public float dragSampleDegrees = 0.5f;

        [Header("旋转（右摇杆）")]
        [Tooltip("摇杆推到底时每秒发出的等效像素（TargetRotator 0.25°/px → 360 px/s ≈ 90°/s）")]
        public float pixelsPerSecond = 360f;
        [Range(0f, 0.9f)] public float stickDeadZone = 0.2f;

        [Header("选中表现")]
        [Tooltip("选中节点的放大倍数")]
        public float selectedScale = 1.25f;
        [Tooltip("选中节点朝玩家方向突起的距离（米，世界空间）")]
        public float selectedLift = 0.04f;
        [Tooltip("放大 / 突起的过渡时长（秒）")]
        public float selectTransition = 0.12f;

        // 当前选中的节点（-1 = 没有）
        public int SelectedNode => selected;

        public override bool IsDraggingNode => gesture == Gesture.NodeDrag;
        public override bool IsDraggingEmpty => stickActive;

        enum Gesture { None, Pressed, NodeDrag, Blocked }

        InputActionMap map;
        InputAction selectAction;
        InputAction approveAction;
        InputAction inspectAction;
        InputAction rotateAction;

        Gesture gesture;
        Vector3 pressDirection;
        Vector3 lastDirection;
        int pressNode = -1;
        IInteractable pressInteractable;
        int lastDragNode = -1;
        bool stickActive;

        int selected = -1;
        Ray selectRay;
        // 批准面板被收起（按了扳机或握键）；选中新节点时复位，取消选中时面板随之隐藏
        bool approveDismissed;
        float selectWeight;
        // 选中节点原本就有姿态覆盖（教学演示方块）：不做放大突起，避免抢写
        bool selectOwnsPose;
        // 正在淡出的上一个选中节点
        int releasing = -1;
        float releasingWeight;

        Transform Aim => aim != null ? aim : transform;
        NodeMorpher Morpher => picker != null ? picker.morpher : null;

        void OnEnable()
        {
            if (actions == null)
            {
                Debug.LogError("[XRInput] XRControllerInput 缺少 actions", this);
                enabled = false;
                return;
            }
            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            selectAction = map.FindAction("Select", throwIfNotFound: true);
            approveAction = map.FindAction("Approve", throwIfNotFound: false);
            inspectAction = map.FindAction("Inspect", throwIfNotFound: false);
            rotateAction = map.FindAction("Rotate", throwIfNotFound: false);
            if (approveAction == null || rotateAction == null)
                Debug.LogWarning("[XRInput] Gameplay 动作表里缺少 Approve 或 Rotate，A 键批准 / 摇杆旋转不可用", this);
            map.Enable();
        }

        void OnDisable()
        {
            CancelAll();
            // PointerInput 共用同一张表，只在它也不用时才会被关掉；这里不主动 Disable，免得 PC 端失去输入
            map = null;
        }

        protected override void OnInputEnabledChanged(bool enabled)
        {
            if (!enabled) CancelAll();
        }

        void Update()
        {
            UpdateSelectionPose();
            if (map == null || !InputEnabled)
            {
                if (approvePanel != null) approvePanel.Hide();
                return;
            }

            Transform a = Aim;
            Ray ray = new Ray(a.position, a.forward);
            CurrentRay = ray;

            ValidateSelection();

            if (selectAction.WasPressedThisFrame() && gesture == Gesture.None) BeginPress(ray);
            if (gesture != Gesture.None) UpdateGesture(ray);
            if (selectAction.WasReleasedThisFrame() && gesture != Gesture.None) EndPress(ray);
            IsPressed = gesture != Gesture.None;

            if (approveAction != null && approveAction.WasPressedThisFrame()) Approve();
            UpdateInspect();
            UpdateStick();
            UpdateHover(ray);

            if (approvePanel != null)
            {
                if (selected >= 0 && !approveDismissed) approvePanel.Show(a, head);
                else approvePanel.Hide();
            }
        }

        // 详情面板（XRWorldCanvas Node 模式）的摆放点：最近一次握键查看的节点（松开后面板还要停留一会，所以不随取消选中清掉）
        public int LastInspectedNode { get; private set; } = -1;

        // 每次握键开始查看 +1，详情面板据此在每次打开时重新取一次深度
        public int InspectSerial { get; private set; }

        // 离 origin 最近的节点表面沿 normal 的深度（米）：S1/S2 = 平面矩阵的正面，S3/S4 = 当前离眼睛最近的那个节点。
        // 跳过有姿态覆盖的节点（选中突起、S1 漂浮），按矩阵本身的位置算
        public bool TryGetNearestNodeDepth(Vector3 origin, Vector3 normal, out float depth)
        {
            depth = float.PositiveInfinity;
            var morpher = Morpher;
            if (morpher == null || morpher.nodeSet == null) return false;
            int count = morpher.nodeSet.Count;
            for (int i = 0; i < count; i++)
            {
                if (morpher.HasPoseOverride(i)) continue;
                if (!morpher.TryGetNodeWorldSphere(i, out Vector3 c, out float r)) continue;
                float d = Vector3.Dot(c - origin, normal) - r;
                if (d > 0f && d < depth) depth = d;
            }
            return !float.IsInfinity(depth);
        }

        public bool TryGetNodeSphere(int id, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0f;
            var morpher = Morpher;
            return id >= 0 && morpher != null && morpher.TryGetNodeWorldSphere(id, out center, out radius);
        }

        // ---- 扳机：选中 / 拖拽 ----

        void BeginPress(Ray ray)
        {
            // 按下扳机就收起批准面板；如果这次按压选中了另一个节点，ToggleSelect 会重新打开
            approveDismissed = true;
            RaycastTargets(picker, ray, physicsMask, maxDistance, out pressNode, out pressInteractable, out _);
            pressDirection = ray.direction;
            lastDirection = ray.direction;
            gesture = Gesture.Pressed;
        }

        bool NodeDragAllowed => allowNodeDrag && (rotator == null || !rotator.IsEnabled);

        void UpdateGesture(Ray ray)
        {
            if (gesture == Gesture.Pressed)
            {
                if (Vector3.Angle(pressDirection, ray.direction) < dragThresholdDegrees) return;
                if (pressNode >= 0 && NodeDragAllowed)
                {
                    gesture = Gesture.NodeDrag;
                    lastDragNode = pressNode;
                    RaiseDragStart(pressNode);
                    SampleDrag(ray.origin, pressDirection, ray.direction);
                }
                else if (pressNode < 0 && pressInteractable == null)
                {
                    // 空白处按下后扫开：松开时仍按短按处理（取消选中），手柄射线不用来旋转
                    return;
                }
                else if (pressNode >= 0)
                {
                    // 不能拖拽的关卡：按住扫过也当短按，松开时看是否还在同一个节点上
                    return;
                }
                else gesture = Gesture.Blocked; // 从按钮上扫开：不点击
            }
            else if (gesture == Gesture.NodeDrag)
            {
                SampleDrag(ray.origin, lastDirection, ray.direction);
            }
            lastDirection = ray.direction;
        }

        // 上一帧到这一帧的射线方向之间按角度步长采样，依次报告进入的新节点
        void SampleDrag(Vector3 origin, Vector3 from, Vector3 to)
        {
            if (picker == null) return;
            float angle = Vector3.Angle(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(angle / Mathf.Max(0.05f, dragSampleDegrees)));
            for (int s = 1; s <= steps; s++)
            {
                Vector3 dir = Vector3.Slerp(from, to, (float)s / steps);
                int node = picker.Pick(new Ray(origin, dir), out float d);
                if (node < 0 || d > maxDistance || node == lastDragNode) continue;
                lastDragNode = node;
                RaiseDragOver(node);
            }
        }

        void EndPress(Ray ray)
        {
            switch (gesture)
            {
                case Gesture.Pressed:
                    RaycastTargets(picker, ray, physicsMask, maxDistance, out int node, out IInteractable target, out _);
                    if (pressNode >= 0)
                    {
                        // 按下和松开在同一个节点上才算；否则什么都不做
                        if (node == pressNode) ToggleSelect(node, ray);
                    }
                    else if (pressInteractable != null)
                    {
                        if (target == pressInteractable)
                        {
                            pressInteractable.OnTap();
                            RaiseInteractableTapped(pressInteractable);
                        }
                    }
                    else if (node < 0 && target == null) Deselect();
                    break;
                case Gesture.NodeDrag:
                    RaiseDragEnd();
                    break;
            }
            ResetGesture();
        }

        // ---- 选中 ----

        void ToggleSelect(int node, Ray ray)
        {
            if (node == selected)
            {
                Deselect();
                return;
            }
            Deselect();
            selected = node;
            selectRay = ray;
            approveDismissed = false;
            if (node == releasing)
            {
                // 重新选中还在淡出的节点：接着它当前的权重放大回去
                selectOwnsPose = true;
                selectWeight = releasingWeight;
                releasing = -1;
                releasingWeight = 0f;
            }
            else
            {
                var morpher = Morpher;
                selectOwnsPose = morpher != null && !morpher.HasPoseOverride(node);
                selectWeight = 0f;
            }
            UpdateSelectionPose();
        }

        void Deselect()
        {
            if (selected < 0) return;
            EndInspect();
            if (selectOwnsPose)
            {
                // 上一个还在淡出的直接清掉，只保留一个淡出中的节点
                ClearReleasing();
                releasing = selected;
                releasingWeight = selectWeight;
            }
            selected = -1;
            selectOwnsPose = false;
            selectWeight = 0f;
        }

        // 选中的节点不能再选时（变形开始、被隐藏、被阶段筛掉）自动取消
        void ValidateSelection()
        {
            if (selected < 0) return;
            var morpher = Morpher;
            bool valid = morpher != null && morpher.IsReady && !morpher.IsMorphing
                         && morpher.GetNodeVisibility(selected) >= 0.5f
                         && (picker.Filter == null || picker.Filter(selected));
            if (!valid) Deselect();
        }

        // 放大 + 朝玩家突起：姿态每帧按当前形态和头的位置重算（S3/S4 植株在转）
        void UpdateSelectionPose()
        {
            var morpher = Morpher;
            if (morpher == null || !morpher.IsReady) return;
            float step = selectTransition > 0f ? Time.deltaTime / selectTransition : 1f;

            if (releasing >= 0)
            {
                releasingWeight = Mathf.MoveTowards(releasingWeight, 0f, step);
                if (releasingWeight <= 0f || morpher.IsMorphing) ClearReleasing();
                else morpher.SetPoseOverride(releasing, SelectedPose(morpher, releasing), Smooth(releasingWeight));
            }

            if (selected >= 0 && selectOwnsPose)
            {
                selectWeight = Mathf.MoveTowards(selectWeight, 1f, step);
                morpher.SetPoseOverride(selected, SelectedPose(morpher, selected), Smooth(selectWeight));
            }
        }

        void ClearReleasing()
        {
            if (releasing < 0) return;
            var morpher = Morpher;
            if (morpher != null && releasing != selected) morpher.ClearPoseOverride(releasing);
            releasing = -1;
            releasingWeight = 0f;
        }

        NodePose SelectedPose(NodeMorpher morpher, int id)
        {
            var pose = morpher.nodeSet.nodes[id].GetPose(morpher.CurrentForm);
            var t = morpher.transform;
            Vector3 world = t.TransformPoint(pose.position);
            Vector3 eye = head != null ? head.position : Aim.position;
            Vector3 toEye = eye - world;
            if (toEye.sqrMagnitude > 1e-8f)
            {
                // 世界距离换算到本地：PlantFit 是均匀缩放
                float scale = Mathf.Max(Mathf.Abs(t.lossyScale.x), 1e-4f);
                pose.position += t.InverseTransformDirection(toEye.normalized) * (selectedLift / scale);
            }
            pose.scale *= selectedScale;
            return pose;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        // ---- A：批准 / 推进对白 ----

        void Approve()
        {
            if (selected >= 0)
            {
                approveDismissed = true; // 按 A 批准后收起批准面板（之后再按 A 仍算一次批准）
                WithSelectRay(() => RaiseTap(selected));
            }
            else RaiseTapEmpty();
        }

        // ---- 握键：查看详情 ----

        void UpdateInspect()
        {
            if (inspectAction == null) return;
            if (inspectAction.WasPressedThisFrame() && selected >= 0 && InspectingNode < 0)
            {
                int id = selected;
                InspectingNode = id;
                LastInspectedNode = id;
                InspectSerial++;
                approveDismissed = true; // 握键看详情时收起批准面板
                WithSelectRay(() => RaiseInspectStart(id));
            }
            if (inspectAction.WasReleasedThisFrame()) EndInspect();
        }

        void EndInspect()
        {
            if (InspectingNode < 0) return;
            InspectingNode = -1;
            RaiseInspectEnd();
        }

        // 订阅方在事件里读 CurrentRay 时拿到的是选中那一刻的射线
        void WithSelectRay(System.Action raise)
        {
            Ray live = CurrentRay;
            CurrentRay = selectRay;
            try { raise(); }
            finally { CurrentRay = live; }
        }

        // ---- 右摇杆：旋转 ----

        void UpdateStick()
        {
            Vector2 stick = rotateAction != null ? rotateAction.ReadValue<Vector2>() : Vector2.zero;
            bool active = allowEmptyDrag && stick.magnitude > stickDeadZone;
            if (active)
            {
                // 死区外重新映射到 0–1，推得轻时转得慢
                float k = Mathf.InverseLerp(stickDeadZone, 1f, Mathf.Min(1f, stick.magnitude));
                Vector2 delta = stick.normalized * (k * pixelsPerSecond * Time.deltaTime);
                stickActive = true;
                RaiseDragEmpty(delta);
            }
            else EndStick();
        }

        void EndStick()
        {
            if (!stickActive) return;
            stickActive = false;
            RaiseDragEmptyEnd();
        }

        // ---- 悬停 ----

        void UpdateHover(Ray ray)
        {
            RaycastTargets(picker, ray, physicsMask, maxDistance, out int node, out IInteractable interactable, out float distance);
            HoverDistance = distance;
            SetHover(node, interactable);
        }

        void SetHover(int node, IInteractable interactable)
        {
            if (node != HoveredNode)
            {
                HoveredNode = node;
                RaiseHoverChanged(node);
            }
            if (!ReferenceEquals(interactable, HoveredInteractable))
            {
                var previous = HoveredInteractable;
                HoveredInteractable = interactable;
                if (IsAlive(previous)) previous.OnHoverExit();
                if (interactable != null) interactable.OnHoverEnter();
            }
        }

        // ---- 收尾 ----

        void CancelAll()
        {
            if (gesture == Gesture.NodeDrag) RaiseDragEnd();
            ResetGesture();
            EndStick();
            Deselect();
            ClearReleasing();
            IsPressed = false;
            SetHover(-1, null);
            HoverDistance = float.PositiveInfinity;
            if (approvePanel != null) approvePanel.Hide();
        }

        void ResetGesture()
        {
            gesture = Gesture.None;
            pressNode = -1;
            pressInteractable = null;
            lastDragNode = -1;
        }
    }
}
