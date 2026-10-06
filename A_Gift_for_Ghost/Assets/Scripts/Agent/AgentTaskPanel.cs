using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Agent
{
    public enum TaskState
    {
        Pending,
        Running,
        Done,
        Failed,
    }

    [Serializable]
    public struct AgentTask
    {
        public string label;
        public TaskState state;

        public AgentTask(string label, TaskState state = TaskState.Pending)
        {
            this.label = label;
            this.state = state;
        }
    }

    // Agent 的任务列表 + 指标面板（World Space）。只负责显示，数据全部由阶段脚本推送：
    //   SetTitle / SetModeLabel / SetTasks / UpdateTask / SetMetric
    // 行和指标在运行时按需生成，高度随内容自动变化（顶边不动）。
    public class AgentTaskPanel : MonoBehaviour
    {
        [Header("引用（AgentUIBuilder 会填好）")]
        public RectTransform panel;
        public Text headerLabel;
        public Text modeLabel;
        public Image modeChip;
        public Text titleLabel;
        public RectTransform content;

        [Header("文字")]
        public string header = "AGENT · TASKS";
        public string modeText = "FULL PROXY";
        public Font font;

        [Header("布局（像素，1000 px = 1 m）")]
        public float width = 600f;
        public float padding = 32f;
        public float headerHeight = 132f;
        public float rowMinHeight = 56f;
        public float rowSpacing = 6f;
        public float sectionGap = 22f;
        public float metricHeight = 76f;
        public int taskFontSize = 38;
        public int metricFontSize = 32;

        [Header("颜色")]
        public Color inkColor = AgentUIStyle.Ink;
        public Color mutedColor = AgentUIStyle.Gray;
        public Color trackColor = AgentUIStyle.LightGray;
        public Color accentColor = AgentUIStyle.BlueGray;
        public Color failedColor = AgentUIStyle.Failed;

        [Tooltip("进行中的任务方块轻微呼吸（不闪烁，频率低）")]
        public bool pulseRunning = true;

        public event Action<int, TaskState> TaskChanged;

        public int TaskCount => tasks.Count;
        public bool Visible => panel != null && panel.gameObject.activeSelf;

        class TaskRow
        {
            public RectTransform root;
            public Image marker;
            public Image markerHole;
            public Text label;
            public Text state;
        }

        class MetricRow
        {
            public string key;
            public RectTransform root;
            public Text label;
            public Text value;
            public RectTransform track;
            public RectTransform fill;
            public bool hasBar;
            public float value01;
        }

        readonly List<AgentTask> tasks = new List<AgentTask>();
        readonly List<TaskRow> taskRows = new List<TaskRow>();
        readonly List<MetricRow> metrics = new List<MetricRow>();
        RectTransform divider;
        bool dirty = true;

        void Awake()
        {
            if (panel == null || content == null)
            {
                Debug.LogError("[Agent] AgentTaskPanel 缺少 panel 或 content，请用 AgentUIBuilder 生成", this);
                enabled = false;
                return;
            }
            if (headerLabel != null) headerLabel.text = header;
            SetModeLabel(modeText);
        }

        void LateUpdate()
        {
            if (dirty) Relayout();
            if (!pulseRunning) return;
            float a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 1.2f));
            for (int i = 0; i < taskRows.Count; i++)
            {
                if (tasks[i].state != TaskState.Running) continue;
                var c = accentColor;
                c.a *= a;
                taskRows[i].marker.color = c;
            }
        }

        // ---- 公共接口 ----

        public void Show() { if (panel != null) panel.gameObject.SetActive(true); }
        public void Hide() { if (panel != null) panel.gameObject.SetActive(false); }

        public void SetTitle(string title)
        {
            if (titleLabel != null) titleLabel.text = title ?? "";
            dirty = true;
        }

        // 右上角的模式标签，比如 "FULL PROXY" / "ASSISTIVE"。传 null 或空串隐藏
        public void SetModeLabel(string mode)
        {
            modeText = mode;
            bool show = !string.IsNullOrEmpty(mode);
            if (modeLabel != null) modeLabel.text = show ? mode : "";
            if (modeChip != null) modeChip.gameObject.SetActive(show);
        }

        public void SetTasks(IList<AgentTask> list)
        {
            tasks.Clear();
            if (list != null) tasks.AddRange(list);
            while (taskRows.Count > tasks.Count)
            {
                Destroy(taskRows[taskRows.Count - 1].root.gameObject);
                taskRows.RemoveAt(taskRows.Count - 1);
            }
            while (taskRows.Count < tasks.Count) taskRows.Add(CreateTaskRow());
            for (int i = 0; i < tasks.Count; i++) ApplyTask(i);
            dirty = true;
        }

        // 追加一条任务，返回下标
        public int AddTask(string label, TaskState state = TaskState.Pending)
        {
            tasks.Add(new AgentTask(label, state));
            taskRows.Add(CreateTaskRow());
            ApplyTask(tasks.Count - 1);
            dirty = true;
            return tasks.Count - 1;
        }

        public void UpdateTask(int index, TaskState state)
        {
            if (!ValidIndex(index)) return;
            var t = tasks[index];
            if (t.state == state) return;
            t.state = state;
            tasks[index] = t;
            ApplyTask(index);
            TaskChanged?.Invoke(index, state);
        }

        public void UpdateTask(int index, string label, TaskState state)
        {
            if (!ValidIndex(index)) return;
            var t = tasks[index];
            t.label = label;
            tasks[index] = t;
            ApplyTask(index);
            dirty = true;
            UpdateTask(index, state);
        }

        public AgentTask GetTask(int index) => ValidIndex(index) ? tasks[index] : default;

        public void ClearTasks() => SetTasks(null);

        // 带进度条的指标：value01 为 0–1，右侧显示百分比
        public void SetMetric(string key, float value01) => SetMetric(key, value01, null);

        // 带进度条的指标，右侧显示自定义文字（如 "3 / 7"）。display 为 null 时显示百分比
        public void SetMetric(string key, float value01, string display)
        {
            var m = GetOrCreateMetric(key, true);
            m.value01 = Mathf.Clamp01(value01);
            m.value.text = display ?? Mathf.RoundToInt(m.value01 * 100f) + "%";
            ApplyBar(m);
        }

        // 纯文字指标（没有进度条），比如 "置信度  低"
        public void SetMetric(string key, string value)
        {
            var m = GetOrCreateMetric(key, false);
            m.value.text = value ?? "";
        }

        public bool RemoveMetric(string key)
        {
            int i = metrics.FindIndex(m => m.key == key);
            if (i < 0) return false;
            Destroy(metrics[i].root.gameObject);
            metrics.RemoveAt(i);
            dirty = true;
            return true;
        }

        public void ClearMetrics()
        {
            foreach (var m in metrics) Destroy(m.root.gameObject);
            metrics.Clear();
            dirty = true;
        }

        // ---- 内部 ----

        bool ValidIndex(int index)
        {
            if (index >= 0 && index < tasks.Count) return true;
            Debug.LogWarning($"[Agent] 任务下标越界：{index}（共 {tasks.Count} 条）", this);
            return false;
        }

        Font UsedFont => font != null ? font : AgentUIStyle.DefaultFont;
        float InnerWidth => width - padding * 2f;

        TaskRow CreateTaskRow()
        {
            var row = new TaskRow();
            row.root = AgentUIStyle.CreateRect("Task", content, Vector2.zero, new Vector2(InnerWidth, rowMinHeight));
            const float marker = 22f;
            var markerRect = AgentUIStyle.CreateRect("Marker", row.root, new Vector2(0f, -(rowMinHeight - marker) * 0.5f),
                new Vector2(marker, marker));
            row.marker = AgentUIStyle.AddImage(markerRect, inkColor);
            var hole = AgentUIStyle.CreateStretch("Hole", markerRect, 3f);
            row.markerHole = AgentUIStyle.AddImage(hole, AgentUIStyle.PanelFill);

            float stateWidth = 150f;
            var labelRect = AgentUIStyle.CreateRect("Label", row.root, new Vector2(marker + 18f, 0f),
                new Vector2(InnerWidth - marker - 18f - stateWidth, rowMinHeight));
            row.label = AgentUIStyle.AddText(labelRect, "", taskFontSize, inkColor, TextAnchor.MiddleLeft, FontStyle.Normal, UsedFont);
            var stateRect = AgentUIStyle.CreateRect("State", row.root, new Vector2(InnerWidth - stateWidth, 0f),
                new Vector2(stateWidth, rowMinHeight));
            row.state = AgentUIStyle.AddText(stateRect, "", Mathf.RoundToInt(taskFontSize * 0.7f), mutedColor,
                TextAnchor.MiddleRight, FontStyle.Bold, UsedFont);
            return row;
        }

        void ApplyTask(int i)
        {
            var t = tasks[i];
            var row = taskRows[i];
            row.label.text = t.label ?? "";
            // 方块标记：未开始 = 空心灰；进行中 = 实心蓝灰；完成 = 实心黑；失败 = 空心暗红
            switch (t.state)
            {
                case TaskState.Pending:
                    row.marker.color = mutedColor;
                    row.markerHole.enabled = true;
                    row.label.color = mutedColor;
                    row.state.text = "PENDING";
                    row.state.color = mutedColor;
                    break;
                case TaskState.Running:
                    row.marker.color = accentColor;
                    row.markerHole.enabled = false;
                    row.label.color = inkColor;
                    row.state.text = "RUNNING";
                    row.state.color = accentColor;
                    break;
                case TaskState.Done:
                    row.marker.color = inkColor;
                    row.markerHole.enabled = false;
                    row.label.color = inkColor;
                    row.state.text = "DONE";
                    row.state.color = inkColor;
                    break;
                case TaskState.Failed:
                    row.marker.color = failedColor;
                    row.markerHole.enabled = true;
                    row.label.color = failedColor;
                    row.state.text = "FAILED";
                    row.state.color = failedColor;
                    break;
            }
        }

        MetricRow GetOrCreateMetric(string key, bool bar)
        {
            var m = metrics.Find(x => x.key == key);
            if (m == null)
            {
                m = new MetricRow { key = key };
                m.root = AgentUIStyle.CreateRect("Metric " + key, content, Vector2.zero, new Vector2(InnerWidth, metricHeight));
                var labelRect = AgentUIStyle.CreateRect("Label", m.root, Vector2.zero, new Vector2(InnerWidth * 0.62f, 42f));
                m.label = AgentUIStyle.AddText(labelRect, key, metricFontSize, mutedColor, TextAnchor.MiddleLeft, FontStyle.Normal, UsedFont);
                var valueRect = AgentUIStyle.CreateRect("Value", m.root, new Vector2(InnerWidth * 0.62f, 0f),
                    new Vector2(InnerWidth * 0.38f, 42f));
                m.value = AgentUIStyle.AddText(valueRect, "", metricFontSize, inkColor, TextAnchor.MiddleRight, FontStyle.Bold, UsedFont);
                m.track = AgentUIStyle.CreateRect("Track", m.root, new Vector2(0f, -52f), new Vector2(InnerWidth, 8f));
                AgentUIStyle.AddImage(m.track, trackColor);
                m.fill = AgentUIStyle.CreateRect("Fill", m.track, Vector2.zero, new Vector2(0f, 8f));
                AgentUIStyle.AddImage(m.fill, accentColor);
                metrics.Add(m);
                dirty = true;
            }
            if (m.hasBar != bar)
            {
                m.hasBar = bar;
                m.track.gameObject.SetActive(bar);
                dirty = true;
            }
            return m;
        }

        void ApplyBar(MetricRow m)
        {
            m.fill.sizeDelta = new Vector2(InnerWidth * m.value01, m.fill.sizeDelta.y);
        }

        // 从上往下排：标题区 → 任务行 → 分隔线 → 指标。面板高度跟着内容变（pivot 在顶边，顶边不动）
        void Relayout()
        {
            dirty = false;
            float y = 0f;
            for (int i = 0; i < taskRows.Count; i++)
            {
                var row = taskRows[i];
                float h = Mathf.Max(rowMinHeight, row.label.preferredHeight + 8f);
                row.root.sizeDelta = new Vector2(InnerWidth, h);
                row.label.rectTransform.sizeDelta = new Vector2(row.label.rectTransform.sizeDelta.x, h);
                row.state.rectTransform.sizeDelta = new Vector2(row.state.rectTransform.sizeDelta.x, h);
                ((RectTransform)row.marker.transform).anchoredPosition = new Vector2(0f, -(h - 22f) * 0.5f);
                row.root.anchoredPosition = new Vector2(0f, -y);
                y += h + rowSpacing;
            }

            if (metrics.Count > 0)
            {
                if (divider == null)
                {
                    divider = AgentUIStyle.CreateRect("Divider", content, Vector2.zero, new Vector2(InnerWidth, 2f));
                    AgentUIStyle.AddImage(divider, trackColor);
                }
                divider.gameObject.SetActive(true);
                y += sectionGap * 0.5f;
                divider.anchoredPosition = new Vector2(0f, -y);
                y += sectionGap;
                foreach (var m in metrics)
                {
                    m.root.anchoredPosition = new Vector2(0f, -y);
                    y += m.hasBar ? metricHeight : 50f;
                }
            }
            else if (divider != null) divider.gameObject.SetActive(false);

            content.sizeDelta = new Vector2(InnerWidth, y);
            float height = headerHeight + y + padding;
            panel.sizeDelta = new Vector2(width, height);
        }
    }
}
