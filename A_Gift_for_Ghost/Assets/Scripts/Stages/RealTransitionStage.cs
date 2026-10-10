using Ghost.Core;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // 过渡（S4 → Pick，形态 Real）：进关时植株开始变成写实模型（GameFlow 进关即变形），同时播 S4_008。
    // 台词播完、变形结束、写实模型完全显现后自动进入 Pick。玩家这一关不操作。
    public class RealTransitionStage : Stage
    {
        public StageContext ctx;
        [Tooltip("和变形同时开始播放的台词（台词表 S4_008）")]
        public DialogueSequence sequence;
        [Tooltip("可选：写实模型溶解显现；为空时只等节点变形结束")]
        public RealModelHandoff handoff;
        [Tooltip("全部就绪后再停留多久（秒）")]
        public float holdAfter = 0.6f;

        public override string PanelText => "";

        bool linesDone;
        float readyTimer;

        public override void Enter()
        {
            base.Enter();
            linesDone = false;
            readyTimer = holdAfter;
            if (ctx == null)
            {
                Debug.LogError("[Stage] RealTransitionStage 缺少 ctx", this);
                return;
            }
            ctx.ResetShared();
            ctx.Play(sequence, () => linesDone = true);
        }

        public override void Exit()
        {
            if (ctx != null) ctx.ResetShared();
            base.Exit();
        }

        void Update()
        {
            if (!IsActive || !linesDone || ctx == null) return;
            if (ctx.morpher != null && ctx.morpher.IsMorphing) return;
            if (handoff != null && handoff.Reveal < 1f) return;
            readyTimer -= Time.deltaTime;
            if (readyTimer <= 0f) Complete();
        }
    }
}
