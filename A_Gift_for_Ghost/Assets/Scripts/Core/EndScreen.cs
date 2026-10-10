using UnityEngine.SceneManagement;

namespace Ghost.Core
{
    // 结束界面：GameFlow.FlowFinished 时淡入，点"重新开始"重新加载当前场景，回到开始界面。
    // 重载场景而不是 JumpTo(0)：各系统的运行时状态（写实度、PlantFit Hold、玩家相机、摘下的果实……）一次清干净。
    // 光标不在这里处理：最后一关之前的 Pick 阶段 Exit 时 PcCursorLock 已经恢复光标。
    public class EndScreen : FlowScreen
    {
        bool reloading;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            flow.FlowFinished += HandleFinished;
            SetVisible(false, immediate: true);
        }

        protected override void OnDestroy()
        {
            if (flow != null) flow.FlowFinished -= HandleFinished;
            base.OnDestroy();
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
