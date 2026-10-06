using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Core
{
    // 调试跳关：读取 Debug 动作表里的 NextStage（默认 N）和 JumpStage1–9（默认 Shift + 数字键 1–9）。
    // 只读 Action，不判断具体按键；键位在 InputSystem_Actions.inputactions 里改。
    public class GameFlowDebug : MonoBehaviour
    {
        public GameFlow flow;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Debug";

        const int JumpCount = 9;

        InputActionMap map;
        InputAction nextAction;
        InputAction[] jumpActions;

        void OnEnable()
        {
            if (flow == null || actions == null)
            {
                Debug.LogError("[Flow] GameFlowDebug 缺少 flow 或 actions", this);
                enabled = false;
                return;
            }

            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            nextAction = map.FindAction("NextStage", throwIfNotFound: true);
            nextAction.performed += OnNext;

            jumpActions = new InputAction[JumpCount];
            for (int i = 0; i < JumpCount; i++)
            {
                jumpActions[i] = map.FindAction($"JumpStage{i + 1}", throwIfNotFound: true);
                jumpActions[i].performed += OnJump;
            }
            map.Enable();
        }

        void OnDisable()
        {
            if (jumpActions == null) return;
            nextAction.performed -= OnNext;
            foreach (var action in jumpActions) action.performed -= OnJump;
            // 其他脚本可能共用同一个资产，这里只关掉 Debug 表
            map.Disable();
            jumpActions = null;
        }

        void OnNext(InputAction.CallbackContext context)
        {
            flow.Next();
        }

        // Shift + 1 跳到第 0 关（Intro），Shift + 9 跳到第 8 关
        void OnJump(InputAction.CallbackContext context)
        {
            int index = System.Array.IndexOf(jumpActions, context.action);
            if (index >= 0) flow.JumpTo(index);
        }
    }
}
