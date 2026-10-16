using Ghost.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.Interaction
{
    // 暂停键：读 Gameplay 表的 Pause（PC Esc / VR 左手柄 menu，绑定都在同一个 Action 上），只调 PauseMenu.Toggle()。
    // PC / VR 共用一个读取方：两个发起方各读一次同一个 Action 会在一帧里切两次。挂在常驻的 Pointer 物体上
    public class PauseInput : MonoBehaviour
    {
        public PauseMenu menu;
        [Tooltip("InputSystem_Actions.inputactions 资产")]
        public InputActionAsset actions;
        public string mapName = "Gameplay";
        public string actionName = "Pause";

        InputAction pauseAction;

        void OnEnable()
        {
            if (menu == null || actions == null)
            {
                Debug.LogError("[Pause] PauseInput 缺少 menu 或 actions", this);
                enabled = false;
                return;
            }
            var map = actions.FindActionMap(mapName, throwIfNotFound: false);
            pauseAction = map?.FindAction(actionName, throwIfNotFound: false);
            if (pauseAction == null)
            {
                Debug.LogWarning($"[Pause] {mapName} 动作表里没有 {actionName}，暂停不可用", this);
                return;
            }
            map.Enable(); // 其他输入组件也启用同一张表，重复启用无副作用
        }

        void OnDisable()
        {
            pauseAction = null;
        }

        void Update()
        {
            if (pauseAction != null && pauseAction.WasPressedThisFrame()) menu.Toggle();
        }
    }
}
