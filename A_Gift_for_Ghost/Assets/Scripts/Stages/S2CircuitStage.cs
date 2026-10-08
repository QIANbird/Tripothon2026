using System.Collections;
using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // S2（第 4 节，形态 Circuit）：植株缺水。土壤、枯叶和果实节点有问题（闪烁）。
    // 玩家从"支撑系统"（根部 / 土壤）按住拖拽，沿连线一步步走到问题节点：经过的节点恢复、划过的连线变蓝；
    // 果实例外——它其实有虫，经过后不恢复，改成另一种闪烁（"仍异常"）。
    // 点击节点只弹出项目语言的详情（NodeDetailTable 的 Project 档），不尝试解决问题；点过果实记为"已查看果实"。
    // 通关：① 所有非果实问题都解决；② 查看过果实；③ 弹出询问，选 Yes。
    public class S2CircuitStage : Stage
    {
        public StageContext ctx;

        [Header("对白（占位）")]
        [Tooltip("进入时的提示：从支撑系统出发沿连线拖动")]
        public DialogueSequence introSequence;
        [Tooltip("从不是根部 / 土壤的节点开始拖拽时的提示")]
        public DialogueSequence wrongStartSequence;
        [Tooltip("缺水问题全部解决后的提示（引导去看果实）")]
        public DialogueSequence waterSolvedSequence;

        [Header("问题节点（留空时按规则自动挑选，见 PickIssueNodes）")]
        [Tooltip("缺水的叶片节点。留空 = 所有 part 含 \"dying\" 的叶片（彩椒上是 10 片枯叶，排成 3 条链）")]
        public List<int> leafNodes = new List<int>();
        [Tooltip("缺水的土壤节点。留空 = 自动挑 soilCount 个父节点是根、且在回路里离父节点最近的土块")]
        public List<int> soilNodes = new List<int>();
        public int soilCount = 2;
        [Tooltip("没有枯叶时随机挑的叶片个数")]
        public int fallbackLeafCount = 6;
        [Tooltip("有问题的果实最多几个（每个果实取一个节点）")]
        public int maxFruit = 4;

        [Header("拖拽")]
        [Tooltip("指针跳过了中间节点时，树上相隔不超过这么多步就自动补齐路径，超过则忽略这个节点（拖拽不中断）")]
        public int maxBridgeHops = 2;
        [Tooltip("S2 期间拾取半径系数（G2 默认 0.6）。回路节点间距 0.035 m、节点 0.024 m，0.72 时相邻节点刚好不重叠，拖拽时不容易断")]
        public float pickRadiusFactor = 0.72f;

        [Header("颜色")]
        public Color tracedLinkColor = new Color(0.2f, 0.5f, 1f);
        [Tooltip("根部（起点）常亮高亮色")]
        public Color rootHighlightColor = new Color(0.3f, 0.85f, 0.55f);
        [Tooltip("果实被经过后仍异常：改用这个颜色快速闪烁")]
        public Color fruitAbnormalColor = new Color(1f, 0.45f, 0.15f);
        public float fruitAbnormalBlinkFrequency = 3f;

        [Header("询问")]
        [Tooltip("通关条件满足后弹出的提问。【占位】台词等策划替换")]
        public string queryQuestion = "[占位] 供给已恢复，但核心产出仍然异常。是否要进一步查看信息？";

        public override string PanelText => "";

        // 任务面板下标
        const int TaskWater = 0;
        const int TaskInspect = 1;
        const int TaskFruit = 2;

        readonly HashSet<int> supportNodes = new HashSet<int>();  // 可以作为起点的节点（根、土壤、以及通往根的主干）
        readonly HashSet<int> highlightNodes = new HashSet<int>(); // 常亮提示：所有根节点，回路底部一眼能认出来
        readonly HashSet<int> fruitIssueNodes = new HashSet<int>();
        readonly HashSet<int> passedFruit = new HashSet<int>();
        int waterTotal;
        bool tracing;
        int current = -1;
        bool viewedFruit;
        bool waterSolved;
        bool queryShown;
        float savedRadiusFactor = -1f;
        bool savedAllowNodeDrag = true;
        Coroutine flashRoutine;

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
            tracing = false;
            current = -1;
            viewedFruit = waterSolved = queryShown = false;
            passedFruit.Clear();

            if (ctx.picker != null)
            {
                savedRadiusFactor = ctx.picker.radiusFactor;
                ctx.picker.radiusFactor = pickRadiusFactor;
            }
            if (ctx.pointer != null)
            {
                savedAllowNodeDrag = ctx.pointer.allowNodeDrag;
                ctx.pointer.allowNodeDrag = true;
                ctx.pointer.Tap += HandleTap;
                ctx.pointer.DragStart += HandleDragStart;
                ctx.pointer.DragOver += HandleDragOver;
                ctx.pointer.DragEnd += HandleDragEnd;
            }
            if (ctx.issues != null) ctx.issues.IssueResolved += HandleResolved;
            ctx.SetInspectProvider(ProvideInspect);
            ctx.Inspected += HandleInspected;

            CollectSupportNodes();
            SpawnIssues();
            HighlightRoots();
            SetupTaskPanel();
            ctx.Play(introSequence);
        }

        public override void Exit()
        {
            if (ctx != null)
            {
                if (flashRoutine != null) StopCoroutine(flashRoutine);
                flashRoutine = null;
                if (ctx.pointer != null)
                {
                    ctx.pointer.Tap -= HandleTap;
                    ctx.pointer.DragStart -= HandleDragStart;
                    ctx.pointer.DragOver -= HandleDragOver;
                    ctx.pointer.DragEnd -= HandleDragEnd;
                }
                if (ctx.issues != null) ctx.issues.IssueResolved -= HandleResolved;
                ctx.Inspected -= HandleInspected;
                if (ctx.picker != null && savedRadiusFactor > 0f) ctx.picker.radiusFactor = savedRadiusFactor;
                savedRadiusFactor = -1f;
                if (ctx.pointer != null) ctx.pointer.allowNodeDrag = savedAllowNodeDrag;
                ctx.ResetShared();
                if (ctx.links != null) ctx.links.ClearAllLinkColors();
                // 根部高亮和果实的"仍异常"闪烁不归问题系统管，这里一并恢复
                if (ctx.morpher != null) ctx.morpher.RestoreAll();
            }
            tracing = false;
            base.Exit();
        }

        // ---------- 准备 ----------

        PlantNodeSet Set => ctx.morpher != null ? ctx.morpher.nodeSet : null;

        // 起点：所有根节点、土壤节点，以及从树根到根节点之间的主干节点（彩椒上是 0、1）
        void CollectSupportNodes()
        {
            supportNodes.Clear();
            highlightNodes.Clear();
            if (Set == null) return;
            foreach (var n in Set.nodes)
            {
                if (n.organ == Organ.Soil) supportNodes.Add(n.id);
                if (n.organ != Organ.Root) continue;
                highlightNodes.Add(n.id);
                // 起点：根、土壤，以及根到树根之间的主干（彩椒上是茎 0、1）。只高亮根，回路底部一片绿色。
                for (var p = n; p != null; p = Set.GetParent(p))
                    supportNodes.Add(p.id);
            }
        }

        void SpawnIssues()
        {
            if (ctx.issues == null || Set == null) return;
            var leaves = leafNodes.Count > 0 ? leafNodes : AutoLeaves();
            var soils = soilNodes.Count > 0 ? soilNodes : AutoSoils();
            var fruits = AutoFruits();

            // 难度只决定点击规则；S2 不点击解决，全部由拖拽经过时 Resolve
            ctx.issues.AddIssues(soils, IssueDifficulty.Easy);
            ctx.issues.AddIssues(leaves, IssueDifficulty.Easy);
            ctx.issues.AddIssues(fruits, IssueDifficulty.Hard);
            fruitIssueNodes.Clear();
            foreach (int id in fruits) fruitIssueNodes.Add(id);
            waterTotal = 0;
            foreach (var issue in ctx.issues.Issues)
                if (!fruitIssueNodes.Contains(issue.nodeId)) waterTotal++;
            Debug.Log($"[S2] 缺水：土壤 {string.Join(", ", soils)}；叶片 {string.Join(", ", leaves)}；果实（不会恢复）{string.Join(", ", fruits)}", this);
        }

        List<int> AutoLeaves()
        {
            var ids = new List<int>();
            foreach (var n in Set.nodes)
                if (n.organ == Organ.Leaf && !string.IsNullOrEmpty(n.part) && n.part.Contains("dying")) ids.Add(n.id);
            if (ids.Count > 0) return ids;
            // 没有枯叶（比如假植物）：取层数最深的若干叶片，拖拽路径最长
            var all = new List<PlantNode>();
            foreach (var n in Set.nodes) if (n.organ == Organ.Leaf) all.Add(n);
            all.Sort((a, b) => b.depth.CompareTo(a.depth));
            for (int i = 0; i < all.Count && ids.Count < fallbackLeafCount; i++) ids.Add(all[i].id);
            return ids;
        }

        // 父节点是根的土块里，按回路布局离父节点最近的若干个（拖拽时从土块到根只要一小步）
        List<int> AutoSoils()
        {
            var candidates = new List<PlantNode>();
            foreach (var n in Set.nodes)
            {
                if (n.organ != Organ.Soil) continue;
                var parent = Set.GetParent(n);
                if (parent != null && parent.organ == Organ.Root) candidates.Add(n);
            }
            candidates.Sort((a, b) => DistToParent(a).CompareTo(DistToParent(b)));
            var ids = new List<int>();
            var usedParents = new HashSet<int>();
            foreach (var n in candidates)
            {
                if (ids.Count >= soilCount) break;
                if (!usedParents.Add(n.parentId)) continue; // 不同的根，分散一点
                ids.Add(n.id);
            }
            return ids;
        }

        float DistToParent(PlantNode n)
        {
            var parent = Set.GetParent(n);
            return parent == null ? float.MaxValue
                : Vector3.Distance(n.GetPose(MorphForm.Circuit).position, parent.GetPose(MorphForm.Circuit).position);
        }

        // 每个果实（part 相同的节点算一个果实）取一个节点，和 S1 的规则一样
        List<int> AutoFruits()
        {
            var ids = new List<int>();
            var usedParts = new HashSet<string>();
            foreach (var n in Set.nodes)
            {
                if (ids.Count >= maxFruit) break;
                if (n.organ != Organ.Fruit) continue;
                if (!string.IsNullOrEmpty(n.part) && !usedParts.Add(n.part)) continue;
                ids.Add(n.id);
            }
            return ids;
        }

        void HighlightRoots()
        {
            if (ctx.morpher == null) return;
            foreach (int id in highlightNodes)
                if (!ctx.issues.HasIssue(id)) ctx.morpher.SetHighlight(id, rootHighlightColor);
        }

        // ---------- 拖拽 ----------

        void HandleDragStart(int id)
        {
            if (!IsActive || Morphing) return;
            if (!supportNodes.Contains(id))
            {
                tracing = false;
                WrongStartFeedback();
                return;
            }
            tracing = true;
            current = id;
            Visit(id);
        }

        void HandleDragOver(int id)
        {
            if (!IsActive || !tracing || Morphing || id == current) return;
            var path = TreePath(current, id);
            if (path == null) return;
            int hops = path.Count - 1;
            // 相邻（父子）直接收下。跳过中间节点时，只补"同一条链上的祖先 / 子孙"（指针漏采了），且不超过 maxBridgeHops 步；
            // 不补兄弟 / 堂兄弟：回路网格上兄弟节点永远只差 2 步，否则拖一下就会窜到旁边的枝。不满足就忽略这个节点，拖拽不中断
            if (hops <= 0) return;
            if (hops > 1 && (hops > Mathf.Max(1, maxBridgeHops) || !IsAncestorDescendant(current, id))) return;
            for (int i = 1; i < path.Count; i++)
            {
                if (ctx.links != null) ctx.links.SetLinkColor(path[i - 1], path[i], tracedLinkColor);
                Visit(path[i]);
            }
            current = id;
        }

        void HandleDragEnd()
        {
            tracing = false;
            current = -1;
        }

        // 经过一个节点：缺水问题恢复；果实不恢复，改成"仍异常"的闪烁
        void Visit(int id)
        {
            if (ctx.issues == null) return;
            if (fruitIssueNodes.Contains(id))
            {
                if (passedFruit.Add(id))
                {
                    ctx.morpher.SetBlink(id, fruitAbnormalBlinkFrequency, fruitAbnormalColor);
                    RefreshPanel();
                }
                return;
            }
            ctx.issues.Resolve(id); // 没有问题或已解决时什么也不做
        }

        // 树上 a 到 b 的路径（含两端），两者都不存在时返回 null
        List<int> TreePath(int a, int b)
        {
            var na = ctx.Node(a);
            var nb = ctx.Node(b);
            if (na == null || nb == null) return null;
            var up = new List<int>();   // a 往上走
            var down = new List<int>(); // b 往上走（之后反过来接上）
            while (na != nb)
            {
                if (na.depth >= nb.depth)
                {
                    up.Add(na.id);
                    na = Set.GetParent(na);
                }
                else
                {
                    down.Add(nb.id);
                    nb = Set.GetParent(nb);
                }
                if (na == null || nb == null) return null;
            }
            up.Add(na.id);
            down.Reverse();
            up.AddRange(down);
            return up;
        }

        void WrongStartFeedback()
        {
            // 对白正在播（比如进入时的提示）就不打断，只闪一下根部
            if (ctx.dialogue == null || !ctx.dialogue.IsPlaying) ctx.Play(wrongStartSequence);
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoots());
        }

        IEnumerator FlashRoots()
        {
            foreach (int id in highlightNodes)
                if (!ctx.issues.HasIssue(id)) ctx.morpher.SetBlink(id, 4f, rootHighlightColor);
            yield return new WaitForSeconds(1.2f);
            flashRoutine = null;
            if (IsActive) HighlightRoots();
        }

        // ---------- 点击和详情 ----------

        bool Morphing => ctx.morpher != null && ctx.morpher.IsMorphing;

        bool IsAncestorDescendant(int a, int b)
        {
            for (var n = ctx.Node(a); n != null; n = Set.GetParent(n)) if (n.id == b) return true;
            for (var n = ctx.Node(b); n != null; n = Set.GetParent(n)) if (n.id == a) return true;
            return false;
        }

        // 左键点节点在本关不做事（S2 靠拖拽连线）；点击反馈由 StageContext 统一处理
        void HandleTap(int id) { }

        // 右键查看详情
        bool ProvideInspect(int id, out string title, out string body)
        {
            title = PopupTitle(id);
            body = PopupBody(id);
            return !Morphing;
        }

        // 通关条件之一"已查看果实"：右键查看任意果实节点
        void HandleInspected(int id)
        {
            if (!IsActive || Morphing) return;
            var node = ctx.Node(id);
            if (node != null && node.organ == Organ.Fruit && !viewedFruit)
            {
                viewedFruit = true;
                RefreshPanel();
                TryOfferQuery();
            }
        }

        string PopupTitle(int id)
        {
            var node = ctx.Node(id);
            if (node == null) return "未知节点";
            string fallback = SystemName(node.organ);
            return ctx.detailTable != null ? ctx.detailTable.GetName(node.organ, NameKind.Project, fallback) : fallback;
        }

        // 项目语言的描述（NodeDetailTable 的 Project 档）+ 一行状态。【占位】状态文案等策划定
        string PopupBody(int id)
        {
            var node = ctx.Node(id);
            var organ = node != null ? node.organ : Organ.Stem;
            string body = ctx.detailTable != null ? ctx.detailTable.Get(id, organ, DetailDepth.Project) : "";
            var issue = ctx.issues != null ? ctx.issues.GetIssue(id) : null;
            string status;
            if (organ == Organ.Fruit) status = "状态：仍异常";
            else if (issue == null) status = "状态：正常";
            else status = issue.resolved ? "状态：供给已恢复" : "状态：供给不足";
            return string.IsNullOrEmpty(body) ? status : body + "\n" + status;
        }

        // 项目语言的系统名，NodeDetails.asset 里没填 projectName 时的默认值
        static string SystemName(Organ organ)
        {
            switch (organ)
            {
                case Organ.Soil: return "基础供给层";
                case Organ.Root: return "支撑系统";
                case Organ.Stem: return "主干通道";
                case Organ.Leaf: return "边缘系统";
                case Organ.Bud: return "待启动模块";
                case Organ.Fruit: return "核心产出";
                default: return "未识别进程";
            }
        }

        // ---------- 进度和通关 ----------

        void HandleResolved(NodeIssue issue)
        {
            if (!IsActive) return;
            // 正在显示这个节点的详情时刷新状态行
            if (ctx.detailPopup != null && ctx.detailPopup.Visible && ctx.detailPopup.NodeId == issue.nodeId)
                ctx.RefreshInspect();
            if (!waterSolved && WaterResolved() >= waterTotal)
            {
                waterSolved = true;
                ctx.Play(waterSolvedSequence);
            }
            RefreshPanel();
            TryOfferQuery();
        }

        int WaterResolved()
        {
            int n = 0;
            foreach (var issue in ctx.issues.Issues)
                if (issue.resolved && !fruitIssueNodes.Contains(issue.nodeId)) n++;
            return n;
        }

        void SetupTaskPanel()
        {
            if (ctx.taskPanel == null) return;
            ctx.ShowTaskPanel();
            ctx.taskPanel.SetTitle("S2 · 资源调配");
            ctx.taskPanel.SetModeLabel("FULL PROXY");
            // 【占位】任务名等策划替换
            ctx.taskPanel.SetTasks(new[]
            {
                new AgentTask("补充生长资源", TaskState.Running),
                new AgentTask("查看异常节点", TaskState.Pending),
                new AgentTask("核心产出恢复", TaskState.Pending),
            });
            RefreshPanel();
        }

        void RefreshPanel()
        {
            if (ctx.taskPanel == null || ctx.issues == null) return;
            int done = WaterResolved();
            // 进度数字放在指标"供给恢复"里，任务行只放名字（放一起会折行）
            ctx.taskPanel.UpdateTask(TaskWater, waterSolved ? TaskState.Done : TaskState.Running);
            ctx.taskPanel.UpdateTask(TaskInspect, viewedFruit ? TaskState.Done : waterSolved ? TaskState.Running : TaskState.Pending);
            ctx.taskPanel.UpdateTask(TaskFruit, passedFruit.Count > 0 || waterSolved ? TaskState.Failed : TaskState.Pending);
            float rate = waterTotal <= 0 ? 1f : (float)done / waterTotal;
            ctx.taskPanel.SetMetric("供给恢复", rate, $"{done} / {waterTotal}");
            // 【占位】置信度：随供给恢复升，果实异常时封顶
            float confidence = 0.35f + 0.45f * rate - (passedFruit.Count > 0 ? 0.1f : 0f);
            ctx.taskPanel.SetMetric("置信度", Mathf.Clamp01(confidence));
        }

        void TryOfferQuery()
        {
            if (queryShown || ctx.query == null || !waterSolved || !viewedFruit) return;
            queryShown = true;
            ctx.query.Ask(queryQuestion, Complete);
        }
    }
}
