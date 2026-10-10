using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // S2（第 4 节，形态 Circuit）：植株缺水。土壤和枯叶节点缺水（闪烁），果实有问题但不在供给线路上。
    // 脉冲引路：当前脉冲源（蓝色闪烁 + 外圈呼吸发光）沿一条固定线路向前发脉冲：段内节点的外发光圈依次亮起、慢慢衰减，
    // 前一个还没灭后一个就亮，看得出方向（最多 segmentLength 个节点）。
    // 按住脉冲源拖拽时，从最后连上的节点拉出一条蓝线跟着指针，玩家知道可以斜着拖，也看得见拖过了哪些节点。
    // 连线进关时是回路形态的直角折线；每结束一次拖拽，按已连通的线路比例往两点直连过渡，全部连通时变成树形直线（S3 的变形从这里开始）。
    // 玩家从脉冲源按住，沿脉冲方向拖过这一段：划过的连线变蓝，经过的缺水节点恢复。
    // 拖到段终点后，终点成为新的脉冲源，继续指向下一个问题节点（同一次拖拽可以接着拖）；
    // 中途松开不回退，从已连通的节点重新计算下一段。下一段总是指向离已连通部分最近的缺水节点，玩家不用选方向。
    // 自动挑选果实时跳过供给线路经过的节点，保证线路上没有"无法解决"的节点。
    // 右键节点弹出项目语言的详情（NodeDetailTable 的 Project 档）；右键看过果实记为"已查看果实"。
    // 通关：① 所有缺水问题都解决（播 S2_006–007）；② 右键查看过果实；两者都满足、且 S2_006–007 播完后播 S2_008，播完直接进入 S3。
    public class S2CircuitStage : Stage
    {
        public StageContext ctx;

        [Header("对白（台词表 S2 段）")]
        [Tooltip("进入时的提示")]
        public DialogueSequence introSequence;
        [Tooltip("从不是脉冲源的节点开始拖拽、或点击非脉冲源节点时的提示")]
        public DialogueSequence wrongStartSequence;
        [Tooltip("缺水问题全部解决后的提示（引导去看果实）")]
        public DialogueSequence waterSolvedSequence;
        [Tooltip("缺水解决、且看过果实后播放，播完进入下一关")]
        public DialogueSequence finishSequence;

        [Header("问题节点（留空时按规则自动挑选，见 SpawnIssues）")]
        [Tooltip("缺水的叶片节点。留空 = 所有 part 含 \"dying\" 的叶片（彩椒上是 10 片枯叶，排成 3 条链）")]
        public List<int> leafNodes = new List<int>();
        [Tooltip("缺水的土壤节点。留空 = 自动挑 soilCount 个父节点是根、且在回路里离父节点最近的土块")]
        public List<int> soilNodes = new List<int>();
        public int soilCount = 2;
        [Tooltip("没有枯叶时随机挑的叶片个数")]
        public int fallbackLeafCount = 6;
        [Tooltip("有问题的果实最多几个（每个果实取一个节点，跳过供给线路经过的节点）")]
        public int maxFruit = 4;

        [Header("脉冲线路")]
        [Tooltip("每段脉冲包含的节点数（含脉冲源）")]
        public int segmentLength = 5;
        [Tooltip("指针跳过了段内的中间节点时，最多自动补齐这么多步，超过则忽略这个节点（拖拽不中断）")]
        public int maxBridgeHops = 2;
        [Tooltip("S2 期间拾取半径系数（G2 默认 0.6）。回路节点间距 0.035 m、节点 0.024 m，0.72 时相邻节点刚好不重叠，拖拽时不容易断")]
        public float pickRadiusFactor = 0.72f;
        [Tooltip("脉冲从一个节点走到下一个节点的间隔（秒）")]
        public float pulseStep = 0.3f;
        [Tooltip("一轮脉冲走完后停顿多久再从脉冲源重新发出（秒）")]
        public float pulsePause = 0.6f;
        [Tooltip("脉冲发光的半衰期（秒）。等于 pulseStep 时，下一个节点亮起时上一个还剩 50%，再下一个时剩 25%")]
        public float pulseHalfLife = 0.3f;
        [Tooltip("进关时连线的直角程度：1 = 回路形态默认的直角折线，0 = 两点直连（树形）。之后随连通比例降到 0")]
        [Range(0f, 1f)] public float linkElbow = 1f;

        [Header("颜色")]
        public Color tracedLinkColor = new Color(0.2f, 0.5f, 1f);
        [Tooltip("脉冲源本身的闪烁色（和问题节点的白色闪烁区分开）")]
        public Color sourceColor = new Color(0.3f, 0.65f, 1f);
        public float sourceBlinkFrequency = 1.2f;
        [Tooltip("脉冲源外圈的呼吸发光")]
        public Color sourceGlowColor = new Color(0.35f, 0.7f, 1f, 0.9f);
        [Tooltip("已连通节点的常驻颜色")]
        public Color connectedColor = new Color(0.35f, 0.6f, 1f);
        [Tooltip("提示玩家从脉冲源开始时，脉冲源快速闪烁的频率和时长")]
        public float hintBlinkFrequency = 5f;
        public float hintDuration = 1.2f;
        [Tooltip("脉冲外发光的颜色")]
        public Color pulseColor = new Color(0.35f, 0.7f, 1f, 0.85f);
        [Tooltip("果实被经过后仍异常：改用这个颜色快速闪烁（线路上正常不会经过果实）")]
        public Color fruitAbnormalColor = new Color(1f, 0.45f, 0.15f);
        public float fruitAbnormalBlinkFrequency = 3f;

        [Header("拖拽线")]
        [Tooltip("拖拽时跟着指针的线的颜色")]
        public Color dragLineColor = new Color(0.3f, 0.6f, 1f, 0.9f);
        [Tooltip("拖拽线宽度（植株本地空间，米；回路节点边长 0.024）")]
        public float dragLineWidth = 0.004f;
        [Tooltip("拖拽线往相机方向抬起的距离（植株本地空间，米），避免被方块挡住")]
        public float dragLineLift = 0.006f;

        public override string PanelText => "";

        // 任务面板下标
        const int TaskWater = 0;
        const int TaskInspect = 1;
        const int TaskFruit = 2;

        readonly HashSet<int> connected = new HashSet<int>();      // 已连通的节点（从树根出发）
        readonly List<int> segment = new List<int>();              // 当前一段：segment[0] 是脉冲源
        readonly HashSet<int> fruitIssueNodes = new HashSet<int>();
        readonly HashSet<int> passedFruit = new HashSet<int>();
        int segmentReached;   // 当前段已连到的下标（0 = 只有脉冲源）
        int source = -1;
        int waterTotal;
        bool tracing;
        bool viewedFruit;
        bool waterSolved;
        bool finishing;
        bool waterLinesDone; // 缺水解决后的那段对白已播完
        float savedRadiusFactor = -1f;
        bool savedAllowNodeDrag = true;
        float hintUntil;
        int pulseIndex;       // 下一个要亮的段内下标
        float nextPulseAt;
        NodeHaloRenderer halos;
        float savedLinkElbow = -1f;
        int routeTotal;       // 供给线路上的节点数（含树根），用来算连通比例
        LineRenderer dragLine;

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
            viewedFruit = waterSolved = finishing = waterLinesDone = false;
            passedFruit.Clear();
            connected.Clear();
            segment.Clear();
            source = -1;
            hintUntil = 0f;

            halos = EnsureHalos();
            if (halos != null) halos.ClearAll();
            if (ctx.links != null)
            {
                // 上次离开后的变形还没结束就重新进来：沿用之前保存的原值
                if (ctx.morpher != null) ctx.morpher.MorphCompleted -= RestoreElbowAfterMorph;
                if (savedLinkElbow < 0f) savedLinkElbow = ctx.links.elbowScale;
                ctx.links.SetElbowScale(linkElbow, immediate: true);
            }
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

            SpawnIssues();
            var root = RootNode();
            if (root != null) connected.Add(root.id);
            RebuildSegment();
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
                if (ctx.links != null)
                {
                    ctx.links.ClearAllLinkColors();
                    // 不在这里恢复：通关时连线已是直线，S3 的变形从直线开始；变形结束后再恢复原值（见 RestoreElbowAfterMorph）
                    if (savedLinkElbow >= 0f && ctx.morpher != null) ctx.morpher.MorphCompleted += RestoreElbowAfterMorph;
                    else if (savedLinkElbow >= 0f) ctx.links.SetElbowScale(savedLinkElbow, immediate: true);
                }
                if (halos != null) halos.ClearAll();
                // 脉冲源的闪烁和果实的"仍异常"闪烁不归问题系统管，这里一并恢复
                if (ctx.morpher != null) ctx.morpher.RestoreAll();
            }
            tracing = false;
            segment.Clear();
            source = -1;
            ShowDragLine(false);
            base.Exit();
        }

        // ---------- 准备 ----------

        PlantNodeSet Set => ctx.morpher != null ? ctx.morpher.nodeSet : null;

        // 发光圈组件和 NodeMorpher 在同一物体上；旧场景里没有时运行时补上（借用连线材质）
        NodeHaloRenderer EnsureHalos()
        {
            if (ctx.halos != null) return ctx.halos;
            if (ctx.morpher == null) return null;
            var h = ctx.morpher.GetComponent<NodeHaloRenderer>();
            if (h == null) h = ctx.morpher.gameObject.AddComponent<NodeHaloRenderer>();
            ctx.halos = h;
            return h;
        }

        PlantNode RootNode()
        {
            if (Set == null) return null;
            foreach (var n in Set.nodes) if (n.parentId < 0) return n;
            return null;
        }

        void SpawnIssues()
        {
            if (ctx.issues == null || Set == null) return;
            var leaves = leafNodes.Count > 0 ? leafNodes : AutoLeaves();
            var soils = soilNodes.Count > 0 ? soilNodes : AutoSoils();

            // 供给线路 = 每个缺水节点到树根的路径；果实不能出现在上面
            var route = new HashSet<int>();
            foreach (int id in soils) AddChain(id, route);
            foreach (int id in leaves) AddChain(id, route);
            routeTotal = route.Count;
            var fruits = AutoFruits(route);

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

        // 节点及其所有祖先
        void AddChain(int id, HashSet<int> into)
        {
            for (var n = ctx.Node(id); n != null; n = Set.GetParent(n)) into.Add(n.id);
        }

        List<int> AutoLeaves()
        {
            var ids = new List<int>();
            foreach (var n in Set.nodes)
                if (n.organ == Organ.Leaf && !string.IsNullOrEmpty(n.part) && n.part.Contains("dying")) ids.Add(n.id);
            if (ids.Count > 0) return ids;
            // 没有枯叶（比如假植物）：取层数最深的若干叶片，线路最长
            var all = new List<PlantNode>();
            foreach (var n in Set.nodes) if (n.organ == Organ.Leaf) all.Add(n);
            all.Sort((a, b) => b.depth.CompareTo(a.depth));
            for (int i = 0; i < all.Count && ids.Count < fallbackLeafCount; i++) ids.Add(all[i].id);
            return ids;
        }

        // 父节点是根的土块里，按回路布局离父节点最近的若干个（从土块到根只要一小步）
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

        // 每个果实（part 相同的节点算一个果实）取一个节点，和 S1 的规则一样；跳过供给线路经过的节点
        List<int> AutoFruits(HashSet<int> route)
        {
            var ids = new List<int>();
            var usedParts = new HashSet<string>();
            foreach (var n in Set.nodes)
            {
                if (ids.Count >= maxFruit) break;
                if (n.organ != Organ.Fruit || route.Contains(n.id)) continue;
                if (!string.IsNullOrEmpty(n.part) && !usedParts.Add(n.part)) continue;
                ids.Add(n.id);
            }
            return ids;
        }

        // ---------- 脉冲线路 ----------

        // 从已连通的部分重新计算下一段：目标 = 离已连通部分步数最少的未恢复缺水节点。
        // 树上目标往上走遇到的第一个已连通节点就是脉冲源，段 = 脉冲源往下的前 segmentLength 个节点
        void RebuildSegment()
        {
            segment.Clear();
            segmentReached = 0;
            List<int> best = null;
            if (ctx.issues != null && Set != null)
            {
                foreach (var issue in ctx.issues.Issues)
                {
                    if (issue.resolved || fruitIssueNodes.Contains(issue.nodeId)) continue;
                    var path = PathFromConnected(issue.nodeId);
                    if (path == null) continue;
                    if (best == null || path.Count < best.Count) best = path;
                }
            }
            if (best != null)
                for (int i = 0; i < best.Count && i < Mathf.Max(2, segmentLength); i++) segment.Add(best[i]);
            SetSource(segment.Count > 0 ? segment[0] : -1);
            pulseIndex = 1;
            nextPulseAt = Time.time;
        }

        // 已连通的祖先 → target 的路径（含两端）。路径上有无法解决的节点时返回 null
        List<int> PathFromConnected(int target)
        {
            var path = new List<int>();
            for (var n = ctx.Node(target); n != null; n = Set.GetParent(n))
            {
                path.Add(n.id);
                if (connected.Contains(n.id))
                {
                    path.Reverse();
                    return path;
                }
                if (fruitIssueNodes.Contains(n.id))
                {
                    Debug.LogWarning($"[S2] 缺水节点 {target} 的线路经过果实问题节点 {n.id}，这条线路跳过", this);
                    return null;
                }
            }
            return null; // 和已连通部分不在一棵树上
        }

        void SetSource(int id)
        {
            if (source == id) return;
            if (source >= 0 && ctx.morpher != null) ctx.morpher.SetTint(source, connectedColor);
            if (source >= 0 && halos != null) halos.SetGlow(source, sourceGlowColor, 0f);
            source = id;
            hintUntil = 0f;
            ApplySourceBlink();
        }

        void ApplySourceBlink()
        {
            if (source < 0 || ctx.morpher == null) return;
            bool hinting = Time.time < hintUntil;
            float frequency = hinting ? hintBlinkFrequency : sourceBlinkFrequency;
            ctx.morpher.SetBlink(source, frequency, sourceColor);
            if (halos != null) halos.SetGlow(source, sourceGlowColor, 1f, frequency);
        }

        void Update()
        {
            if (!IsActive || ctx == null || ctx.morpher == null) return;
            UpdateDragLine();

            // 问题恢复色到期后 NodeIssueSystem 会把节点恢复正常，已连通的颜色要补回来
            foreach (int id in connected)
                if (id != source && ctx.morpher.GetState(id) == NodeVisualState.Normal) ctx.morpher.SetTint(id, connectedColor);
            if (source < 0 || Morphing) return;

            // 脉冲源同理；提示结束后回到正常频率
            if (ctx.morpher.GetState(source) != NodeVisualState.Blink) ApplySourceBlink();
            if (hintUntil > 0f && Time.time >= hintUntil)
            {
                hintUntil = 0f;
                ApplySourceBlink();
            }

            // 脉冲：从已连到的下一个节点开始，沿段依次点亮外发光圈，走到段终点后停顿再重来
            if (Time.time < nextPulseAt || halos == null) return;
            if (pulseIndex <= segmentReached) pulseIndex = segmentReached + 1;
            if (pulseIndex < segment.Count)
            {
                halos.Flash(segment[pulseIndex], pulseColor, pulseHalfLife);
                pulseIndex++;
                nextPulseAt = Time.time + pulseStep;
            }
            else
            {
                pulseIndex = segmentReached + 1;
                nextPulseAt = Time.time + pulsePause;
            }
        }

        // ---------- 拖拽 ----------

        void HandleDragStart(int id)
        {
            if (!IsActive || Morphing || source < 0) return;
            if (id != source)
            {
                tracing = false;
                Hint(true);
                return;
            }
            tracing = true;
        }

        void HandleDragOver(int id)
        {
            if (!IsActive || !tracing || Morphing) return;
            int k = segment.IndexOf(id);
            // 只收段内、还没连上的节点；跳过的中间节点不超过 maxBridgeHops 步时补齐，否则忽略（拖拽不中断）
            if (k <= segmentReached) return;
            if (k - segmentReached > Mathf.Max(1, maxBridgeHops)) return;
            for (int i = segmentReached + 1; i <= k; i++)
            {
                if (ctx.links != null) ctx.links.SetLinkColor(segment[i - 1], segment[i], tracedLinkColor);
                Visit(segment[i]);
            }
            segmentReached = k;
            if (k < segment.Count - 1) return;

            // 段走完：终点就是下一段的脉冲源时可以接着拖，否则（回到分叉点）要重新按住
            int end = segment[k];
            RebuildSegment();
            if (source != end) tracing = false;
        }

        void HandleDragEnd()
        {
            if (!IsActive) return;
            // 中断不回退：从已连通的部分重新计算下一段
            if (tracing && segmentReached > 0) RebuildSegment();
            tracing = false;
            UpdateLinkElbow();
        }

        // 每结束一次拖拽：连线直角程度 = 进关值 → 0，按已连通的线路比例插值（全部连通 = 0，树形直线，S3 的变形从这里开始）
        void UpdateLinkElbow()
        {
            if (ctx.links == null) return;
            float progress = routeTotal <= 1 ? 1f : Mathf.Clamp01((connected.Count - 1f) / (routeTotal - 1f));
            if (WaterResolved() >= waterTotal) progress = 1f;
            ctx.links.SetElbowScale(Mathf.Lerp(linkElbow, 0f, progress));
        }

        // 离开 S2 后的那次变形结束时恢复直角倍数（网络 / 几何形态本身就是直线，恢复不会有可见变化；再回到回路时是直角）
        void RestoreElbowAfterMorph(MorphForm form)
        {
            if (ctx == null || ctx.morpher == null) return;
            ctx.morpher.MorphCompleted -= RestoreElbowAfterMorph;
            if (IsActive) return; // 已经重新进入 S2，由 Enter 设置
            if (ctx.links != null && savedLinkElbow >= 0f) ctx.links.SetElbowScale(savedLinkElbow, immediate: true);
            savedLinkElbow = -1f;
        }

        // ---------- 拖拽线 ----------

        // 拖拽中：从最后连上的节点到指针拉一条线。指针位置 = 指针射线和植株平面（回路形态是本地 XY 平面）的交点
        void UpdateDragLine()
        {
            int anchor = tracing && segmentReached < segment.Count ? segment[segmentReached] : -1;
            var poses = ctx.morpher.CurrentPoses;
            if (anchor < 0 || ctx.pointer == null || poses == null || Morphing)
            {
                ShowDragLine(false);
                return;
            }

            var t = ctx.morpher.transform;
            Vector3 anchorLocal = poses[anchor].position;
            Ray ray = ctx.pointer.CurrentRay;
            Vector3 origin = t.InverseTransformPoint(ray.origin);
            Vector3 dir = t.InverseTransformDirection(ray.direction);
            if (Mathf.Abs(dir.z) < 1e-5f)
            {
                ShowDragLine(false);
                return;
            }
            Vector3 endLocal = origin + dir * ((anchorLocal.z - origin.z) / dir.z);
            // 往相机方向抬一点，免得被方块挡住
            Vector3 lift = -dir.normalized * dragLineLift;

            var line = EnsureDragLine();
            if (line == null) return;
            line.SetPosition(0, anchorLocal + lift);
            line.SetPosition(1, endLocal + lift);
            ShowDragLine(true);
        }

        LineRenderer EnsureDragLine()
        {
            if (dragLine != null) return dragLine;
            var material = ctx.links != null ? ctx.links.material : null;
            if (material == null) return null;
            var go = new GameObject("S2DragLine");
            go.transform.SetParent(ctx.morpher.transform, false);
            go.layer = ctx.morpher.gameObject.layer;
            dragLine = go.AddComponent<LineRenderer>();
            dragLine.useWorldSpace = false; // 跟着植株的缩放和位置
            dragLine.positionCount = 2;
            dragLine.sharedMaterial = material;
            dragLine.numCapVertices = 4;
            dragLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dragLine.receiveShadows = false;
            dragLine.enabled = false;
            return dragLine;
        }

        void ShowDragLine(bool show)
        {
            if (dragLine == null) return;
            if (show)
            {
                // LineRenderer 的宽度是世界单位，按植株当前的世界缩放换算
                Vector3 s = ctx.morpher.transform.lossyScale;
                dragLine.startWidth = dragLine.endWidth = dragLineWidth * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
                dragLine.startColor = dragLine.endColor = dragLineColor;
            }
            dragLine.enabled = show;
        }

        // 连上一个节点：缺水问题恢复；果实不恢复，改成"仍异常"的闪烁（正常线路上不会出现）
        void Visit(int id)
        {
            connected.Add(id);
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

        // 提示从脉冲源开始：脉冲源快速闪一阵，不扣分不重置。点击只闪不播对白，免得乱点时对白刷屏
        void Hint(bool withDialogue)
        {
            if (source < 0) return;
            // 对白正在播（比如进入时的提示）就不打断
            if (withDialogue && (ctx.dialogue == null || !ctx.dialogue.IsPlaying)) ctx.Play(wrongStartSequence);
            hintUntil = Time.time + hintDuration;
            ApplySourceBlink();
        }

        // ---------- 点击和详情 ----------

        bool Morphing => ctx.morpher != null && ctx.morpher.IsMorphing;

        // 左键点击不连线（S2 靠拖拽）；点了脉冲源以外的节点时提示从脉冲源开始。点击反馈由 StageContext 统一处理
        void HandleTap(int id)
        {
            if (!IsActive || Morphing || id == source) return;
            Hint(false);
        }

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
                TryFinish();
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
                ctx.Play(waterSolvedSequence, () =>
                {
                    waterLinesDone = true;
                    TryFinish();
                });
            }
            RefreshPanel();
            TryFinish();
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

        void TryFinish()
        {
            if (!IsActive || finishing || !waterSolved || !waterLinesDone || !viewedFruit) return;
            finishing = true;
            ctx.Play(finishSequence, Complete);
        }
    }
}
