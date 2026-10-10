using System;
using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Core;
using Ghost.Gameplay;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Narrative;
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

        [Header("输入")]
        public PointerInput pointer;
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
