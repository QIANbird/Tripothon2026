using Ghost.Core;
using UnityEngine;

namespace Ghost.Narrative
{
    // 开场剧情：黑屏 + 语音字幕。台词表 INTRO 段分两段：
    //   sequence（INTRO_001–008）播完后停住，等玩家点击屏幕任意处（SubtitlePanel.AdvancedWhileIdle）；
    //   afterClickSequence（INTRO_009–011）播完自动 Complete() 进入教学。
    // 按 N 跳关时 GameFlow 调 Exit()：停止对白、隐藏字幕、黑屏淡出。
    public class IntroStage : Stage
    {
        public DialoguePlayer player;
        public SubtitlePanel subtitles;
        public DialogueSequence sequence;
        [Tooltip("玩家点击后播放的第二段；为空时第一段播完直接进入教学")]
        public DialogueSequence afterClickSequence;
        public ScreenBlackout blackout;
        [Tooltip("离开开场时黑屏淡出的时长（秒）")]
        public float fadeOutTime = 1.2f;

        public override string PanelText => ""; // 黑屏期间不需要阶段面板文字

        bool waitingClick;

        public override void Enter()
        {
            base.Enter();
            waitingClick = false;
            if (blackout != null) blackout.SetImmediate(true);
            if (player == null || sequence == null)
            {
                Debug.LogWarning("[Dialogue] IntroStage 缺少 player 或 sequence，直接通关", this);
                Complete();
                return;
            }
            if (subtitles != null) subtitles.AdvancedWhileIdle += HandleIdleAdvance;
            player.Play(sequence, WaitForClick);
        }

        public override void Exit()
        {
            waitingClick = false;
            if (subtitles != null) subtitles.AdvancedWhileIdle -= HandleIdleAdvance;
            if (player != null && (player.CurrentSequence == sequence || player.CurrentSequence == afterClickSequence)) player.Stop();
            if (blackout != null) blackout.FadeTo(false, fadeOutTime);
            base.Exit();
        }

        void WaitForClick()
        {
            if (!IsActive) return;
            if (afterClickSequence == null || subtitles == null)
            {
                if (afterClickSequence != null) player.Play(afterClickSequence, Complete);
                else Complete();
                return;
            }
            waitingClick = true;
        }

        void HandleIdleAdvance()
        {
            if (!IsActive || !waitingClick) return;
            waitingClick = false;
            player.Play(afterClickSequence, Complete);
        }
    }
}
