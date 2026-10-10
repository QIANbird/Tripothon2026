using System.Collections;
using Ghost.Agent;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // 新手教学（docs/tasks/tutorial-single-cube.md）：黑暗中只有一个放大的方块。
    // 简单问题（点一次解决）→ 同一方块变成困难问题（点了解决不了）→ 温柔女声引导右键 →
    // 右键查看详情 → 方块后退缩小、回到矩阵槽位，其余方块渐显 → Complete() 进入 S1（同为 Matrix，不再变形）。
    // 台词来自台词表 TUT 段（DialogueCsvImporter 生成的 Script/TUT_*.asset），由 MainSceneMenu 挂上。
    public class TutorialStage : IssueStage
    {
        [Header("教学对白（台词表 TUT 段）")]
        [Tooltip("进关：看到那个在闪的方块了吗…… / 点击异常节点……")]
        public DialogueSequence introSequence;
        [Tooltip("简单问题解决后：这是你的agent……")]
        public DialogueSequence afterEasySequence;
        [Tooltip("困难问题失败后：尝试解决失败…… / ……你该主动介入一下 / 右键教学")]
        public DialogueSequence afterHardSequence;
        [Tooltip("右键详情的内容：第一句台词的文本，第一行做标题，其余做正文")]
        public DialogueSequence inspectSequence;
        [Tooltip("右键查看之后：直观、清晰、安心……")]
        public DialogueSequence afterInspectSequence;

        [Header("演示方块（由 MainSceneMenu 按 Matrix 布局填好）")]
        public int demoNode = -1;
        [Tooltip("演示方块在矩阵包围盒里的位置（0–1，x 向右、y 向上）")]
        public Vector2 demoAnchor = new Vector2(0.5f, 0.58f);
        [Tooltip("演示方块是矩阵节点的几倍大")]
        public float demoScale = 6f;
        [Tooltip("演示方块往镜头方向提出多少（植株本地空间，矩阵面朝 -Z）")]
        public float demoForward = 0.08f;

        [Header("转场：方块融入矩阵")]
        [Tooltip("方块后退、缩小回矩阵槽位的时长（秒）")]
        public float retreatDuration = 1.5f;
        [Tooltip("其余方块在这段时间内陆续显现（秒），和后退的后半段重叠")]
        public float revealDuration = 1f;

        enum Phase { Intro, Easy, AfterEasy, Hard, AfterHard, WaitInspect, AfterInspect, Transition, Done }

        Phase phase;
        Coroutine transitionRoutine;

        // 演示方块开始退回矩阵、其余方块陆续显现（音效等订阅）
        public event System.Action MergeStarted;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null) return;

            phase = Phase.Intro;
            SetupTaskPanel();
            ShowDemoOnly();
            ctx.Inspected += HandleInspected;
            ctx.Play(introSequence, BeginEasy);
        }

        public override void Exit()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
            if (ctx != null)
            {
                ctx.Inspected -= HandleInspected;
                if (ctx.morpher != null)
                {
                    ctx.morpher.ClearPoseOverride(demoNode);
                    // 开场的闪烁不是问题系统挂的，ResetShared 清不掉
                    ctx.morpher.Restore(demoNode);
                    ctx.morpher.ShowAll(true);
                }
            }
            phase = Phase.Done;
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
                new AgentTask("困难异常", TaskState.Pending),
            });
            ctx.taskPanel.SetMetric("完成率", 0f, "0 / 2");
            ctx.taskPanel.SetMetric("置信度", 0.4f);
        }

        // 只留演示方块：其余节点立即隐藏（隐藏的节点不参与拾取），演示方块放大拉到镜头前
        void ShowDemoOnly()
        {
            var morpher = ctx.morpher;
            if (morpher == null || morpher.nodeSet == null) return;
            if (morpher.nodeSet.Get(demoNode) == null)
            {
                // 旧场景里没有 demoNode（还没重新生成主场景）时，运行时按同样规则挑一个
                demoNode = ctx.NearestNodeInLayout(MorphForm.Matrix, new Vector2(0.5f, 0.55f),
                    n => n.organ != Organ.Fruit && n.organ != Organ.Bug);
                Debug.LogWarning($"[Tutorial] 场景里没有演示节点，临时使用 {demoNode}。请重新生成主场景", this);
                if (morpher.nodeSet.Get(demoNode) == null) return;
            }
            for (int i = 0; i < morpher.nodeSet.Count; i++)
                morpher.SetVisible(i, i == demoNode, true);
            morpher.SetPoseOverride(demoNode, DemoPose(), 1f);
            // 开场台词"看到那个在闪的方块了吗"时就在闪；简单问题挂上后由 NodeIssueSystem 接管闪烁
            morpher.SetBlink(demoNode);
        }

        NodePose DemoPose()
        {
            var nodes = ctx.morpher.nodeSet.nodes;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
            foreach (var n in nodes)
            {
                Vector3 p = n.GetPose(MorphForm.Matrix).position;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var slot = ctx.morpher.nodeSet.Get(demoNode).GetPose(MorphForm.Matrix);
            Vector3 pos = new Vector3(
                Mathf.Lerp(min.x, max.x, demoAnchor.x),
                Mathf.Lerp(min.y, max.y, demoAnchor.y),
                (min.z + max.z) * 0.5f - demoForward);
            return new NodePose(pos, slot.rotation, slot.scale * demoScale);
        }

        void BeginEasy()
        {
            if (!IsActive) return;
            phase = Phase.Easy;
            ctx.issues.AddIssue(demoNode, IssueDifficulty.Easy);
            SetTask(0, TaskState.Running);
        }

        protected override void OnIssueResolved(NodeIssue issue)
        {
            if (issue.nodeId != demoNode || phase != Phase.Easy) return;
            phase = Phase.AfterEasy;
            SetTask(0, TaskState.Done);
            if (ctx.taskPanel != null)
            {
                ctx.taskPanel.SetMetric("完成率", 0.5f, "1 / 2");
                ctx.taskPanel.SetMetric("置信度", 0.6f);
            }
            ctx.Play(afterEasySequence, BeginHard);
        }

        void BeginHard()
        {
            if (!IsActive) return;
            phase = Phase.Hard;
            ctx.issues.AddIssue(demoNode, IssueDifficulty.Hard);
            SetTask(1, TaskState.Running);
        }

        protected override void OnIssueAttempted(NodeIssue issue, bool solved)
        {
            // 只认第一次失败；之后再点只按困难问题的规则暂停，不重播台词
            if (issue.nodeId != demoNode || solved || phase != Phase.Hard) return;
            phase = Phase.AfterHard;
            SetTask(1, TaskState.Failed);
            ctx.Play(afterHardSequence, BeginInspect);
        }

        void BeginInspect()
        {
            if (!IsActive) return;
            phase = Phase.WaitInspect;
        }

        void HandleInspected(int id)
        {
            if (!IsActive || id != demoNode || phase != Phase.WaitInspect) return;
            phase = Phase.AfterInspect;
            ctx.Play(afterInspectSequence, BeginTransition);
        }

        // 右键教学开始后，详情显示台词表里的那段文字；之前照常显示任务状态
        protected override string PopupTitle(int id)
        {
            if (UseScriptedDetail(id)) return SplitDetail(out _);
            return base.PopupTitle(id);
        }

        protected override string PopupBody(int id)
        {
            if (UseScriptedDetail(id))
            {
                SplitDetail(out string body);
                return body;
            }
            return base.PopupBody(id);
        }

        bool UseScriptedDetail(int id)
        {
            return id == demoNode && phase >= Phase.WaitInspect && inspectSequence != null && inspectSequence.lines.Count > 0;
        }

        string SplitDetail(out string body)
        {
            string text = inspectSequence.lines[0].text ?? "";
            int nl = text.IndexOf('\n');
            if (nl < 0)
            {
                body = "";
                return text.Trim();
            }
            body = text.Substring(nl + 1).Trim();
            return text.Substring(0, nl).Trim();
        }

        void BeginTransition()
        {
            if (!IsActive) return;
            phase = Phase.Transition;
            ctx.issues.RemoveIssue(demoNode);
            if (ctx.detailPopup != null) ctx.detailPopup.Hide();
            if (ctx.taskPanel != null)
            {
                ctx.taskPanel.SetMetric("完成率", 1f, "2 / 2");
                ctx.taskPanel.SetMetric("置信度", 0.62f);
            }
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(MergeIntoMatrix());
            MergeStarted?.Invoke();
        }

        IEnumerator MergeIntoMatrix()
        {
            var morpher = ctx.morpher;
            if (morpher == null || morpher.nodeSet == null || morpher.nodeSet.Get(demoNode) == null)
            {
                transitionRoutine = null;
                Complete();
                yield break;
            }
            var demoPose = DemoPose();
            int count = morpher.nodeSet.Count;
            // 其余方块按随机顺序在后退的后半段陆续显现
            var showAt = new float[count];
            float revealStart = Mathf.Max(0f, retreatDuration - revealDuration);
            for (int i = 0; i < count; i++)
                showAt[i] = revealStart + Random.value * Mathf.Max(0.01f, revealDuration);
            var shown = new bool[count];
            shown[Mathf.Clamp(demoNode, 0, count - 1)] = true;

            float total = Mathf.Max(retreatDuration, revealStart + revealDuration);
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                float k = retreatDuration > 0f ? Mathf.Clamp01(t / retreatDuration) : 1f;
                // ease-in-out：先慢慢往后退，再落进槽位
                float eased = k * k * (3f - 2f * k);
                morpher.SetPoseOverride(demoNode, demoPose, 1f - eased);
                for (int i = 0; i < count; i++)
                {
                    if (shown[i] || t < showAt[i]) continue;
                    shown[i] = true;
                    morpher.Show(i);
                }
                yield return null;
            }

            morpher.ClearPoseOverride(demoNode);
            morpher.ShowAll();
            transitionRoutine = null;
            phase = Phase.Done;
            Complete();
        }

        void SetTask(int index, TaskState state)
        {
            if (ctx.taskPanel != null) ctx.taskPanel.UpdateTask(index, state);
        }
    }
}
