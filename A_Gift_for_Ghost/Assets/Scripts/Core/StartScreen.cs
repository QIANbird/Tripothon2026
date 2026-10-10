namespace Ghost.Core
{
    // 开始界面：Play 后显示，点"开始"淡出并 GameFlow.Begin()。
    // 调试跳关（N / Shift+数字）直接进关时也会收起。GameFlow.autoStart 勾上时一开始就不显示。
    public class StartScreen : FlowScreen
    {
        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            flow.StageEntered += HandleStageEntered;
            SetVisible(!flow.autoStart && !flow.HasStarted, immediate: true);
        }

        protected override void OnDestroy()
        {
            if (flow != null) flow.StageEntered -= HandleStageEntered;
            base.OnDestroy();
        }

        protected override void OnButton()
        {
            SetVisible(false, immediate: false);
            flow.Begin();
        }

        void HandleStageEntered(Stage stage) => SetVisible(false, immediate: false);
    }
}
