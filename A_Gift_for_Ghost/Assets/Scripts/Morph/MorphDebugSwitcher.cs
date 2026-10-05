using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Morph
{
    // 调试用：读取 Debug 动作表里的 MorphStage1–5，切换 NodeMorpher 的形态。
    // 只读 Action，不判断具体按键；键位在 InputSystem_Actions.inputactions 里改（默认数字键 1–5）
    public class MorphDebugSwitcher : MonoBehaviour
    {
        public NodeMorpher morpher;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Debug";

        // 下标对应按键 1–5，从抽象到写实
        static readonly MorphForm[] FormOrder =
        {
            MorphForm.Matrix,
            MorphForm.Circuit,
            MorphForm.Network,
            MorphForm.Geometric,
            MorphForm.Real,
        };

        InputActionMap map;
        InputAction[] stageActions;

        void OnEnable()
        {
            if (morpher == null || actions == null)
            {
                Debug.LogError("[Morph] MorphDebugSwitcher 缺少 morpher 或 actions", this);
                enabled = false;
                return;
            }

            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            stageActions = new InputAction[FormOrder.Length];
            for (int i = 0; i < FormOrder.Length; i++)
            {
                stageActions[i] = map.FindAction($"MorphStage{i + 1}", throwIfNotFound: true);
                stageActions[i].performed += OnStage;
            }
            map.Enable();
        }

        void OnDisable()
        {
            if (stageActions == null) return;
            foreach (var action in stageActions) action.performed -= OnStage;
            // 其他脚本可能共用同一个资产，这里只关掉 Debug 表
            map.Disable();
            stageActions = null;
        }

        void OnStage(InputAction.CallbackContext context)
        {
            int index = System.Array.IndexOf(stageActions, context.action);
            if (index >= 0) morpher.MorphTo(FormOrder[index]);
        }
    }
}
