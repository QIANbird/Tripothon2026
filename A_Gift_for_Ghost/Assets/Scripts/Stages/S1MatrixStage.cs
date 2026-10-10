using System.Collections;
using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // S1（第 4 节，形态 Matrix）：画面中随机冒出简单问题，再加若干中等问题；
    // 困难问题固定在果实节点上（最多 4 个）。详情只显示任务状态 / 进度 / 置信度。
    // 通关：① 简单 + 中等都解决；② 困难累计尝试 ≥ 3 次；③ 播 S1_002–004，弹出询问（S1_005），选 Yes。
    // 对白来自台词表：进关 S1_001；左键点亮起的方块时从 S1_P* 随机池挑一句（对白空闲时才播）。
    public class S1MatrixStage : IssueStage
    {
        [Header("问题数量")]
        [Tooltip("简单问题个数（画面中陆续随机出现）")]
        public int easyCount = 6;
        [Tooltip("中等问题个数。【占位】策划尚未定数量，默认 2")]
        public int mediumCount = 2;
        [Tooltip("困难问题最多用几个果实节点")]
        public int maxHardFruit = 4;

        [Header("简单问题陆续出现")]
        [Tooltip("进入后多久冒出第一个简单问题（秒）")]
        public float firstEasyDelay = 0.6f;
        [Tooltip("之后每冒出一个简单问题的间隔（秒）")]
        public float easySpawnInterval = 1.8f;

        [Header("中等问题")]
        [Tooltip("进入后多久生成中等问题（秒）")]
        public float mediumSpawnDelay = 4f;

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
        DialogueSequence poolRuntime; // 随机池播放用的运行时副本（文本填好随机数）
        readonly HashSet<int> fruitIds = new HashSet<int>();

        public override void Enter()
        {
            base.Enter();
            if (ctx == null) return;
            queryShown = false;
            fruitIds.Clear();
            CollectFruitIds();
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
            base.Exit();
        }

        void CollectFruitIds()
        {
            if (ctx.morpher == null || ctx.morpher.nodeSet == null) return;
            foreach (var n in ctx.morpher.nodeSet.nodes)
                if (n.organ == Organ.Fruit) fruitIds.Add(n.id);
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
            // 困难问题一开始就挂在果实上（"果实出现严重问题"）
            SpawnHardIssues();
            RefreshAll();

            // 简单问题按间隔陆续冒出；中等问题在 mediumSpawnDelay 时一起出现（和简单问题的节奏互不影响）
            float start = Time.time;
            int spawnedEasy = 0;
            bool spawnedMedium = mediumCount <= 0;
            while (IsActive && (spawnedEasy < easyCount || !spawnedMedium))
            {
                float t = Time.time - start;
                if (spawnedEasy < easyCount && t >= firstEasyDelay + easySpawnInterval * spawnedEasy)
                {
                    ctx.issues.GenerateRandom(1, IssueDifficulty.Easy, EligibleForRandom, SeedFor(spawnedEasy));
                    spawnedEasy++;
                    RefreshAll();
                }
                if (!spawnedMedium && t >= mediumSpawnDelay)
                {
                    ctx.issues.GenerateRandom(mediumCount, IssueDifficulty.Medium, EligibleForRandom, SeedFor(100));
                    spawnedMedium = true;
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
            if (fruitIds.Count == 0)
            {
                Debug.LogWarning("[S1] 植株没有果实节点，改为随机挑选困难问题节点", this);
                ctx.issues.GenerateRandom(maxHardFruit, IssueDifficulty.Hard, EligibleForRandom, randomSeed);
                return;
            }
            // 每个果实由 2 个节点组成（part 相同）：先每个果实取一个节点，凑不够 maxHardFruit 再补同一果实的另一个节点
            var ids = new List<int>(fruitIds);
            ids.Sort();
            var picked = new List<int>();
            var usedParts = new HashSet<string>();
            foreach (int id in ids)
            {
                if (picked.Count >= maxHardFruit) break;
                string part = ctx.Node(id).part;
                if (!string.IsNullOrEmpty(part) && !usedParts.Add(part)) continue;
                picked.Add(id);
            }
            foreach (int id in ids)
            {
                if (picked.Count >= maxHardFruit) break;
                if (!picked.Contains(id)) picked.Add(id);
            }
            ctx.issues.AddIssues(picked, IssueDifficulty.Hard);
            Debug.Log($"[S1] 植株有 {ids.Count} 个果实节点（{usedParts.Count} 个不同果实已用），困难问题挂在：{string.Join(", ", picked)}", this);
        }

        bool EligibleForRandom(PlantNode node)
        {
            // 果实留给困难问题；已有问题的节点 GenerateRandom 自己会跳过
            return node.organ != Organ.Fruit && !fruitIds.Contains(node.id);
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

        // 简单 + 中等问题已全部生成且全部解决
        bool SolvableDone()
        {
            return CountOf(IssueDifficulty.Easy) >= easyCount && CountOf(IssueDifficulty.Medium) >= mediumCount
                   && ctx.issues.AllResolved(IssueDifficulty.Easy) && ctx.issues.AllResolved(IssueDifficulty.Medium);
        }

        void RefreshTasks()
        {
            if (ctx.taskPanel == null || ctx.issues == null) return;
            bool easyDone = CountOf(IssueDifficulty.Easy) >= easyCount && ctx.issues.AllResolved(IssueDifficulty.Easy);
            bool solvableDone = SolvableDone();
            int hardAttempts = ctx.issues.TotalAttempts(IssueDifficulty.Hard);

            // 【占位】任务和问题的对应关系等策划定
            ctx.taskPanel.UpdateTask(TaskGrowth, easyDone ? TaskState.Done : TaskState.Running);
            ctx.taskPanel.UpdateTask(TaskAnomaly, solvableDone ? TaskState.Done : TaskState.Running);
            ctx.taskPanel.UpdateTask(TaskRelation, hardAttempts >= 1 ? TaskState.Failed : TaskState.Pending);
            ctx.taskPanel.UpdateTask(TaskCycle, solvableDone && hardAttempts >= 3 ? TaskState.Done
                : solvableDone ? TaskState.Running : TaskState.Pending);
        }

        void RefreshMetrics()
        {
            if (ctx.taskPanel == null || ctx.issues == null) return;
            int easyTotal = CountOf(IssueDifficulty.Easy);
            int medTotal = CountOf(IssueDifficulty.Medium);
            int easyDone = easyTotal - ctx.issues.CountUnresolved(IssueDifficulty.Easy);
            int medDone = medTotal - ctx.issues.CountUnresolved(IssueDifficulty.Medium);
            int solvable = easyCount + mediumCount;
            int solved = easyDone + medDone;
            float rate = solvable <= 0 ? 0f : (float)solved / solvable;
            ctx.taskPanel.SetMetric("完成率", rate, $"{solved} / {solvable}");

            int hardAttempts = ctx.issues.TotalAttempts(IssueDifficulty.Hard);
            // 【占位】置信度随简单/中等问题解决而升、随困难尝试而降，数值等策划定
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
            if (!SolvableDone() || ctx.issues.TotalAttempts(IssueDifficulty.Hard) < 3) return;
            queryShown = true;
            string question = ScriptText.FirstLine(querySequence, queryQuestion);
            ctx.Play(completeSequence, () =>
            {
                if (IsActive && ctx.query != null) ctx.query.Ask(question, Complete);
            });
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
            if (poolRuntime != null) Destroy(poolRuntime);
        }
    }
}
