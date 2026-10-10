using UnityEngine;

namespace Ghost.Core
{
    // 占位阶段：只显示一行字，没有通关条件，用调试键 N（GameFlowDebug）进入下一关。
    // 以后每个阶段换成自己的 Stage 子类（如 S1Stage），这个组件就可以删掉。
    public class PlaceholderStage : Stage
    {
        [Tooltip("阶段面板上显示的占位文字")]
        public string message = "";

        public override string PanelText => message;

        public override void Enter()
        {
            base.Enter();
            Debug.Log($"[Flow] 进入占位阶段 {stageName}", this);
        }
    }
}
