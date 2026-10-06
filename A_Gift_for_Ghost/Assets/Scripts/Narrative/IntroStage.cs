using Ghost.Core;
using UnityEngine;

namespace Ghost.Narrative
{
    // 开场剧情：黑屏 + 语音字幕，播完自动 Complete() 进入教学。
    // 按 N 跳关时 GameFlow 调 Exit()：停止对白、隐藏字幕、黑屏淡出。
    public class IntroStage : Stage
    {
        public DialoguePlayer player;
        public DialogueSequence sequence;
        public ScreenBlackout blackout;
        [Tooltip("离开开场时黑屏淡出的时长（秒）")]
        public float fadeOutTime = 1.2f;

        public override string PanelText => ""; // 黑屏期间不需要阶段面板文字

        public override void Enter()
        {
            base.Enter();
            if (blackout != null) blackout.SetImmediate(true);
            if (player == null || sequence == null)
            {
                Debug.LogWarning("[Dialogue] IntroStage 缺少 player 或 sequence，直接通关", this);
                Complete();
                return;
            }
            player.Play(sequence, Complete);
        }

        public override void Exit()
        {
            if (player != null && player.CurrentSequence == sequence) player.Stop();
            if (blackout != null) blackout.FadeTo(false, fadeOutTime);
            base.Exit();
        }
    }
}
