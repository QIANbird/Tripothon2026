using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ghost.Core
{
    // 结束界面：GameFlow.FlowFinished 时淡入，底部提示"按下A返回开始界面"就是按钮，确认后重新加载当前场景，回到开始界面。
    // 确认方式：点按钮（PC 鼠标）、Gameplay 表的 Advance（PC 空格 / 回车），VR 按 A（XRUIRayTarget.approveKey，XRUIBuilder 里勾上）。
    // 重载场景而不是 JumpTo(0)：各系统的运行时状态（写实度、PlantFit Hold、玩家相机、摘下的果实……）一次清干净。
    // 光标不在这里处理：最后一关之前的 Pick 阶段 Exit 时 PcCursorLock 已经恢复光标。
    public class EndScreen : FlowScreen
    {
        [Tooltip("InputSystem_Actions.inputactions；为空时只能点按钮")]
        public InputActionAsset actions;
        public string mapName = "Gameplay";
        [Tooltip("淡入到这个透明度后才接受空格 / 回车，避免把上一关的按键带进来")]
        public float acceptAlpha = 0.95f;

        InputAction advanceAction;
        bool reloading;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            flow.FlowFinished += HandleFinished;
            SetVisible(false, immediate: true);
            var map = actions != null ? actions.FindActionMap(mapName, throwIfNotFound: false) : null;
            advanceAction = map?.FindAction("Advance", throwIfNotFound: false);
            map?.Enable(); // 其他输入组件也启用同一张表，重复启用无副作用
        }

        protected override void OnDestroy()
        {
            if (flow != null) flow.FlowFinished -= HandleFinished;
            base.OnDestroy();
        }

        protected override void Update()
        {
            base.Update();
            if (advanceAction != null && group.interactable && group.alpha >= acceptAlpha
                && advanceAction.WasPressedThisFrame())
                OnButton();
        }

        void HandleFinished() => SetVisible(true, immediate: false);

        protected override void OnButton()
        {
            if (reloading) return;
            reloading = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
