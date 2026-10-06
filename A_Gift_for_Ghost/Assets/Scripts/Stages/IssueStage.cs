using Ghost.Core;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // 教学和 S1 共用的"节点问题"阶段基类（第 4 节通用机制）：
    // 点击节点 = 授权 Agent 尝试一次（NodeIssueSystem.TryAttempt），同时弹出节点详情（Status 深度）。
    // Enter 订阅指针和问题事件，Exit 全部退订并清理（问题、闪烁、弹窗、询问框、对白），保证 N 键和 Shift + 数字跳关两个方向都干净。
    public abstract class IssueStage : Stage
    {
        public StageContext ctx;

        [Tooltip("详情弹窗多久刷新一次文字（秒），用来反映困难问题重新闪烁等状态变化")]
        public float popupRefreshInterval = 0.25f;

        // 真实阶段不用 G1 的阶段面板（位置会和 Agent 界面、字幕挤在一起），提示由对白字幕和任务面板承担
        public override string PanelText => "";

        float nextPopupRefresh;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null)
            {
                Debug.LogError($"[Stage] {stageName} 缺少 StageContext", this);
                return;
            }
            ctx.ResetShared();
            if (ctx.pointer != null) ctx.pointer.Tap += HandleTap;
            if (ctx.issues != null)
            {
                ctx.issues.IssueAttempted += HandleAttempted;
                ctx.issues.IssueResolved += HandleResolved;
            }
            if (ctx.taskPanel != null) ctx.taskPanel.Show();
        }

        public override void Exit()
        {
            if (ctx != null)
            {
                if (ctx.pointer != null) ctx.pointer.Tap -= HandleTap;
                if (ctx.issues != null)
                {
                    ctx.issues.IssueAttempted -= HandleAttempted;
                    ctx.issues.IssueResolved -= HandleResolved;
                }
                ctx.ResetShared();
            }
            base.Exit();
        }

        protected virtual void Update()
        {
            if (!IsActive || ctx == null || ctx.detailPopup == null || !ctx.detailPopup.Visible) return;
            if (Time.time < nextPopupRefresh) return;
            nextPopupRefresh = Time.time + popupRefreshInterval;
            int id = ctx.detailPopup.NodeId;
            if (id >= 0) ctx.detailPopup.SetText(PopupTitle(id), PopupBody(id));
        }

        void HandleTap(int id)
        {
            if (!IsActive) return;
            if (ctx.issues != null) ctx.issues.TryAttempt(id);
            ShowPopup(id);
            OnNodeTapped(id);
        }

        void HandleAttempted(NodeIssue issue, bool solved)
        {
            if (IsActive) OnIssueAttempted(issue, solved);
        }

        void HandleResolved(NodeIssue issue)
        {
            if (IsActive) OnIssueResolved(issue);
        }

        protected virtual void OnNodeTapped(int id) { }
        protected virtual void OnIssueAttempted(NodeIssue issue, bool solved) { }
        protected virtual void OnIssueResolved(NodeIssue issue) { }

        protected void ShowPopup(int id)
        {
            if (ctx.detailPopup == null || ctx.picker == null) return;
            nextPopupRefresh = Time.time + popupRefreshInterval;
            ctx.detailPopup.ShowAtNode(ctx.picker, id, PopupTitle(id), PopupBody(id));
        }

        protected virtual string PopupTitle(int id)
        {
            return $"任务 #{id}";
        }

        // S1 / 教学的详情：只有任务状态、进度、置信度（NodeDetailTable 的 Status 模板 + 占位符）
        protected virtual string PopupBody(int id)
        {
            var node = ctx.Node(id);
            var organ = node != null ? node.organ : Organ.Stem;
            Describe(id, out string status, out int progress, out int confidence);
            if (ctx.detailTable == null) return $"{status}\n进度 {progress}%　置信度 {confidence}%";
            return ctx.detailTable.Format(id, organ, DetailDepth.Status,
                ("id", id), ("status", status), ("progress", progress), ("confidence", confidence));
        }

        // 按问题状态生成详情里的三个数值。【占位规则】，文案和数值等策划定
        protected virtual void Describe(int id, out string status, out int progress, out int confidence)
        {
            var issue = ctx.issues != null ? ctx.issues.GetIssue(id) : null;
            // 正常节点：置信度按 id 给一个稳定的高值
            confidence = 90 + (id * 7) % 10;
            progress = 100;
            status = "正常";
            if (issue == null) return;

            if (issue.resolved)
            {
                status = "已解决";
                confidence = 88 + (id * 3) % 8;
                return;
            }
            switch (issue.difficulty)
            {
                case IssueDifficulty.Easy:
                    progress = 0;
                    confidence = 80;
                    break;
                case IssueDifficulty.Medium:
                    int total = ctx.issues.mediumClicks;
                    progress = Mathf.RoundToInt(100f * (total - issue.clicksRemaining) / Mathf.Max(1, total));
                    confidence = 55 + 10 * (total - issue.clicksRemaining);
                    break;
                default:
                    progress = 0;
                    confidence = Mathf.Max(3, 31 - 9 * issue.attempts);
                    break;
            }
            if (issue.IsWaiting)
                status = issue.difficulty == IssueDifficulty.Hard ? "处理失败 · 等待重试" : "处理中";
            else
                status = issue.attempts > 0 && issue.difficulty == IssueDifficulty.Hard ? "严重异常 · 未解决" : "异常 · 等待授权";
        }
    }
}
