using Ghost.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ghost.Interaction
{
    // PC 端的交互发起方：读 Gameplay 动作表的 Point（指针位置）和 Select（按下），
    // 从指定相机发射线，同时测试节点（NodePicker）和普通物体（Physics + IInteractable），谁近算谁。
    // 只发事件，不包含任何玩法逻辑。VR 版的发起方是 XR/XRControllerInput，两者都继承 InteractionSource，由 InteractionRouter 选一个。
    //
    // 手势：
    //   按下和松开都在同一个节点上，且移动不超过 dragThreshold 像素 → Tap(nodeId)
    //   从节点上按下并移动超过阈值 → DragStart(nodeId)，之后每进入一个新节点 → DragOver(nodeId)，松开 → DragEnd()
    //   从空白处按下并移动超过阈值 → 每帧 DragEmpty(像素增量)，松开 → DragEmptyEnd()
    //   按下和松开都在同一个 IInteractable 上 → 调它的 OnTap()，并发 InteractableTapped
    //   按下和松开都在空白处（没有节点、物体和 HUD） → TapEmpty()（推进对白用；节点优先）
    //   Inspect（右键）按下时指针下有节点 → InspectStart(nodeId)；松开 → InspectEnd()（查看详情用，和左键手势互不影响）
    public class PointerInput : InteractionSource
    {
        [Tooltip("发射线的相机（固定相机）。交互物体本身不做射线检测")]
        public Camera rayCamera;
        public NodePicker picker;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Gameplay";

        [Tooltip("按下后移动超过这么多像素算拖拽，否则算点击")]
        public float dragThreshold = 8f;
        [Tooltip("拖拽经过节点时按这个像素步长细分采样，鼠标移动快时也不会漏掉节点")]
        public float dragSampleStep = 6f;

        [Tooltip("普通物体射线检测的层")]
        public LayerMask physicsMask = ~0;
        public float maxDistance = 20f;

        [Tooltip("指针在 UGUI（HUD）上时不拾取节点、不开始手势（比赛期间的 Screen Space HUD，见 PROJECT_SUMMARY 技术债）")]
        public bool blockWhenOverUI = true;

        // 事件和状态属性声明在 InteractionSource
        public override bool IsDraggingNode => gesture == Gesture.NodeDrag;
        public override bool IsDraggingEmpty => gesture == Gesture.EmptyDrag;

        protected override void OnInputEnabledChanged(bool enabled)
        {
            if (!enabled) CancelGesture();
        }

        enum Gesture { None, Pressed, NodeDrag, EmptyDrag, Blocked }

        InputActionMap map;
        InputAction pointAction;
        InputAction selectAction;
        InputAction inspectAction;

        Gesture gesture;
        Vector2 pressPosition;
        Vector2 lastPosition;
        int pressNode = -1;
        IInteractable pressInteractable;
        int lastDragNode = -1;

        void OnEnable()
        {
            if (actions == null || rayCamera == null)
            {
                Debug.LogError("[Pointer] PointerInput 缺少 actions 或 rayCamera", this);
                enabled = false;
                return;
            }
            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            pointAction = map.FindAction("Point", throwIfNotFound: true);
            selectAction = map.FindAction("Select", throwIfNotFound: true);
            inspectAction = map.FindAction("Inspect", throwIfNotFound: false);
            if (inspectAction == null) Debug.LogWarning("[Pointer] Gameplay 动作表里没有 Inspect，右键查看详情不可用", this);
            map.Enable();
        }

        void OnDisable()
        {
            CancelGesture();
            // 其他脚本可能共用同一个资产，这里只关掉 Gameplay 表
            if (map != null) map.Disable();
            map = null;
        }

        void Update()
        {
            if (map == null) return;
            if (!InputEnabled)
            {
                return;
            }
            // 暂停中不派发任何节点交互（暂停界面的按钮走 UGUI EventSystem）；按到一半的手势收尾
            if (PauseMenu.IsPaused)
            {
                CancelGesture();
                return;
            }

            Vector2 position = pointAction.ReadValue<Vector2>();
            PointerPosition = position;
            CurrentRay = rayCamera.ScreenPointToRay(position);

            bool pressedThisFrame = selectAction.WasPressedThisFrame();
            bool releasedThisFrame = selectAction.WasReleasedThisFrame();

            bool overUI = IsPointerOverUI();
            if (pressedThisFrame && gesture == Gesture.None)
            {
                // 在 HUD 上按下：这次按压整体作废，不点节点也不旋转
                if (overUI) { gesture = Gesture.Blocked; pressNode = -1; pressInteractable = null; pressPosition = position; }
                else BeginPress(position);
            }

            if (gesture != Gesture.None) UpdateGesture(position);

            if (releasedThisFrame && gesture != Gesture.None) EndPress(position);

            IsPressed = gesture != Gesture.None;
            UpdateInspect(overUI);
            if (overUI) SetHover(-1, null);
            else UpdateHover();
            lastPosition = position;
        }

        void Raycast(Ray ray, out int nodeId, out IInteractable interactable, out float distance)
        {
            RaycastTargets(picker, ray, physicsMask, maxDistance, out nodeId, out interactable, out distance);
        }

        void BeginPress(Vector2 position)
        {
            Raycast(rayCamera.ScreenPointToRay(position), out pressNode, out pressInteractable, out _);
            pressPosition = position;
            lastPosition = position;
            gesture = Gesture.Pressed;
        }

        void UpdateGesture(Vector2 position)
        {
            if (gesture == Gesture.Pressed)
            {
                if ((position - pressPosition).sqrMagnitude < dragThreshold * dragThreshold) return;

                if (pressNode >= 0)
                {
                    if (allowNodeDrag)
                    {
                        gesture = Gesture.NodeDrag;
                        lastDragNode = pressNode;
                        RaiseDragStart(pressNode);
                        SampleDrag(pressPosition, position);
                    }
                    else gesture = Gesture.Blocked;
                }
                else if (pressInteractable == null && allowEmptyDrag)
                {
                    gesture = Gesture.EmptyDrag;
                    // 第一帧补上从按下点到现在的位移
                    RaiseDragEmpty(position - pressPosition);
                }
                else gesture = Gesture.Blocked; // 从按钮上拖开：不点击也不旋转
                return;
            }

            if (gesture == Gesture.NodeDrag)
            {
                SampleDrag(lastPosition, position);
            }
            else if (gesture == Gesture.EmptyDrag)
            {
                Vector2 delta = position - lastPosition;
                if (delta.sqrMagnitude > 0f) RaiseDragEmpty(delta);
            }
        }

        // 沿上一帧到这一帧的指针线段按像素步长采样，依次报告进入的新节点
        void SampleDrag(Vector2 from, Vector2 to)
        {
            if (picker == null) return;
            float length = (to - from).magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(1f, dragSampleStep)));
            for (int s = 1; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(from, to, (float)s / steps);
                int node = picker.Pick(rayCamera.ScreenPointToRay(p), out float d);
                if (node < 0 || d > maxDistance || node == lastDragNode) continue;
                lastDragNode = node;
                RaiseDragOver(node);
            }
        }

        void EndPress(Vector2 position)
        {
            switch (gesture)
            {
                case Gesture.Pressed:
                    // 松开时再测一次，按下和松开在同一个目标上才算点击
                    Raycast(rayCamera.ScreenPointToRay(position), out int node, out IInteractable target, out _);
                    if (pressNode >= 0 && node == pressNode) RaiseTap(node);
                    else if (pressInteractable != null && target == pressInteractable)
                    {
                        pressInteractable.OnTap();
                        RaiseInteractableTapped(pressInteractable);
                    }
                    else if (pressNode < 0 && pressInteractable == null && node < 0 && target == null)
                        RaiseTapEmpty();
                    break;
                case Gesture.NodeDrag:
                    RaiseDragEnd();
                    break;
                case Gesture.EmptyDrag:
                    RaiseDragEmptyEnd();
                    break;
            }
            ResetGesture();
        }

        bool IsPointerOverUI()
        {
            if (!blockWhenOverUI) return false;
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        void UpdateInspect(bool overUI)
        {
            if (inspectAction == null) return;
            if (inspectAction.WasPressedThisFrame() && !overUI)
            {
                Raycast(CurrentRay, out int node, out _, out _);
                if (node >= 0)
                {
                    InspectingNode = node;
                    RaiseInspectStart(node);
                }
            }
            if (inspectAction.WasReleasedThisFrame()) EndInspect();
        }

        void EndInspect()
        {
            if (InspectingNode < 0) return;
            InspectingNode = -1;
            RaiseInspectEnd();
        }

        void UpdateHover()
        {
            int node;
            IInteractable interactable;
            float distance;
            Raycast(CurrentRay, out node, out interactable, out distance);
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


        void CancelGesture()
        {
            if (gesture == Gesture.NodeDrag) RaiseDragEnd();
            else if (gesture == Gesture.EmptyDrag) RaiseDragEmptyEnd();
            EndInspect();
            ResetGesture();
            IsPressed = false;
            SetHover(-1, null);
            HoverDistance = float.PositiveInfinity;
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
