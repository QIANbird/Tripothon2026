using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // S3（第 4 节，形态 Network）：节点开始显示真实颜色，玩家拖拽空白处旋转结构，靠颜色认出所有虫子节点。
    // 点任意节点弹出物体描述（NodeDetailTable 的 Physical 档）；点到虫子 = 识别，节点保持高亮。
    // 对白来自台词表 S3 段：进关 S3_001–005，第一次标记 S3_006，全部标记 S3_007–010。
    // 通关：① 所有虫子节点都点过；② 弹出询问（S3_011 的全文），选 Yes → 进入 S4（同一帧变形到 Geometric）。
    public class S3NetworkStage : Stage
    {
        public StageContext ctx;
        [Tooltip("旋转植株的组件（S4 复用同一个）")]
        public TargetRotator rotator;

        [Header("对白（台词表 S3 段）")]
        [Tooltip("进入时的提示：颜色恢复、拖动空白处旋转")]
        public DialogueSequence introSequence;
        [Tooltip("标记第一个虫子时播放（全部标记时不播）")]
        public DialogueSequence firstMarkedSequence;
        [Tooltip("全部虫子识别后播放，播完弹出询问")]
        public DialogueSequence allFoundSequence;
        [Tooltip("询问的问题文字（台词表 Agent询问 行）；为空时用 queryQuestion")]
        public DialogueSequence querySequence;

        [Header("颜色")]
        [Tooltip("S3 期间整体写实度（Network 形态本身是 0 = 灰阶）。1 = 完全用部位真实颜色")]
        [Range(0f, 1f)] public float networkRealness = 0.9f;
        [Tooltip("虫子节点的颜色：和叶片同是绿色系，但更黄更亮，仔细看才分得出")]
        public Color bugColor = BugStageUtil.DefaultBugColor;
        [Tooltip("识别出的虫子保持的高亮色")]
        public Color foundColor = new Color(1f, 0.92f, 0.45f);

        [Header("询问")]
        [Tooltip("querySequence 为空时用的提问")]
        public string queryQuestion = "除虫是精细操作，机械臂摘除有伤害植物的风险，是否要进一步手动介入？";

        public override string PanelText => "";

        public int BugTotal => bugNodes.Count;
        public int BugFound => found.Count;
        public IReadOnlyList<int> BugNodes => bugNodes;

        readonly List<int> bugNodes = new List<int>();
        readonly HashSet<int> found = new HashSet<int>();
        bool queryShown;
        bool savedAllowEmptyDrag = true;
        bool savedAllowNodeDrag = true;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null)
            {
                Debug.LogError($"[Stage] {stageName} 缺少 StageContext", this);
                return;
            }
            ctx.ResetShared();
            if (ctx.links != null) ctx.links.ClearAllLinkColors();
            found.Clear();
            queryShown = false;

            if (ctx.pointer != null)
            {
                savedAllowEmptyDrag = ctx.pointer.allowEmptyDrag;
                savedAllowNodeDrag = ctx.pointer.allowNodeDrag;
                ctx.pointer.allowEmptyDrag = true;
                // 网络里节点很密，从节点上按下拖动也要能转（TargetRotator.rotateOnNodeDrag），所以打开节点拖拽，本关不订阅 DragOver
                ctx.pointer.allowNodeDrag = true;
                ctx.pointer.Tap += HandleTap;
            }
            if (rotator != null) rotator.Enable();

            CollectBugs();
            ApplyColors();
            ctx.SetInspectProvider(ProvideInspect);
            SetupTaskPanel();
            ctx.Play(introSequence);
        }

        public override void Exit()
        {
            if (ctx != null)
            {
                if (ctx.pointer != null)
                {
                    ctx.pointer.Tap -= HandleTap;
                    ctx.pointer.allowEmptyDrag = savedAllowEmptyDrag;
                    ctx.pointer.allowNodeDrag = savedAllowNodeDrag;
                }
                ctx.ResetShared();
                if (ctx.morpher != null)
                {
                    ctx.morpher.RestoreAll();
                    ctx.morpher.ClearRealness();
                }
            }
            // 复位策略：离开 S3 时平滑转回正面（和变形同时进行），S4 从正面开始
            if (rotator != null)
            {
                rotator.Disable();
                rotator.ResetRotation(true);
            }
            base.Exit();
        }

        PlantNodeSet Set => ctx.morpher != null ? ctx.morpher.nodeSet : null;

        void CollectBugs()
        {
            bugNodes.Clear();
            bugNodes.AddRange(BugStageUtil.CollectBugs(Set, "S3", this));
        }

        // 节点显示真实颜色：整体写实度覆盖（G3 Realness，平滑过渡）。虫子再单独染成和叶片相近的黄绿色
        void ApplyColors()
        {
            if (ctx.morpher == null) return;
            ctx.morpher.Realness = networkRealness;
            foreach (int id in bugNodes) ctx.morpher.SetTint(id, bugColor);
        }

        void HandleTap(int id)
        {
            if (!IsActive || ctx.morpher == null || ctx.morpher.IsMorphing) return;
            var node = ctx.Node(id);
            if (node == null) return;
            if (node.organ == Organ.Bug && found.Add(id))
            {
                ctx.morpher.SetHighlight(id, foundColor);
                RefreshPanel();
                if (found.Count == 1 && found.Count < bugNodes.Count) ctx.Play(firstMarkedSequence);
            }
            ctx.RefreshInspect();
            TryFinish();
        }

        // 右键查看详情（左键仍然是标记虫子）
        bool ProvideInspect(int id, out string title, out string body)
        {
            var node = ctx.Node(id);
            title = body = null;
            if (node == null || (ctx.morpher != null && ctx.morpher.IsMorphing)) return false;
            title = PopupTitle(node);
            body = PopupBody(node);
            return true;
        }

        string PopupTitle(PlantNode node) => BugStageUtil.Title(ctx.detailTable, node, NameKind.Physical);

        string PopupBody(PlantNode node)
        {
            string mark = node.organ == Organ.Bug ? $"已标记 {found.Count} / {bugNodes.Count}" : null;
            return BugStageUtil.PhysicalBody(ctx.detailTable, node, mark);
        }

        void SetupTaskPanel()
        {
            if (ctx.taskPanel == null) return;
            ctx.ShowTaskPanel();
            ctx.taskPanel.SetTitle("S3 · 异常识别");
            ctx.taskPanel.SetModeLabel("FULL PROXY");
            // 【占位】任务名等策划替换
            ctx.taskPanel.SetTasks(new[]
            {
                new AgentTask("识别异常个体", TaskState.Running),
                new AgentTask("制定处理方案", TaskState.Pending),
            });
            RefreshPanel();
        }

        void RefreshPanel()
        {
            if (ctx.taskPanel == null) return;
            bool all = bugNodes.Count > 0 && found.Count >= bugNodes.Count;
            ctx.taskPanel.UpdateTask(0, all ? TaskState.Done : TaskState.Running);
            ctx.taskPanel.UpdateTask(1, all ? TaskState.Running : TaskState.Pending);
            float rate = bugNodes.Count == 0 ? 0f : (float)found.Count / bugNodes.Count;
            ctx.taskPanel.SetMetric("已识别", rate, $"{found.Count} / {bugNodes.Count}");
        }

        void TryFinish()
        {
            if (queryShown || bugNodes.Count == 0 || found.Count < bugNodes.Count) return;
            queryShown = true;
            string question = ScriptText.FirstLine(querySequence, queryQuestion);
            ctx.Play(allFoundSequence, () =>
            {
                if (IsActive && ctx.query != null) ctx.query.Ask(question, Complete);
            });
        }
    }
}
