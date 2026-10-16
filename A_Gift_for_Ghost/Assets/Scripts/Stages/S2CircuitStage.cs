using System.Collections;
using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Gameplay;
using Ghost.Morph;
using Ghost.Narrative;
using PrimeTween;
using UnityEngine;

namespace Ghost.Stages
{
    // S2（第 4 节，形态 Circuit）：补缺口（docs/tasks/s1-s2-missing-nodes.md）。
    // 植株照常变成回路并连线，但 4 个缺失节点（StageContext.MissingNodes，和 S1 是同一批）的位置空着：
    // 它们自己的连线和子节点的连线都隐藏，缺口（它们在 Circuit 形态下的槽位）有呼吸外发光，节点本身散落在网格周围。
    // 玩家按住散落节点拖动（跟着 CurrentRay，在过拖拽起点、垂直于起始射线的平面上移动）：
    //   靠近正确缺口（snapRadius 内）→ 两侧连线向它伸出、缺口发光变强；拖开时连线收回；松手 → 缓动落位，发光熄灭。
    //   靠近错误缺口 → 没有反馈；松手 → 飘回散落位置，Agent 播 S2_WRONG。
    //   其他地方松手 → 静默飘回。
    // 4 个补齐之前点击或拖拽其他节点 → Agent 提示路径不存在（S2_NOPATH；对白正在播时不打断，有冷却）。
    // 4 个补齐后：播 S2_PATCHED，然后自动浇水——脉冲从树根沿供给线路（树根 → 各缺水节点）逐层推进，
    //   经过的连线变蓝、节点外发光闪一下（NodeHaloRenderer.Flash），缺水节点 Resolve，连线直角程度按进度降到 0；之后接 S2_006–007。
    // 右键节点弹出项目语言的详情（NodeDetailTable 的 Project 档），散落节点也能看；右键看过果实记为"已查看果实"。
    // 通关：① 缺水全部解决（播 S2_006–007）；② 右键查看过果实；两者都满足、且 S2_006–007 播完后播 S2_008，播完进入 S3。
    // 所有位置只走 NodeMorpher.SetPoseOverride（植株本地空间），不加 Collider / GameObject；不读鼠标 / 键盘 / 屏幕坐标。
    public class S2CircuitStage : Stage
    {
        public StageContext ctx;

        [Header("对白（台词表 S2 段）")]
        [Tooltip("进入时的提示")]
        public DialogueSequence introSequence;
        [Tooltip("4 个缺口补齐前点击 / 拖拽其他节点时的提示（S2_NOPATH）")]
        public DialogueSequence noPathSequence;
        [Tooltip("把散落节点放到错误缺口后松手时的提示（S2_WRONG）")]
        public DialogueSequence wrongModuleSequence;
        [Tooltip("4 个缺口补齐后、自动浇水前的提示（S2_PATCHED）")]
        public DialogueSequence patchedSequence;
        [Tooltip("缺水问题全部解决后的提示（引导去看果实）")]
        public DialogueSequence waterSolvedSequence;
        [Tooltip("缺水解决、且看过果实后播放，播完进入下一关")]
        public DialogueSequence finishSequence;

        [Header("问题节点（留空时按规则自动挑选，见 SpawnIssues；缺失节点总是排除在外）")]
        [Tooltip("缺水的叶片节点。留空 = 所有 part 含 \"dying\" 的叶片")]
        public List<int> leafNodes = new List<int>();
        [Tooltip("缺水的土壤节点。留空 = 自动挑 soilCount 个父节点是根、且在回路里离父节点最近的土块")]
        public List<int> soilNodes = new List<int>();
        public int soilCount = 2;
        [Tooltip("没有枯叶时随机挑的叶片个数")]
        public int fallbackLeafCount = 6;
        [Tooltip("有问题的果实最多几个（每个果实取一个节点）")]
        public int maxFruit = 4;

        [Header("拾取")]
        [Tooltip("S2 期间拾取半径系数（G2 默认 0.6）。回路节点间距 0.035 m、节点 0.024 m")]
        public float pickRadiusFactor = 0.72f;
        [Tooltip("进关时连线的直角程度：1 = 回路形态默认的直角折线，0 = 两点直连（树形）。补齐后降到 0（S3 的变形从直线开始）")]
        [Range(0f, 1f)] public float linkElbow = 1f;

        [Header("补缺口")]
        [Tooltip("吸附半径（植株本地空间，米）。回路节点间距约 0.035，默认约 1.5 个间距")]
        public float snapRadius = 0.0525f;
        [Tooltip("跳关直接进 S2 时，缺失节点飘到散落位置的用时（秒）")]
        public float scatterDuration = 1.2f;
        [Tooltip("松手后飘回散落位置的用时（秒）")]
        public float returnDuration = 0.45f;
        [Tooltip("松手后落进缺口的用时（秒）")]
        public float settleDuration = 0.25f;
        [Tooltip("预览时连线生长 / 收回速度（每秒进度）")]
        public float linkGrowSpeed = 4f;
        [Tooltip("缺口的呼吸外发光（和原来脉冲源一样）")]
        public Color gapGlowColor = new Color(0.35f, 0.7f, 1f, 0.9f);
        public float gapGlowFrequency = 1.2f;
        [Tooltip("拖到正确缺口附近时，缺口改成这个常亮外发光")]
        public Color gapPreviewColor = new Color(0.55f, 0.85f, 1f, 1f);
        [Header("散落节点")]
        [Tooltip("散落节点相对普通节点的放大倍数（落位时缩回 1 倍）")]
        public float looseScale = 1.5f;
        [Tooltip("散落节点外圈的蓝色呼吸脉冲")]
        public Color looseGlowColor = new Color(0.3f, 0.65f, 1f, 1f);
        public float looseGlowFrequency = 1.6f;
        [Tooltip("不拖拽时的浮游范围（植株本地空间，米；节点边长约 0.024）")]
        public float driftRadius = 0.006f;
        [Tooltip("浮游速度（噪声频率，Hz）")]
        public float driftFrequency = 0.35f;
        [Tooltip("闲置时跟随浮游位置的平滑速度（每秒），也决定 S1 交接结束后放大到 looseScale 的快慢")]
        public float idleFollowRate = 4f;

        [Header("缺口详情")]
        [Tooltip("右键查看缺口（槽位上没有节点）时的详情标题")]
        public string gapDetailTitle = "模块缺失";
        [TextArea(2, 4)]
        [Tooltip("右键查看缺口时的详情正文")]
        public string gapDetailBody = "模块缺失，产线已暂停运作。请根据周边模块推测缺失模块类型，重新插入模块以继续生产。";

        [Tooltip("路径不存在提示的冷却（秒）")]
        public float noPathCooldown = 5f;

        [Header("自动浇水")]
        [Tooltip("脉冲从一层节点推进到下一层的间隔（秒）")]
        public float pulseStep = 0.3f;
        [Tooltip("脉冲发光的半衰期（秒）。等于 pulseStep 时，下一层亮起时上一层还剩 50%")]
        public float pulseHalfLife = 0.3f;
        [Tooltip("脉冲外发光的颜色")]
        public Color pulseColor = new Color(0.35f, 0.7f, 1f, 0.85f);
        [Tooltip("脉冲经过后连线的颜色")]
        public Color tracedLinkColor = new Color(0.2f, 0.5f, 1f);

        public override string PanelText => "";

        // 任务面板下标
        const int TaskWater = 0;
        const int TaskInspect = 1;
        const int TaskFruit = 2;

        readonly HashSet<int> fruitIssueNodes = new HashSet<int>();
        readonly List<int> missing = new List<int>();
        readonly HashSet<int> placed = new HashSet<int>();
        readonly Dictionary<int, Tween> moveTweens = new Dictionary<int, Tween>();
        readonly HashSet<int> looseGlowing = new HashSet<int>();
        int waterTotal;
        bool viewedFruit;
        bool waterSolved;
        bool finishing;
        bool waterLinesDone; // 缺水解决后的那段对白已播完
        bool missingReady;   // 缺失节点已挑好、连线已断开
        bool gapGlowsShown;
        float savedRadiusFactor = -1f;
        bool savedAllowNodeDrag = true;
        NodeHaloRenderer halos;
        float savedLinkElbow = -1f;
        float noPathAt = -999f;
        Coroutine setupRoutine;
        Coroutine waterRoutine;

        // 拖拽状态（植株本地空间）
        int dragId = -1;
        Vector3 dragPlanePoint, dragPlaneNormal, dragOffset;
        int previewGap = -1; // 正在预览的正确缺口（= dragId），没有时 -1
        int nearGap = -1;    // 当前离得最近、在半径内的缺口（可能是错的）

        // 音效等订阅：开始 / 结束拖散落节点；节点落位；放错被弹回；浇水时脉冲推进一层（参数：第几层，从 1 起）；浇水开始 / 结束
        public event System.Action TraceStarted;
        public event System.Action TraceEnded;
        public event System.Action<int> NodePlaced;
        public event System.Action<int> PlaceRejected;
        public event System.Action<int> NodeLinked;
        public event System.Action WateringStarted;
        public event System.Action WateringFinished;
        public bool IsWatering => waterRoutine != null;
        public bool IsTracing => dragId >= 0;
        public bool AllPlaced => missingReady && placed.Count >= missing.Count;

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
            viewedFruit = waterSolved = finishing = waterLinesDone = false;
            missingReady = gapGlowsShown = false;
            missing.Clear();
            placed.Clear();
            looseGlowing.Clear();
            dragId = previewGap = nearGap = -1;
            noPathAt = -999f;

            halos = ctx.EnsureHalos();
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
                ctx.pointer.DragEnd += HandleDragEnd;
            }
            if (ctx.issues != null) ctx.issues.IssueResolved += HandleResolved;
            ctx.SetInspectProvider(ProvideInspect);
            ctx.Inspected += HandleInspected;

            SetupTaskPanel();
            ctx.Play(introSequence);
            setupRoutine = StartCoroutine(SetupWhenReady());
        }

        public override void Exit()
        {
            if (setupRoutine != null)
            {
                StopCoroutine(setupRoutine);
                setupRoutine = null;
            }
            if (waterRoutine != null)
            {
                StopCoroutine(waterRoutine);
                waterRoutine = null;
            }
            StopMoveTweens();
            bool wasDragging = dragId >= 0;
            dragId = previewGap = nearGap = -1;
            if (wasDragging) TraceEnded?.Invoke();
            if (ctx != null)
            {
                if (ctx.pointer != null)
                {
                    ctx.pointer.Tap -= HandleTap;
                    ctx.pointer.DragStart -= HandleDragStart;
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
                if (ctx.picker != null) ctx.picker.ClearAliases();
                // 散落 / 拖拽中的姿态覆盖、缺口发光、隐藏的连线
                ctx.ClearMissingNodeVisuals();
                if (ctx.morpher != null) ctx.morpher.RestoreAll();
            }
            missing.Clear();
            placed.Clear();
            looseGlowing.Clear();
            missingReady = false;
            base.Exit();
        }

        void OnDestroy()
        {
            StopMoveTweens();
        }

        // ---------- 准备 ----------

        PlantNodeSet Set => ctx.morpher != null ? ctx.morpher.nodeSet : null;
        bool Morphing => ctx.morpher != null && ctx.morpher.IsMorphing;
        // 拖拽、点击提示在变形中、S1 交接缓动中、缺失节点没准备好时都不生效
        bool Interactive => IsActive && missingReady && !Morphing && !ctx.IsHandingOff;

        // 缺失节点的挑选要用连线表（NodeLinkRenderer.Start 之后才有）；Play 第一帧就进关时等它建好
        IEnumerator SetupWhenReady()
        {
            while (IsActive && (ctx.morpher == null || !ctx.morpher.IsReady || ctx.links == null || !ctx.links.IsReady))
                yield return null;
            setupRoutine = null;
            if (!IsActive) yield break;

            missing.AddRange(ctx.MissingNodes());
            foreach (int id in missing)
            {
                // 缺口两侧断线（立即生效，变形过程中就看得出"缺了中间节点"）
                ctx.links.SetNodeLinksGrowth(id, 0f);
                // S1 交接过来时已有姿态覆盖、正在飘向散落位置；跳关进来时从变形结果渐变到散落位置
                if (!ctx.morpher.HasPoseOverride(id) && ctx.MissingScatterPose(id, out _))
                {
                    int nodeId = id;
                    // 目标每帧现算（含放大和浮游），缓动结束时和闲置浮游无缝衔接
                    var tween = Tween.Custom(this, 0f, 1f, Mathf.Max(0.01f, scatterDuration),
                        (self, w) => self.ctx.morpher.SetPoseOverride(nodeId, self.LoosePose(nodeId), w), Ease.InOutSine);
                    moveTweens[id] = tween.OnComplete(this, self => self.moveTweens.Remove(nodeId));
                }
            }
            missingReady = true;
            SpawnIssues();
            RefreshPanel();
        }

        void SpawnIssues()
        {
            if (ctx.issues == null || Set == null) return;
            var excluded = new HashSet<int>(missing);
            var leaves = Without(leafNodes.Count > 0 ? leafNodes : AutoLeaves(), excluded);
            var soils = Without(soilNodes.Count > 0 ? soilNodes : AutoSoils(), excluded);
            var fruits = AutoFruits(excluded);

            // 难度只决定点击规则；S2 不点击解决，全部在补齐缺口后 Resolve
            ctx.issues.AddIssues(soils, IssueDifficulty.Easy);
            ctx.issues.AddIssues(leaves, IssueDifficulty.Easy);
            ctx.issues.AddIssues(fruits, IssueDifficulty.Hard);
            fruitIssueNodes.Clear();
            foreach (int id in fruits) fruitIssueNodes.Add(id);
            waterTotal = 0;
            foreach (var issue in ctx.issues.Issues)
                if (!fruitIssueNodes.Contains(issue.nodeId)) waterTotal++;
            Debug.Log($"[S2] 缺失节点 {string.Join(", ", missing)}；缺水：土壤 {string.Join(", ", soils)}；叶片 {string.Join(", ", leaves)}；果实（不会恢复）{string.Join(", ", fruits)}", this);
        }

        static List<int> Without(List<int> ids, HashSet<int> excluded)
        {
            var result = new List<int>(ids.Count);
            foreach (int id in ids) if (!excluded.Contains(id)) result.Add(id);
            return result;
        }

        List<int> AutoLeaves()
        {
            var ids = new List<int>();
            foreach (var n in Set.nodes)
                if (n.organ == Organ.Leaf && !string.IsNullOrEmpty(n.part) && n.part.Contains("dying")) ids.Add(n.id);
            if (ids.Count > 0) return ids;
            // 没有枯叶（比如假植物）：取层数最深的若干叶片
            var all = new List<PlantNode>();
            foreach (var n in Set.nodes) if (n.organ == Organ.Leaf) all.Add(n);
            all.Sort((a, b) => b.depth.CompareTo(a.depth));
            for (int i = 0; i < all.Count && ids.Count < fallbackLeafCount; i++) ids.Add(all[i].id);
            return ids;
        }

        // 父节点是根的土块里，按回路布局离父节点最近的若干个
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

        // 每个果实（part 相同的节点算一个果实）取一个节点，和 S1 的规则一样；跳过缺失节点
        List<int> AutoFruits(HashSet<int> excluded)
        {
            var ids = new List<int>();
            var usedParts = new HashSet<string>();
            foreach (var n in Set.nodes)
            {
                if (ids.Count >= maxFruit) break;
                if (n.organ != Organ.Fruit || excluded.Contains(n.id)) continue;
                if (!string.IsNullOrEmpty(n.part) && !usedParts.Add(n.part)) continue;
                ids.Add(n.id);
            }
            return ids;
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

        // ---------- 每帧 ----------

        void Update()
        {
            if (!IsActive || ctx == null || ctx.morpher == null || !missingReady) return;

            // 缺口发光等变形结束后再亮（变形中槽位附近的节点还在飞）
            if (!gapGlowsShown && !Morphing)
            {
                gapGlowsShown = true;
                foreach (int id in missing)
                {
                    if (placed.Contains(id)) continue;
                    SetGapGlow(id, false);
                    // 缺口也能右键看详情：槽位上挂一个替身拾取球，命中时报告缺失节点的 id（详情里按 HitsAlias 区分）
                    if (ctx.picker != null) ctx.picker.SetAlias(id, GapPose(id));
                }
            }
            if (dragId >= 0) UpdateDrag();
            UpdateLoose();
        }

        // 散落节点的闲置状态：放大 + 蓝色呼吸脉冲 + 小范围浮游。
        // S1 交接缓动、飘回 / 散落缓动、拖拽中不接管姿态；交接结束后从当前姿态平滑过渡过来（顺带放大到 looseScale）
        void UpdateLoose()
        {
            if (ctx.IsHandingOff) return;
            var poses = ctx.morpher.CurrentPoses;
            if (poses == null) return;
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, idleFollowRate) * Time.deltaTime);
            foreach (int id in missing)
            {
                if (!IsLoose(id)) continue;
                if (halos != null && looseGlowing.Add(id)) halos.SetGlow(id, looseGlowColor, 1f, looseGlowFrequency);
                if (id == dragId || moveTweens.ContainsKey(id)) continue;
                ctx.morpher.SetPoseOverride(id, NodePose.Lerp(poses[id], LoosePose(id), k), 1f);
            }
        }

        // 散落姿态（放大 looseScale）+ 浮游偏移。Perlin 噪声，每个节点相位不同，在 driftRadius 范围内缓慢游走
        NodePose LoosePose(int id, bool drift = true)
        {
            if (!ctx.MissingScatterPose(id, out var pose)) return ctx.Node(id).GetPose(MorphForm.Circuit);
            pose.scale *= looseScale;
            if (!drift || driftRadius <= 0f) return pose;
            float t = Time.time * driftFrequency;
            float sx = id * 17.31f, sy = id * 7.77f + 50f, sz = id * 3.13f + 100f;
            var offset = new Vector3(
                Mathf.PerlinNoise(sx, t) * 2f - 1f,
                Mathf.PerlinNoise(sy, t) * 2f - 1f,
                (Mathf.PerlinNoise(sz, t) * 2f - 1f) * 0.5f);
            pose.position += offset * driftRadius;
            return pose;
        }

        NodePose GapPose(int id) => ctx.Node(id).GetPose(MorphForm.Circuit);

        void SetGapGlow(int id, bool preview)
        {
            if (halos == null) return;
            if (preview) halos.SetGlowAt(id, GapPose(id), gapPreviewColor, 1f);
            else halos.SetGlowAt(id, GapPose(id), gapGlowColor, 1f, gapGlowFrequency);
        }

        // ---------- 拖拽 ----------

        bool IsLoose(int id) => missingReady && missing.Contains(id) && !placed.Contains(id);

        // 射线命中的是缺口（替身拾取球），不是散落节点本身
        bool PointsAtGap(int id) => IsLoose(id) && ctx.picker != null && ctx.pointer != null && ctx.picker.HitsAlias(ctx.pointer.CurrentRay, id);

        void HandleDragStart(int id)
        {
            if (!Interactive || ctx.pointer == null) return;
            // 从缺口上开始拖：缺口里没有模块，什么都不做（不提示路径不存在，免得和"看缺口详情"混淆）
            if (PointsAtGap(id)) return;
            if (!IsLoose(id))
            {
                if (!AllPlaced) HintNoPath();
                return;
            }
            var poses = ctx.morpher.CurrentPoses;
            if (poses == null) return;
            StopMove(id); // 飘回 / 散落途中被抓住：从当前位置接着拖
            dragId = id;
            previewGap = nearGap = -1;
            // 移动平面：回路平面本身（植株本地 XY 平面，过缺口所在深度；散落位置也在这个深度上，按下时不会跳）。
            // 不用"垂直于起始射线"的平面：透视相机下散落节点在画面边缘，起始射线是斜的，平面跟着倾斜，
            // 拖到缺口上"看起来重合"时实际位置沿视线方向差出约 横向距离 × sin(射线倾角)，常常超过 snapRadius
            var t = ctx.morpher.transform;
            Ray ray = ctx.pointer.CurrentRay;
            dragPlanePoint = GapPose(id).position;
            dragPlaneNormal = Vector3.forward;
            // 射线几乎和回路平面平行（VR 里从侧面指）时退回到垂直于射线的平面
            if (Mathf.Abs(Vector3.Dot(t.InverseTransformDirection(ray.direction).normalized, dragPlaneNormal)) < 0.2f)
            {
                dragPlanePoint = poses[id].position;
                dragPlaneNormal = t.InverseTransformDirection(ray.direction).normalized;
            }
            // 保持按下时指针和节点中心（投到拖拽平面上）的偏移，节点不会跳到指针下
            Vector3 grab = poses[id].position - dragPlaneNormal * Vector3.Dot(poses[id].position - dragPlanePoint, dragPlaneNormal);
            dragOffset = RayOnPlane(ray, out var hit) ? grab - hit : Vector3.zero;
            TraceStarted?.Invoke();
        }

        // 指针射线和拖拽平面的交点（植株本地空间）
        bool RayOnPlane(Ray ray, out Vector3 point)
        {
            var t = ctx.morpher.transform;
            Vector3 origin = t.InverseTransformPoint(ray.origin);
            Vector3 dir = t.InverseTransformDirection(ray.direction);
            float denom = Vector3.Dot(dir, dragPlaneNormal);
            point = default;
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float k = Vector3.Dot(dragPlanePoint - origin, dragPlaneNormal) / denom;
            if (k < 0f) return false;
            point = origin + dir * k;
            return true;
        }

        void UpdateDrag()
        {
            if (Morphing || ctx.pointer == null || !ctx.MissingScatterPose(dragId, out _)) return;
            if (!RayOnPlane(ctx.pointer.CurrentRay, out var hit)) return;
            Vector3 pos = hit + dragOffset;
            var held = LoosePose(dragId, drift: false);
            ctx.morpher.SetPoseOverride(dragId, new NodePose(pos, held.rotation, held.scale), 1f);

            // 半径内最近的未补缺口（参考 XRSocketInteractor：悬停 → 预览 → 松手吸入，只借思路）
            nearGap = -1;
            float best = snapRadius * snapRadius;
            foreach (int id in missing)
            {
                if (placed.Contains(id)) continue;
                // 只比画面内（回路平面 XY）的距离，深度差不算：看起来压在缺口上就算靠近
                Vector3 delta = GapPose(id).position - pos;
                float d = delta.x * delta.x + delta.y * delta.y;
                if (d <= best)
                {
                    best = d;
                    nearGap = id;
                }
            }
            SetPreview(nearGap == dragId ? dragId : -1);
        }

        // 进入 / 离开正确缺口：两侧连线朝拖拽中的节点伸出 / 收回，缺口发光变强 / 恢复呼吸
        void SetPreview(int gap)
        {
            if (previewGap == gap) return;
            if (previewGap >= 0)
            {
                if (ctx.links != null) ctx.links.SetNodeLinksGrowth(previewGap, 0f, linkGrowSpeed);
                SetGapGlow(previewGap, false);
            }
            previewGap = gap;
            if (previewGap >= 0)
            {
                if (ctx.links != null) ctx.links.SetNodeLinksGrowth(previewGap, 1f, linkGrowSpeed);
                SetGapGlow(previewGap, true);
            }
        }

        void HandleDragEnd()
        {
            if (dragId < 0) return;
            int id = dragId;
            bool correct = previewGap == id;
            int wrongGap = correct ? -1 : nearGap;
            previewGap = nearGap = -1;
            dragId = -1;
            TraceEnded?.Invoke();
            if (!IsActive) return;

            if (correct)
            {
                Place(id);
                return;
            }
            if (wrongGap >= 0)
            {
                PlayFeedback(wrongModuleSequence);
                PlaceRejected?.Invoke(id);
            }
            ReturnToScatter(id);
        }

        // 落位：缓动到槽位，连线长满，缺口发光熄灭；到位后去掉姿态覆盖（之后跟着形态正常变形）
        void Place(int id)
        {
            placed.Add(id);
            if (ctx.links != null) ctx.links.SetNodeLinksGrowth(id, 1f, linkGrowSpeed);
            if (halos != null)
            {
                halos.ClearAt(id);
                halos.Clear(id);
            }
            looseGlowing.Remove(id);
            if (ctx.picker != null) ctx.picker.ClearAlias(id);
            MoveTo(id, GapPose(id), settleDuration, Ease.OutCubic, clearOverride: true);
            NodePlaced?.Invoke(id);
            Debug.Log($"[S2] 节点 {id} 已补回（{placed.Count} / {missing.Count}）", this);
            if (AllPlaced) OnAllPlaced();
        }

        void ReturnToScatter(int id)
        {
            if (ctx.links != null) ctx.links.SetNodeLinksGrowth(id, 0f, linkGrowSpeed);
            if (gapGlowsShown) SetGapGlow(id, false);
            if (ctx.MissingScatterPose(id, out _)) MoveTo(id, default, returnDuration, Ease.OutCubic, clearOverride: false, toLoose: true);
        }

        // 从当前显示位置缓动到 target（权重 1 的姿态覆盖）。同一节点上一段缓动会被打断，从当前位置接着走。
        // toLoose：目标是每帧现算的散落姿态（含放大和浮游），结束后交给 UpdateLoose 继续浮游，不会跳
        void MoveTo(int id, NodePose target, float duration, Ease ease, bool clearOverride, bool toLoose = false)
        {
            StopMove(id);
            var poses = ctx.morpher.CurrentPoses;
            if (poses == null) return;
            var from = poses[id];
            int nodeId = id;
            var tween = toLoose
                ? Tween.Custom(this, 0f, 1f, Mathf.Max(0.01f, duration),
                    (self, t) => self.ctx.morpher.SetPoseOverride(nodeId, NodePose.Lerp(from, self.LoosePose(nodeId), t), 1f), ease)
                : Tween.Custom(this, 0f, 1f, Mathf.Max(0.01f, duration),
                    (self, t) => self.ctx.morpher.SetPoseOverride(nodeId, NodePose.Lerp(from, target, t), 1f), ease);
            moveTweens[id] = tween.OnComplete(this, self =>
            {
                self.moveTweens.Remove(nodeId);
                if (clearOverride) self.ctx.morpher.ClearPoseOverride(nodeId);
            });
        }

        void StopMove(int id)
        {
            if (!moveTweens.TryGetValue(id, out var tween)) return;
            tween.Stop();
            moveTweens.Remove(id);
        }

        void StopMoveTweens()
        {
            foreach (var tween in moveTweens.Values) tween.Stop();
            moveTweens.Clear();
        }

        // 4 个补齐：播"路径已重建"，然后自动浇水
        void OnAllPlaced()
        {
            ctx.Play(patchedSequence, () =>
            {
                if (IsActive && waterRoutine == null) waterRoutine = StartCoroutine(Water());
            });
        }

        // 自动浇水：供给线路 = 每个未恢复缺水节点到树根的路径（跳过果实问题节点）。
        // 脉冲从树根开始按层推进：每层的节点外发光闪一下、连到父节点的线变蓝、缺水节点 Resolve；直角程度按层进度降到 0
        IEnumerator Water()
        {
            // 等最后一个节点落位（台词为空时 Play 会立即回调）
            yield return new WaitForSeconds(settleDuration);
            WateringStarted?.Invoke();

            var layers = BuildWaterLayers();
            float startElbow = ctx.links != null ? ctx.links.elbowScale : 0f;
            for (int k = 0; k < layers.Count; k++)
            {
                while (Morphing) yield return null;
                foreach (int id in layers[k])
                {
                    if (halos != null) halos.Flash(id, pulseColor, pulseHalfLife);
                    if (ctx.links != null && k > 0) ctx.links.SetLinkColor(id, tracedLinkColor);
                    if (ctx.issues != null && !fruitIssueNodes.Contains(id)) ctx.issues.Resolve(id); // 没有问题时什么也不做
                }
                if (k > 0) NodeLinked?.Invoke(k);
                float progress = layers.Count <= 1 ? 1f : (float)k / (layers.Count - 1);
                if (ctx.links != null) ctx.links.SetElbowScale(Mathf.Lerp(startElbow, 0f, progress));
                yield return new WaitForSeconds(pulseStep);
            }
            if (ctx.links != null) ctx.links.SetElbowScale(0f);

            // 兜底：线路走不到的缺水节点（不在同一棵树上 / 路径被果实挡住）也算恢复，不卡关
            if (ctx.issues != null)
            {
                var left = new List<int>();
                foreach (var issue in ctx.issues.Issues)
                    if (!issue.resolved && !fruitIssueNodes.Contains(issue.nodeId)) left.Add(issue.nodeId);
                if (left.Count > 0) Debug.LogWarning($"[S2] 浇水线路没有经过缺水节点 {string.Join(", ", left)}，直接恢复", this);
                foreach (int id in left) ctx.issues.Resolve(id);
            }
            waterRoutine = null;
            WateringFinished?.Invoke();

            // 没有缺水节点时不会收到 IssueResolved，直接走"缺水已解决"
            if (!waterSolved)
            {
                waterSolved = true;
                ctx.Play(waterSolvedSequence, () =>
                {
                    waterLinesDone = true;
                    TryFinish();
                });
                RefreshPanel();
            }
        }

        // 供给线路按离树根的步数分层：layers[0] = 树根，layers[k] = 第 k 步到达的节点
        List<List<int>> BuildWaterLayers()
        {
            var layers = new List<List<int>>();
            if (ctx.issues == null || Set == null) return layers;
            var depthOf = new Dictionary<int, int>();
            var chain = new List<int>();
            foreach (var issue in ctx.issues.Issues)
            {
                if (issue.resolved || fruitIssueNodes.Contains(issue.nodeId)) continue;
                chain.Clear();
                bool blocked = false;
                for (var n = ctx.Node(issue.nodeId); n != null; n = Set.GetParent(n))
                {
                    if (fruitIssueNodes.Contains(n.id)) { blocked = true; break; }
                    chain.Add(n.id);
                }
                if (blocked) continue;
                // chain：缺水节点 → 树根；倒过来第 k 个就是第 k 步
                for (int k = 0; k < chain.Count; k++)
                    depthOf[chain[chain.Count - 1 - k]] = k;
            }
            foreach (var pair in depthOf)
            {
                while (layers.Count <= pair.Value) layers.Add(new List<int>());
                layers[pair.Value].Add(pair.Key);
            }
            return layers;
        }

        // ---------- 提示 ----------

        // 路径不存在：对白正在播时不打断，同一句有冷却
        void HintNoPath()
        {
            if (Time.time - noPathAt < noPathCooldown) return;
            if (ctx.dialogue != null && ctx.dialogue.IsPlaying) return;
            noPathAt = Time.time;
            ctx.Play(noPathSequence);
        }

        // 放错的提示：空闲时播；正在播的是本关的提示句时替换掉；其他对白（进关、通关）不打断
        void PlayFeedback(DialogueSequence sequence)
        {
            if (sequence == null) return;
            if (ctx.dialogue != null && ctx.dialogue.IsPlaying)
            {
                var current = ctx.dialogue.CurrentSequence;
                if (current != noPathSequence && current != wrongModuleSequence) return;
            }
            ctx.Play(sequence);
        }

        // ---------- 点击和详情 ----------

        // 左键点击不做事；补齐前点了散落节点以外的节点时提示路径不存在。点击反馈由 StageContext 统一处理
        void HandleTap(int id)
        {
            if (!Interactive || AllPlaced || IsLoose(id)) return;
            HintNoPath();
        }

        // 右键查看详情（散落节点也能看；缺口处显示"模块缺失"）
        bool ProvideInspect(int id, out string title, out string body)
        {
            if (PointsAtGap(id))
            {
                title = gapDetailTitle;
                body = gapDetailBody;
                return !Morphing;
            }
            title = PopupTitle(id);
            body = PopupBody(id);
            return !Morphing;
        }

        // 通关条件之一"已查看果实"：右键查看任意果实节点
        void HandleInspected(int id)
        {
            if (!IsActive || Morphing || PointsAtGap(id)) return; // 看的是空缺口，不算看过果实
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
            if (IsLoose(id)) status = "状态：未接入";
            else if (organ == Organ.Fruit && issue != null) status = "状态：仍异常";
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
            // 浇水途中不播：浇完由 Water() 统一接 S2_006
            if (!waterSolved && waterRoutine == null && WaterResolved() >= waterTotal)
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
            if (ctx.issues == null) return n;
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
            ctx.taskPanel.UpdateTask(TaskFruit, waterSolved ? TaskState.Failed : TaskState.Pending);
            float rate = waterTotal <= 0 ? 1f : (float)done / waterTotal;
            ctx.taskPanel.SetMetric("供给恢复", rate, $"{done} / {waterTotal}");
            // 【占位】置信度：随供给恢复升
            ctx.taskPanel.SetMetric("置信度", Mathf.Clamp01(0.35f + 0.45f * rate));
        }

        void TryFinish()
        {
            if (!IsActive || finishing || !waterSolved || !waterLinesDone || !viewedFruit) return;
            finishing = true;
            ctx.Play(finishSequence, Complete);
        }
    }
}
