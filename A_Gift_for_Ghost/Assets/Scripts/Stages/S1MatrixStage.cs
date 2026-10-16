using System.Collections;
using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using PrimeTween;
using UnityEngine;

namespace Ghost.Stages
{
    // S1（第 4 节，形态 Matrix）：画面中随机冒出简单问题；困难问题挂在 4 个缺失节点上（叶子 / 经脉 / 果实 / 根系各一个，
    // StageContext.MissingNodes，和 S2 是同一批）。详情只显示任务状态 / 进度 / 置信度。
    // 浮出：简单问题全部解决、且这个困难节点至少尝试失败过 1 次后，右键查看一次 → 节点朝玩家方向飘出矩阵、外圈发红光，永久不退回。
    // 通关：4 个困难节点全部浮出 → 播 S1_002–004，弹出询问（S1_005），选 Yes。
    // 进 S2 时这 4 个节点不变成回路姿态，直接从浮出位置飘到 S2 的散落位置（StageContext.HandOffMissingToScatter）。
    // 对白来自台词表：进关 S1_001；左键点亮起的方块时从 S1_P* 随机池挑一句（对白空闲时才播）。
    // 详见 docs/tasks/s1-s2-missing-nodes.md
    public class S1MatrixStage : IssueStage
    {
        [Header("问题数量")]
        [Tooltip("简单问题个数（画面中陆续随机出现）")]
        public int easyCount = 6;

        [Header("简单问题陆续出现")]
        [Tooltip("进入后多久冒出第一个简单问题（秒）")]
        public float firstEasyDelay = 0.6f;
        [Tooltip("之后每冒出一个简单问题的间隔（秒）")]
        public float easySpawnInterval = 1.8f;

        [Header("困难节点浮出")]
        [Tooltip("朝玩家方向飘出的距离（植株本地空间，米；任务要求 0.10–0.15）")]
        public float floatDistance = 0.12f;
        [Tooltip("飘出用时（秒）")]
        public float floatDuration = 0.9f;
        [Tooltip("悬浮晃动的幅度（植株本地空间，米）")]
        public float hoverAmplitude = 0.004f;
        [Tooltip("悬浮晃动的频率（Hz）")]
        public float hoverFrequency = 0.5f;
        [Tooltip("浮出节点外圈的红光")]
        public Color floatGlowColor = new Color(1f, 0.2f, 0.15f, 0.95f);
        public float floatGlowIntensity = 1f;
        [Tooltip("通关进 S2 时，从浮出位置飘到散落位置的用时（秒）")]
        public float handoffDuration = 1.6f;

        [Header("随机")]
        [Tooltip("< 0 每次随机；≥ 0 固定种子，方便验收复现")]
        public int randomSeed = -1;

        [Header("对白（台词表 S1 段）")]
        public DialogueSequence introSequence;
        [Tooltip("左键点亮起的方块时随机挑一段播放；文本里的 {a-b} 换成随机数")]
        public DialogueSequence[] clickPool = new DialogueSequence[0];
        [Tooltip("通关条件满足后播放，播完弹出询问")]
        public DialogueSequence completeSequence;
        [Tooltip("询问的问题文字（台词表 Agent询问 行）；为空时用 queryQuestion")]
        public DialogueSequence querySequence;

        [Header("询问")]
        [Tooltip("querySequence 为空时用的提问")]
        public string queryQuestion = "是否要进一步查看信息？";

        // 任务面板下标：0 提高增长 / 1 解决异常 / 2 维持关系 / 3 完成本周期任务
        const int TaskGrowth = 0;
        const int TaskAnomaly = 1;
        const int TaskRelation = 2;
        const int TaskCycle = 3;

        Coroutine spawnRoutine;
        bool queryShown;
        bool handingOff;
        DialogueSequence poolRuntime; // 随机池播放用的运行时副本（文本填好随机数）
        readonly List<int> hardIds = new List<int>();
        // 已浮出的困难节点：id → 浮出进度（0–1，PrimeTween 驱动，Update 里每帧按进度叠加姿态覆盖）
        readonly Dictionary<int, float> floatProgress = new Dictionary<int, float>();
        readonly List<Tween> floatTweens = new List<Tween>();
        readonly List<int> floatKeys = new List<int>();

        // 音效等订阅：某个困难节点开始浮出
        public event System.Action<int> NodeFloated;

        public override void Enter()
        {
            base.Enter();
            if (ctx == null) return;
            queryShown = false;
            handingOff = false;
            ClearFloated();
            // 上次 S1 / S2 留下的姿态覆盖、外发光、隐藏的连线（例如从 S2 跳回 S1）
            ctx.ClearMissingNodeVisuals();
            hardIds.Clear();
            ctx.Inspected += HandleInspected;
            SetupTaskPanel();
            ctx.Play(introSequence);
            spawnRoutine = StartCoroutine(SpawnIssuesOverTime());
        }

        public override void Exit()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
            queryShown = false;
            StopFloatTweens();
            if (ctx != null)
            {
                ctx.Inspected -= HandleInspected;
                // 通关进 S2：保留姿态覆盖，交给 StageContext 从当前位置飘到散落位置；跳关 / N 键：全部清掉
                if (handingOff) ctx.HandOffMissingToScatter(handoffDuration, floatGlowColor);
                else ctx.ClearMissingNodeVisuals();
            }
            floatProgress.Clear();
            handingOff = false;
            base.Exit();
        }

        void SetupTaskPanel()
        {
            if (ctx.taskPanel == null) return;
            ctx.taskPanel.SetTitle("S1 · 植株维护");
            ctx.taskPanel.SetModeLabel("FULL PROXY");
            // 【占位】任务名等策划替换。状态随问题解决更新
            ctx.taskPanel.SetTasks(new[]
            {
                new AgentTask("提高增长", TaskState.Running),
                new AgentTask("解决异常", TaskState.Running),
                new AgentTask("维持关系", TaskState.Pending),
                new AgentTask("完成本周期任务", TaskState.Pending),
            });
            RefreshMetrics();
        }

        IEnumerator SpawnIssuesOverTime()
        {
            // 缺失节点的挑选要用连线表（NodeLinkRenderer.Start 之后才有）；Play 第一帧就进关时等它建好
            while (IsActive && ctx.links != null && !ctx.links.IsReady) yield return null;

            // 困难问题一开始就挂在 4 个缺失节点上
            SpawnHardIssues();
            RefreshAll();

            // 简单问题按间隔陆续冒出
            float start = Time.time;
            int spawnedEasy = 0;
            while (IsActive && spawnedEasy < easyCount)
            {
                if (Time.time - start >= firstEasyDelay + easySpawnInterval * spawnedEasy)
                {
                    ctx.issues.GenerateRandom(1, IssueDifficulty.Easy, EligibleForRandom, SeedFor(spawnedEasy));
                    spawnedEasy++;
                    RefreshAll();
                }
                yield return null;
            }
            spawnRoutine = null;
        }

        int SeedFor(int k) => randomSeed < 0 ? -1 : randomSeed + k;

        void RefreshAll()
        {
            RefreshTasks();
            RefreshMetrics();
        }

        void SpawnHardIssues()
        {
            hardIds.Clear();
            hardIds.AddRange(ctx.MissingNodes());
            if (hardIds.Count < MissingNodePicker.Organs.Length)
                Debug.LogWarning($"[S1] 只挑出 {hardIds.Count} 个缺失节点（应为 {MissingNodePicker.Organs.Length}），通关只要求这些全部浮出", this);
            ctx.issues.AddIssues(hardIds, IssueDifficulty.Hard);
            Debug.Log($"[S1] 困难问题挂在：{string.Join(", ", hardIds)}", this);
        }

        bool EligibleForRandom(PlantNode node)
        {
            // 缺失节点留给困难问题；已有问题的节点 GenerateRandom 自己会跳过
            return !hardIds.Contains(node.id);
        }

        protected override void OnIssueAttempted(NodeIssue issue, bool solved)
        {
            RefreshAll();
            TryOfferQuery(); // 先判断通关：通关的这一下直接播 S1_002，不先冒一句随机池
            PlayPoolLine();
        }

        protected override void OnIssueResolved(NodeIssue issue)
        {
            RefreshAll();
            TryOfferQuery();
        }

        // 简单问题已全部生成且全部解决
        bool SolvableDone()
        {
            return CountOf(IssueDifficulty.Easy) >= easyCount && ctx.issues.AllResolved(IssueDifficulty.Easy);
        }

        bool AllFloated() => hardIds.Count > 0 && floatProgress.Count >= hardIds.Count;

        // 右键查看：满足条件的困难节点浮出。只浮一次，之后再查看只显示详情；条件不满足时也只显示详情
        void HandleInspected(int id)
        {
            if (!IsActive || queryShown || floatProgress.ContainsKey(id) || !hardIds.Contains(id)) return;
            if (!SolvableDone()) return;
            var issue = ctx.issues.GetIssue(id);
            if (issue == null || issue.attempts < 1) return;
            FloatOut(id);
            RefreshAll();
            TryOfferQuery();
        }

        void FloatOut(int id)
        {
            floatProgress[id] = 0f;
            var halos = ctx.EnsureHalos();
            if (halos != null) halos.SetGlow(id, floatGlowColor, floatGlowIntensity);
            floatTweens.Add(Tween.Custom(this, 0f, 1f, Mathf.Max(0.01f, floatDuration), (self, t) =>
            {
                if (self.floatProgress.ContainsKey(id)) self.floatProgress[id] = t;
            }, Ease.OutCubic));
            NodeFloated?.Invoke(id);
            Debug.Log($"[S1] 困难节点 {id} 浮出（{floatProgress.Count} / {hardIds.Count}）", this);
        }

        // 浮出的目标姿态：Matrix 槽位朝玩家方向（ViewerPosition，植株本地空间）推出 floatDistance，再叠加轻微悬浮晃动
        NodePose FloatPose(int id)
        {
            var pose = ctx.Node(id).GetPose(MorphForm.Matrix);
            Vector3 toViewer = ctx.ViewerPositionLocal() - pose.position;
            Vector3 dir = toViewer.sqrMagnitude > 1e-8f ? toViewer.normalized : Vector3.back;
            float w = Time.time * hoverFrequency * 2f * Mathf.PI + id * 1.37f;
            var sway = new Vector3(Mathf.Sin(w), Mathf.Sin(w * 0.8f + 1.1f), 0f) * hoverAmplitude;
            pose.position += dir * floatDistance + sway;
            return pose;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsActive || ctx == null || ctx.morpher == null || floatProgress.Count == 0) return;
            floatKeys.Clear();
            floatKeys.AddRange(floatProgress.Keys);
            foreach (int id in floatKeys)
                ctx.morpher.SetPoseOverride(id, FloatPose(id), floatProgress[id]);
        }

        void StopFloatTweens()
        {
            foreach (var t in floatTweens) t.Stop();
            floatTweens.Clear();
        }

        void ClearFloated()
        {
            StopFloatTweens();
            floatProgress.Clear();
        }

        void RefreshTasks()
        {
            if (ctx.taskPanel == null || ctx.issues == null) return;
            bool easyDone = SolvableDone();
            int hardAttempts = ctx.issues.TotalAttempts(IssueDifficulty.Hard);

            // 【占位】任务和问题的对应关系等策划定
            ctx.taskPanel.UpdateTask(TaskGrowth, easyDone ? TaskState.Done : TaskState.Running);
            ctx.taskPanel.UpdateTask(TaskAnomaly, easyDone ? TaskState.Done : TaskState.Running);
            ctx.taskPanel.UpdateTask(TaskRelation, hardAttempts >= 1 ? TaskState.Failed : TaskState.Pending);
            ctx.taskPanel.UpdateTask(TaskCycle, easyDone && AllFloated() ? TaskState.Done
                : easyDone ? TaskState.Running : TaskState.Pending);
        }

        void RefreshMetrics()
        {
            if (ctx.taskPanel == null || ctx.issues == null) return;
            int solvable = easyCount;
            int solved = CountOf(IssueDifficulty.Easy) - ctx.issues.CountUnresolved(IssueDifficulty.Easy);
            float rate = solvable <= 0 ? 0f : (float)solved / solvable;
            ctx.taskPanel.SetMetric("完成率", rate, $"{solved} / {solvable}");

            int hardAttempts = ctx.issues.TotalAttempts(IssueDifficulty.Hard);
            // 【占位】置信度随简单问题解决而升、随困难尝试而降，数值等策划定
            float confidence = 0.38f + 0.5f * rate - 0.04f * Mathf.Min(hardAttempts, 5);
            ctx.taskPanel.SetMetric("置信度", Mathf.Clamp01(confidence));
        }

        int CountOf(IssueDifficulty d)
        {
            int n = 0;
            foreach (var issue in ctx.issues.Issues)
                if (issue.difficulty == d) n++;
            return n;
        }

        void TryOfferQuery()
        {
            if (queryShown || ctx.query == null || ctx.issues == null) return;
            if (!SolvableDone() || !AllFloated()) return;
            queryShown = true;
            string question = ScriptText.FirstLine(querySequence, queryQuestion);
            ctx.Play(completeSequence, () =>
            {
                if (IsActive && ctx.query != null) ctx.query.Ask(question, CompleteWithHandoff);
            });
        }

        // 选 Yes：标记交接，Exit 时 4 个浮出节点直接飘到 S2 的散落位置
        void CompleteWithHandoff()
        {
            if (!IsActive) return;
            handingOff = true;
            Complete();
            // 没被流程接走（没挂 GameFlow）时保持原样
            if (IsActive) handingOff = false;
        }

        // 点亮起的方块：Agent 弹窗随机报一条任务状态。已有对白在播（进关台词、上一条）时不打断
        void PlayPoolLine()
        {
            if (queryShown || clickPool == null || clickPool.Length == 0 || ctx.dialogue == null || ctx.dialogue.IsPlaying) return;
            var src = clickPool[Random.Range(0, clickPool.Length)];
            if (src == null || src.lines.Count == 0) return;
            if (poolRuntime == null)
            {
                poolRuntime = ScriptableObject.CreateInstance<DialogueSequence>();
                poolRuntime.hideFlags = HideFlags.DontSave;
            }
            var line = src.lines[0];
            poolRuntime.name = src.name;
            poolRuntime.lines.Clear();
            poolRuntime.lines.Add(new DialogueLine(line.speaker, ScriptText.FillRandom(line.text))
            {
                id = line.id,
                channel = line.channel,
                clip = line.clip,
                durationOverride = line.durationOverride,
                pauseAfter = line.pauseAfter,
            });
            ctx.Play(poolRuntime);
        }

        void OnDestroy()
        {
            StopFloatTweens();
            if (poolRuntime != null) Destroy(poolRuntime);
        }
    }
}
