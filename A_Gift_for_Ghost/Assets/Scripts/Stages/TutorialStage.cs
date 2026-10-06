using System.Collections;
using Ghost.Agent;
using Ghost.Gameplay;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // 新手教学（第 4 节，形态 Matrix）。【占位流程】策划尚未给出具体步骤（"待策划补充"），
    // 这里用三种难度各体验一次：先 Easy（点一次解决）→ Medium（点 3 次）→ Hard（点一次后 2–3 s 再闪），
    // 然后 Complete()。每一步配一句对白字幕（占位台词，资产在 Assets/Data/Narrative/Tutorial*.asset）。
    public class TutorialStage : IssueStage
    {
        [Header("教学对白（占位）")]
        public DialogueSequence introSequence;
        public DialogueSequence afterEasySequence;
        public DialogueSequence afterMediumSequence;
        public DialogueSequence afterHardSequence;

        [Header("教学节点（由 MainSceneMenu 按 Matrix 布局填好）")]
        [Tooltip("简单问题节点（点一次解决）")]
        public int easyNode = -1;
        [Tooltip("中等问题节点（点 3 次）")]
        public int mediumNode = -1;
        [Tooltip("困难问题节点（解决不了，暂停后重新闪烁）")]
        public int hardNode = -1;

        Coroutine afterHardRoutine;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null) return;

            SetupTaskPanel();
            ctx.Play(introSequence, BeginEasy);
        }

        public override void Exit()
        {
            if (afterHardRoutine != null)
            {
                StopCoroutine(afterHardRoutine);
                afterHardRoutine = null;
            }
            base.Exit();
        }

        void SetupTaskPanel()
        {
            if (ctx.taskPanel == null) return;
            ctx.taskPanel.SetTitle("教学 · 授权处理");
            ctx.taskPanel.SetModeLabel("FULL PROXY");
            ctx.taskPanel.SetTasks(new[]
            {
                new AgentTask("简单异常", TaskState.Pending),
                new AgentTask("中等异常", TaskState.Pending),
                new AgentTask("困难异常", TaskState.Pending),
            });
            ctx.taskPanel.SetMetric("完成率", 0f, "0 / 3");
            ctx.taskPanel.SetMetric("置信度", 0.4f);
        }

        void BeginEasy()
        {
            if (!IsActive) return;
            ctx.issues.AddIssue(easyNode, IssueDifficulty.Easy);
            SetTask(0, TaskState.Running);
        }

        protected override void OnIssueAttempted(NodeIssue issue, bool solved)
        {
            if (issue.nodeId == mediumNode && ctx.taskPanel != null)
            {
                int remaining = issue.clicksRemaining;
                int total = ctx.issues.mediumClicks;
                int done = total - remaining;
                ctx.taskPanel.UpdateTask(1, $"中等异常　{done} / {total}", solved ? TaskState.Done : TaskState.Running);
            }
            if (issue.nodeId == hardNode && !solved)
            {
                SetTask(2, TaskState.Failed);
                ctx.Play(afterHardSequence);
                if (afterHardRoutine != null) StopCoroutine(afterHardRoutine);
                afterHardRoutine = StartCoroutine(WaitThenFinish());
            }
        }

        protected override void OnIssueResolved(NodeIssue issue)
        {
            if (issue.nodeId == easyNode)
            {
                SetTask(0, TaskState.Done);
                ctx.taskPanel.SetMetric("完成率", 1f / 3f, "1 / 3");
                ctx.taskPanel.SetMetric("置信度", 0.55f);
                ctx.Play(afterEasySequence, BeginMedium);
            }
            else if (issue.nodeId == mediumNode)
            {
                SetTask(1, TaskState.Done);
                ctx.taskPanel.SetMetric("完成率", 2f / 3f, "2 / 3");
                ctx.taskPanel.SetMetric("置信度", 0.7f);
                ctx.Play(afterMediumSequence, BeginHard);
            }
        }

        void BeginMedium()
        {
            if (!IsActive) return;
            ctx.issues.AddIssue(mediumNode, IssueDifficulty.Medium);
            SetTask(1, TaskState.Running);
        }

        void BeginHard()
        {
            if (!IsActive) return;
            ctx.issues.AddIssue(hardNode, IssueDifficulty.Hard);
            SetTask(2, TaskState.Running);
        }

        IEnumerator WaitThenFinish()
        {
            // 让玩家看到困难问题重新闪烁（NodeIssueSystem 的 hardRetryDelay 是 2–3 s）
            yield return new WaitForSeconds(3.4f);
            afterHardRoutine = null;
            if (!IsActive) yield break;
            ctx.taskPanel.SetMetric("完成率", 1f, "3 / 3");
            ctx.taskPanel.SetMetric("置信度", 0.62f);
            Complete();
        }

        void SetTask(int index, TaskState state)
        {
            if (ctx.taskPanel != null) ctx.taskPanel.UpdateTask(index, state);
        }
    }
}
