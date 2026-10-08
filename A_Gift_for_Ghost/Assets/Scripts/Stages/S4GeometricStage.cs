using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // S4（第 4 节，形态 Geometric，逐步变写实）：玩家转动盆栽，找到藏在叶背的虫子，点击摘除。
    // 每摘除一只，整株写实度（G3 Realness）上升一档：浅色几何体 → 鲜艳的几何体，但始终不是写实模型。
    // 点其他节点弹出物体描述（Physical 档，和 S3 一致）。通关：所有虫子都摘除 → 稍等后 Complete()，进入过渡（G10）。
    //
    // 跳关清理：
    //   - Enter 先把所有虫子节点重新显示、恢复颜色，计数归零；
    //   - 正常通关或往后跳（Transition 及以后）：被摘除的虫子保持隐藏，写实度停在 1（和 Real 形态一致，交给写实模型不跳色）；
    //   - 之后再往回变形到比 Geometric 更抽象的形态（跳回 S3 及以前）：订阅 morpher.MorphStarted，
    //     在新阶段 Enter 之前把虫子重新显示、清除写实度覆盖（S3 自己的写实度会在它的 Enter 里设置，不会被覆盖）。
    public class S4GeometricStage : Stage
    {
        public StageContext ctx;
        [Tooltip("旋转盆栽的组件（和 S3 共用）")]
        public TargetRotator rotator;
        [Tooltip("可选：在虫子节点上摆真实虫子模型")]
        public BugModelInstances bugModels;

        [Header("对白（占位）")]
        [Tooltip("进入时的提示：手是你的了，转动盆栽找虫子")]
        public DialogueSequence introSequence;
        [Tooltip("摘除第一只虫子后播放")]
        public DialogueSequence firstRemovedSequence;

        [Header("写实度")]
        [Tooltip("进入时的写实度（Geometric 形态本身是 0.4）。越低越接近灰阶")]
        [Range(0f, 1f)] public float startRealness = 0.15f;
        [Tooltip("摘完最后一只时的写实度，几何体最鲜艳的一档（仍不是写实模型）")]
        [Range(0f, 1f)] public float endRealness = 0.95f;
        [Tooltip("每一档写实度的过渡时长（秒）")]
        public float stepDuration = 0.9f;
        [Tooltip("离开本关进入写实阶段时的写实度（Real 形态是 1）")]
        [Range(0f, 1f)] public float exitRealness = 1f;

        [Header("虫子")]
        [Tooltip("虫子节点的颜色（和 S3 相同的黄绿色）")]
        public Color bugColor = BugStageUtil.DefaultBugColor;
        [Tooltip("摘除时节点闪一下的颜色")]
        public Color removeFlashColor = new Color(1f, 0.92f, 0.45f);
        [Tooltip("虫背朝向相机的程度（点积）达到多少才点得到。0 = 侧对相机即可；调高 = 必须把叶背转得更正")]
        [Range(-1f, 1f)] public float bugFacingThreshold = 0f;
        [Tooltip("别的节点比虫子近多少米才算挡住虫子（拾取球偏大，留一点余量）")]
        public float occluderMargin = 0.02f;

        [Header("通关")]
        [Tooltip("摘完最后一只后等多久进入下一关（秒），让最后一档写实度过渡完")]
        public float completeDelay = 1.6f;

        public override string PanelText => "";

        public int BugTotal => bugNodes.Count;
        public int BugRemoved => removed.Count;
        public IReadOnlyList<int> BugNodes => bugNodes;
        // 当前（tween 中的）写实度
        public float CurrentRealness => realnessNow;

        readonly List<int> bugNodes = new List<int>();
        readonly HashSet<int> removed = new HashSet<int>();
        // 已经摘除、离开本关后仍保持隐藏的虫子（往回跳到更抽象的形态时恢复）
        readonly HashSet<int> keptHidden = new HashSet<int>();
        bool holdsRealness;
        bool finishing;
        float completeTimer;
        float realnessFrom, realnessTo, realnessNow, realnessT = 1f;
        bool savedAllowEmptyDrag = true;
        bool savedAllowNodeDrag = true;
        NodeMorpher subscribedMorpher;

        void Awake()
        {
            if (ctx != null && ctx.morpher != null)
            {
                subscribedMorpher = ctx.morpher;
                subscribedMorpher.MorphStarted += HandleMorphStarted;
            }
        }

        void OnDestroy()
        {
            if (subscribedMorpher != null) subscribedMorpher.MorphStarted -= HandleMorphStarted;
        }

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
            removed.Clear();
            finishing = false;

            bugNodes.Clear();
            bugNodes.AddRange(BugStageUtil.CollectBugs(ctx.morpher != null ? ctx.morpher.nodeSet : null, "S4", this));
            RestoreBugs();

            if (ctx.morpher != null)
            {
                ctx.morpher.RestoreAll();
                foreach (int id in bugNodes) ctx.morpher.SetTint(id, bugColor);
                // 不用 SetRealnessImmediate：从形态自带的写实度平滑过渡到浅色
                ctx.morpher.Realness = startRealness;
                holdsRealness = true;
            }
            realnessFrom = realnessTo = realnessNow = startRealness;
            realnessT = 1f;

            if (bugModels != null) bugModels.Spawn(bugNodes);

            if (ctx.pointer != null)
            {
                savedAllowEmptyDrag = ctx.pointer.allowEmptyDrag;
                savedAllowNodeDrag = ctx.pointer.allowNodeDrag;
                ctx.pointer.allowEmptyDrag = true;
                // 和 S3 一样：从节点上按下拖动也旋转（TargetRotator.rotateOnNodeDrag），本关不订阅 DragOver
                ctx.pointer.allowNodeDrag = true;
                ctx.pointer.Tap += HandleTap;
            }
            // 虫子都在叶背：默认正面（偏航 0）三只都背对相机，所以从正面开始，不额外转起始角度
            if (rotator != null) rotator.Enable();

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
                    // 摘除的虫子保持隐藏（往后进入写实阶段）；其余节点恢复正常颜色
                    ctx.morpher.RestoreAll();
                    keptHidden.Clear();
                    foreach (int id in removed) keptHidden.Add(id);
                    // 写实度停在 exitRealness，和写实形态一致；往回跳时由 HandleMorphStarted 清除
                    ctx.morpher.Realness = exitRealness;
                    holdsRealness = true;
                }
            }
            if (bugModels != null) bugModels.Clear();
            // 离开时平滑转回正面（和变形同时进行）。G10 进入写实前可以再 ResetRotation(false) 保证俯仰为 0
            if (rotator != null)
            {
                rotator.Disable();
                rotator.ResetRotation(true);
            }
            finishing = false;
            base.Exit();
        }

        // 往更抽象的形态变形（跳回 S3 及以前）：在新阶段 Enter 之前恢复虫子、清除写实度覆盖
        void HandleMorphStarted(MorphForm form)
        {
            if (IsActive || form >= MorphForm.Geometric) return;
            RestoreBugs();
            if (holdsRealness && ctx != null && ctx.morpher != null) ctx.morpher.ClearRealness();
            holdsRealness = false;
        }

        void RestoreBugs()
        {
            if (ctx == null || ctx.morpher == null) return;
            foreach (int id in keptHidden) ctx.morpher.Show(id, true);
            foreach (int id in bugNodes) ctx.morpher.Show(id, true);
            keptHidden.Clear();
        }

        void Update()
        {
            if (!IsActive || ctx == null || ctx.morpher == null) return;

            // 写实度逐档过渡（自己 tween，比 morpher 自带的匀速过渡更柔和）
            if (realnessT < 1f)
            {
                realnessT = stepDuration > 0f ? Mathf.Min(1f, realnessT + Time.deltaTime / stepDuration) : 1f;
                float e = realnessT * realnessT * (3f - 2f * realnessT);
                realnessNow = Mathf.Lerp(realnessFrom, realnessTo, e);
                ctx.morpher.Realness = realnessNow;
                RefreshPanel();
            }

            if (finishing)
            {
                completeTimer -= Time.deltaTime;
                if (completeTimer <= 0f)
                {
                    finishing = false;
                    Complete();
                }
            }
        }

        void HandleTap(int id)
        {
            if (!IsActive || ctx.morpher == null || ctx.morpher.IsMorphing) return;
            id = ResolveBugTap(id);
            var node = ctx.Node(id);
            if (node == null) return;

            if (node.organ == Organ.Bug)
            {
                if (removed.Contains(id)) return;
                RemoveBug(id);
                return;
            }
        }

        // 右键查看详情；叶片下面的虫子同样按 ResolveBugTap 优先
        bool ProvideInspect(int id, out string title, out string body)
        {
            title = body = null;
            if (ctx.morpher != null && ctx.morpher.IsMorphing) return false;
            var node = ctx.Node(ResolveBugTap(id));
            if (node == null) return false;
            title = BugStageUtil.Title(ctx.detailTable, node, NameKind.Real);
            body = BugStageUtil.PhysicalBody(ctx.detailTable, node);
            return true;
        }

        // Geometric 形态下叶片很大，叶片的拾取球（半径约 6 cm）把贴在它上面的虫子（距叶心 1–2 cm）整个包住，
        // NodePicker 总是先命中叶片，虫子永远点不到。这里在本关内修正：点到的不是虫子时，沿同一条射线只找虫子，
        // 满足下面两条就算点到虫子：
        //   ① 虫背朝向相机（虫子在叶背，背 = 节点 up；要把叶背转过来才点得到）；
        //   ② 射线上没有别的节点（除了它所在的叶片）明显挡在前面。
        int ResolveBugTap(int tappedId)
        {
            var tapped = ctx.Node(tappedId);
            if (tapped == null || tapped.organ == Organ.Bug || ctx.picker == null || ctx.pointer == null) return tappedId;
            var morpher = ctx.morpher;
            var poses = morpher.CurrentPoses;
            var set = morpher.nodeSet;
            if (poses == null || set == null) return tappedId;
            Ray ray = ctx.pointer.CurrentRay;
            Transform t = morpher.transform;

            int bug = ctx.picker.Pick(ray, i =>
            {
                if (set.nodes[i].organ != Organ.Bug || removed.Contains(i)) return false;
                Vector3 up = t.rotation * (poses[i].rotation * Vector3.up);
                Vector3 toEye = ray.origin - t.TransformPoint(poses[i].position);
                return Vector3.Dot(up, toEye.normalized) >= bugFacingThreshold;
            }, out float bugDist);
            if (bug < 0) return tappedId;

            int parent = set.nodes[bug].parentId;
            int blocker = ctx.picker.Pick(ray, i => i != parent && set.nodes[i].organ != Organ.Bug, out float blockDist);
            if (blocker >= 0 && blockDist < bugDist - occluderMargin) return tappedId;
            return bug;
        }

        // 摘除：节点闪一下并缩小隐藏（G3 Hide 动画，虫子模型跟着缩没），写实度上升一档
        void RemoveBug(int id)
        {
            removed.Add(id);
            ctx.morpher.SetHighlight(id, removeFlashColor);
            ctx.morpher.Hide(id);
            if (ctx.detailPopup != null) ctx.detailPopup.Hide();

            float target = RealnessFor(removed.Count);
            realnessFrom = realnessNow;
            realnessTo = target;
            realnessT = 0f;
            RefreshPanel();

            if (removed.Count == 1 && removed.Count < bugNodes.Count) ctx.Play(firstRemovedSequence);
            if (removed.Count >= bugNodes.Count)
            {
                finishing = true;
                completeTimer = Mathf.Max(completeDelay, stepDuration);
            }
        }

        // 摘除 n 只后的写实度：从 startRealness 到 endRealness 平均分档
        public float RealnessFor(int removedCount)
        {
            if (bugNodes.Count == 0) return endRealness;
            return Mathf.Lerp(startRealness, endRealness, Mathf.Clamp01((float)removedCount / bugNodes.Count));
        }

        void SetupTaskPanel()
        {
            if (ctx.taskPanel == null) return;
            ctx.ShowTaskPanel();
            ctx.taskPanel.SetTitle("S4 · 手动除虫");
            // 【占位】玩家亲手操作，Agent 不再代理
            ctx.taskPanel.SetModeLabel("MANUAL");
            ctx.taskPanel.SetTasks(new[]
            {
                new AgentTask("摘除异常个体", TaskState.Running),
            });
            RefreshPanel();
        }

        void RefreshPanel()
        {
            if (ctx.taskPanel == null) return;
            bool all = bugNodes.Count > 0 && removed.Count >= bugNodes.Count;
            ctx.taskPanel.UpdateTask(0, all ? TaskState.Done : TaskState.Running);
            float rate = bugNodes.Count == 0 ? 0f : (float)removed.Count / bugNodes.Count;
            ctx.taskPanel.SetMetric("已摘除", rate, $"{removed.Count} / {bugNodes.Count}");
            ctx.taskPanel.SetMetric("写实度", realnessNow);
        }
    }
}
