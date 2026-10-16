using System;
using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Gameplay;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Narrative;
using PrimeTween;
using UnityEngine;

namespace Ghost.Stages
{
    // 各阶段共用的场景引用和小工具。由 MainSceneMenu 生成场景时挂在 GameFlow 物体上并填好引用，
    // 阶段脚本只需要引用这一个组件，保持精简。
    public class StageContext : MonoBehaviour
    {
        [Header("流程")]
        public GameFlow flow;

        [Header("植株")]
        public NodeMorpher morpher;
        public NodeLinkRenderer links;
        [Tooltip("节点外发光圈（S2 脉冲）。留空时 S2 运行时在 morpher 物体上补一个")]
        public NodeHaloRenderer halos;
        public NodeIssueSystem issues;
        [Tooltip("构图视点来源（ViewerPosition 用）。留空时运行时在场景里找")]
        public PlantFit plantFit;

        [Header("缺失节点（S1 浮出 / S2 补缺口）")]
        [Tooltip("可选覆盖，按 Leaf / Stem / Fruit / Root 顺序；某一位 < 0 = 自动挑选（MissingNodePicker）")]
        public List<int> missingNodeOverrides = new List<int>();

        [Header("输入")]
        public InteractionSource pointer;
        public NodePicker picker;
        [Tooltip("G8：旋转植株（S3 / S4 共用）")]
        public TargetRotator rotator;

        [Header("Agent 界面（G4）")]
        [Tooltip("任务面板暂不使用：false 时各阶段调 ShowTaskPanel() 不会显示（代码保留，以后再开）")]
        public bool showTaskPanel = false;
        public AgentTaskPanel taskPanel;
        public NodeDetailPopup detailPopup;
        public AgentQueryDialog query;

        [Header("对白与详情（G5）")]
        public DialoguePlayer dialogue;
        [Tooltip("字幕 / Agent 弹窗；阶段用它固定一条常驻的 Agent 消息（S4 除虫进度）")]
        public SubtitlePanel subtitles;
        public NodeDetailTable detailTable;

        [Header("点击反馈")]
        [Tooltip("左键点中节点时的脉冲：瞬间亮到 pulseColor，再在这么多秒内回落")]
        public float tapPulseDuration = 0.6f;
        public Color tapPulseColor = Color.white;

        [Tooltip("节点显示缩放小于这个值时不参与拾取（被 Hide 的节点、写实交接时缩没的节点）")]
        [Range(0f, 1f)] public float minPickVisibility = 0.5f;

        // 默认拾取筛选：跳过隐藏节点。阶段需要额外筛选时用 SetPickFilter，离开时 ResetShared 会恢复默认
        public Func<int, bool> DefaultFilter { get; private set; }

        // 右键查看详情：当前阶段提供标题和正文（返回 false = 这个节点不显示详情）。阶段 Enter 时设置，ResetShared 清空
        public delegate bool InspectProvider(int id, out string title, out string body);
        InspectProvider inspectProvider;

        List<int> missingNodes;
        NodePose[] missingScatter;
        NodePose[] handoffFrom;
        Color handoffGlow;
        Tween missingHandoff;
        // 右键查看了某个节点（S2 "已查看果实"用）
        public event Action<int> Inspected;

        void Awake()
        {
            DefaultFilter = IsNodeVisible;
            if (picker != null) picker.Filter = DefaultFilter;
        }

        void OnEnable()
        {
            if (pointer == null) return;
            pointer.Tap += HandleTapPulse;
            pointer.InspectStart += HandleInspectStart;
            pointer.InspectEnd += HandleInspectEnd;
        }

        void OnDisable()
        {
            if (pointer == null) return;
            pointer.Tap -= HandleTapPulse;
            pointer.InspectStart -= HandleInspectStart;
            pointer.InspectEnd -= HandleInspectEnd;
        }

        // 所有阶段统一的左键点击反馈
        void HandleTapPulse(int id)
        {
            if (morpher != null && tapPulseDuration > 0f) morpher.Pulse(id, tapPulseDuration, tapPulseColor);
        }

        // 阶段设置右键详情内容；传 null 关闭右键详情
        public void SetInspectProvider(InspectProvider provider)
        {
            inspectProvider = provider;
        }

        void HandleInspectStart(int id)
        {
            if (inspectProvider == null || detailPopup == null) return;
            if (!inspectProvider(id, out string title, out string body)) return;
            detailPopup.Show(id, title, body);
            Inspected?.Invoke(id);
        }

        void HandleInspectEnd()
        {
            if (detailPopup != null) detailPopup.Release();
        }

        // 详情弹窗正在显示时刷新文字（内容随问题状态变化）
        public void RefreshInspect()
        {
            if (inspectProvider == null || detailPopup == null || !detailPopup.Visible || detailPopup.NodeId < 0) return;
            if (inspectProvider(detailPopup.NodeId, out string title, out string body)) detailPopup.SetText(title, body);
        }

        // 尊重 showTaskPanel 开关
        public void ShowTaskPanel()
        {
            if (taskPanel == null) return;
            if (showTaskPanel) taskPanel.Show();
            else taskPanel.Hide();
        }

        public bool IsNodeVisible(int id)
        {
            return morpher == null || !morpher.IsReady || morpher.GetNodeVisibility(id) >= minPickVisibility;
        }

        // 额外筛选（和"跳过隐藏节点"同时生效）。传 null 恢复默认
        public void SetPickFilter(Func<int, bool> filter)
        {
            if (picker == null) return;
            picker.Filter = filter == null ? DefaultFilter : id => IsNodeVisible(id) && filter(id);
        }

        // "玩家所在的方向"统一从这里取（世界坐标）：PlantFit 的构图视点（viewPoint，VR 下是 ViewAnchor，不跟头），
        // 没有时用它的 viewCamera。玩法里不要直接用 Camera.main
        public Vector3 ViewerPosition()
        {
            if (plantFit == null) plantFit = FindFirstObjectByType<PlantFit>();
            if (plantFit != null)
            {
                if (plantFit.viewPoint != null) return plantFit.viewPoint.position;
                if (plantFit.viewCamera != null) return plantFit.viewCamera.transform.position;
            }
            Debug.LogWarning("[Stage] ViewerPosition：找不到 PlantFit 视点，返回植株前方", this);
            return morpher != null ? morpher.transform.position - morpher.transform.forward : Vector3.zero;
        }

        // ViewerPosition 换到植株本地空间（NodePose 所在空间）
        public Vector3 ViewerPositionLocal()
        {
            Vector3 world = ViewerPosition();
            return morpher != null ? morpher.transform.InverseTransformPoint(world) : world;
        }

        // S1 / S2 共用的 4 个缺失节点（Leaf / Stem / Fruit / Root），第一次用时挑选并缓存，之后每次一样。
        // 需要 NodeLinkRenderer 已初始化（Start 之后）；还没好时返回空列表、不缓存
        public IReadOnlyList<int> MissingNodes()
        {
            if (missingNodes != null) return missingNodes;
            if (morpher == null || morpher.nodeSet == null || links == null || !links.IsReady) return Array.Empty<int>();
            missingNodes = MissingNodePicker.Pick(morpher.nodeSet, links.HasLink, missingNodeOverrides);
            missingScatter = MissingNodePicker.ScatterPoses(morpher.nodeSet, missingNodes);
            return missingNodes;
        }

        // 缺失节点在 S2 的散落姿态（植株本地空间），找不到时返回 false
        public bool MissingScatterPose(int id, out NodePose pose)
        {
            pose = default;
            var ids = MissingNodes();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != id) continue;
                pose = missingScatter[i];
                return true;
            }
            return false;
        }

        // 节点外发光圈：场景里没配时在 morpher 物体上补一个
        public NodeHaloRenderer EnsureHalos()
        {
            if (halos != null) return halos;
            if (morpher == null) return null;
            halos = morpher.GetComponent<NodeHaloRenderer>();
            if (halos == null) halos = morpher.gameObject.AddComponent<NodeHaloRenderer>();
            return halos;
        }

        // S1 通关进 S2：缺失节点从当前位置（浮出姿态）缓动到散落位置，外发光同时淡出。
        // 和 GameFlow 的变形同时进行；姿态覆盖在变形中照常叠加，所以这 4 个节点不会先变成回路姿态。
        // 散落的姿态覆盖保留到 ClearMissingNodeVisuals（S2 离开 / 重新进 S1）
        public void HandOffMissingToScatter(float duration, Color glowColor)
        {
            missingHandoff.Stop();
            if (morpher == null || morpher.CurrentPoses == null) return;
            var ids = MissingNodes();
            if (ids.Count == 0) return;
            if (handoffFrom == null || handoffFrom.Length != ids.Count) handoffFrom = new NodePose[ids.Count];
            for (int i = 0; i < ids.Count; i++) handoffFrom[i] = morpher.CurrentPoses[ids[i]];
            handoffGlow = glowColor;
            EnsureHalos();
            missingHandoff = Tween.Custom(this, 0f, 1f, Mathf.Max(0.01f, duration), (self, t) => self.ApplyHandoff(t), Ease.InOutCubic);
        }

        // S1 → S2 的交接缓动还在进行（S2 期间不接受拖拽）
        public bool IsHandingOff => missingHandoff.isAlive;

        void ApplyHandoff(float t)
        {
            var ids = MissingNodes();
            for (int i = 0; i < ids.Count && i < handoffFrom.Length; i++)
            {
                morpher.SetPoseOverride(ids[i], NodePose.Lerp(handoffFrom[i], missingScatter[i], t), 1f);
                if (halos == null) continue;
                if (t < 1f) halos.SetGlow(ids[i], handoffGlow, 1f - t);
                else halos.Clear(ids[i]);
            }
        }

        // 清掉缺失节点留下的一切：姿态覆盖、节点外发光、缺口发光、隐藏 / 生长中的连线。离开 S1 / S2（非交接）时调用
        public void ClearMissingNodeVisuals()
        {
            missingHandoff.Stop();
            if (missingNodes == null) return;
            foreach (int id in missingNodes)
            {
                if (morpher != null) morpher.ClearPoseOverride(id);
                if (halos != null)
                {
                    halos.Clear(id);
                    halos.ClearAt(id);
                }
                if (links != null) links.SetNodeLinksGrowth(id, 1f);
            }
        }

        public PlantNode Node(int id)
        {
            return morpher != null && morpher.nodeSet != null ? morpher.nodeSet.Get(id) : null;
        }

        // 播一段对白；没有播放器或序列时直接回调
        public void Play(DialogueSequence sequence, Action onComplete = null)
        {
            if (dialogue == null || sequence == null)
            {
                onComplete?.Invoke();
                return;
            }
            dialogue.Play(sequence, onComplete);
        }

        // 离开阶段时调用：清掉问题和闪烁、隐藏弹窗 / 询问框 / 任务面板、停止对白、恢复拾取筛选
        public void ResetShared()
        {
            if (issues != null) issues.ClearAll();
            if (detailPopup != null) detailPopup.Hide();
            if (query != null) query.Hide();
            if (dialogue != null) dialogue.Stop();
            if (subtitles != null) subtitles.ClearAgentFeed();
            if (taskPanel != null)
            {
                taskPanel.ClearTasks();
                taskPanel.ClearMetrics();
                taskPanel.Hide();
            }
            if (picker != null) picker.Filter = DefaultFilter;
            inspectProvider = null;
        }

        // 在某个形态的布局里，找离归一化位置（0–1，按该形态所有节点的 XY 包围盒）最近的节点。
        // 平面形态（Matrix / Circuit）面朝 -Z，x 向右、y 向上，和固定相机的画面方向一致。
        public int NearestNodeInLayout(MorphForm form, Vector2 normalized, Predicate<PlantNode> filter = null,
            ICollection<int> exclude = null)
        {
            if (morpher == null || morpher.nodeSet == null) return -1;
            var nodes = morpher.nodeSet.nodes;
            if (nodes.Count == 0) return -1;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (var n in nodes)
            {
                Vector3 p = n.GetPose(form).position;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 target = new Vector2(Mathf.Lerp(min.x, max.x, normalized.x), Mathf.Lerp(min.y, max.y, normalized.y));

            int best = -1;
            float bestDist = float.MaxValue;
            foreach (var n in nodes)
            {
                if (filter != null && !filter(n)) continue;
                if (exclude != null && exclude.Contains(n.id)) continue;
                float d = ((Vector2)n.GetPose(form).position - target).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n.id;
                }
            }
            return best;
        }
    }
}
