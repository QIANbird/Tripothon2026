using UnityEngine;

namespace Ghost.Morph
{
    // 节点的显示状态：玩法层（NodeIssueSystem、各阶段）用来闪烁、高亮、着色、隐藏单个节点，以及整体调写实度。
    // 只影响显示，不改节点数据，也不打断形态变形。
    public enum NodeVisualState
    {
        // 按形态正常显示
        Normal,
        // 在原色和闪烁色之间来回切换（默认白色）
        Blink,
        // 常亮高亮（默认白色）
        Highlight,
        // 染成指定颜色，比如问题解决后的"恢复色"
        Tint,
    }

    public partial class NodeMorpher
    {
        [Header("节点状态")]
        [Tooltip("闪烁的默认频率（次/秒）")]
        public float defaultBlinkFrequency = 1.5f;
        [Tooltip("闪烁、高亮的默认颜色")]
        public Color defaultHighlightColor = Color.white;
        [Tooltip("高亮、着色时目标色的占比，1 = 完全换成目标色")]
        [Range(0f, 1f)] public float highlightStrength = 0.85f;
        [Tooltip("隐藏 / 显示单个节点的缩放动画时长（秒）")]
        public float hideDuration = 0.35f;
        [Tooltip("调整整体写实度时颜色过渡的速度（每秒变化量）")]
        public float realnessSpeed = 1.5f;

        NodeVisualState[] visualStates;
        Color[] stateColors;
        float[] blinkFrequencies;
        float[] stateStartTimes;
        // 状态切换时从上一个显示色淡入，避免颜色跳变
        Color[] stateFromColors;
        float[] visibility;
        float[] visibilityTarget;

        bool realnessOverride;
        float realnessTarget;
        float realnessCurrent;
        float realnessWeight;

        // 状态切换时颜色淡入的时长（秒）
        const float StateFadeDuration = 0.15f;

        public bool IsReady => visualStates != null;

        // 整体写实度（0 = 纯灰阶，1 = 写实色）。设置后覆盖形态自带的写实度，S4 用它逐档变鲜艳；ClearRealness 恢复按形态
        public float Realness
        {
            get => realnessOverride ? realnessTarget : OrganPalette.Realness(CurrentForm);
            set
            {
                if (!realnessOverride) realnessCurrent = OrganPalette.Realness(CurrentForm);
                realnessOverride = true;
                realnessTarget = Mathf.Clamp01(value);
            }
        }

        public bool HasRealnessOverride => realnessOverride;

        public void ClearRealness()
        {
            realnessOverride = false;
        }

        // 立即设置写实度，不播过渡
        public void SetRealnessImmediate(float value)
        {
            Realness = value;
            realnessCurrent = realnessTarget;
            realnessWeight = 1f;
        }

        public NodeVisualState GetState(int id)
        {
            return IsValid(id) ? visualStates[id] : NodeVisualState.Normal;
        }

        // 在原色和 color（默认白色）之间闪烁。frequency <= 0 时用 defaultBlinkFrequency
        public void SetBlink(int id, float frequency = 0f, Color? color = null)
        {
            if (!IsValid(id)) return;
            blinkFrequencies[id] = frequency > 0f ? frequency : defaultBlinkFrequency;
            SetState(id, NodeVisualState.Blink, color ?? defaultHighlightColor);
        }

        // 常亮高亮
        public void SetHighlight(int id, Color? color = null)
        {
            if (!IsValid(id)) return;
            SetState(id, NodeVisualState.Highlight, color ?? defaultHighlightColor);
        }

        // 染成指定颜色（恢复色、标记色等）
        public void SetTint(int id, Color color)
        {
            if (!IsValid(id)) return;
            SetState(id, NodeVisualState.Tint, color);
        }

        // 恢复按形态正常显示（停止闪烁、取消高亮和着色）
        public void Restore(int id)
        {
            if (!IsValid(id)) return;
            SetState(id, NodeVisualState.Normal, Color.clear);
        }

        public void RestoreAll()
        {
            if (!IsReady) return;
            for (int i = 0; i < Count; i++) Restore(i);
        }

        // 隐藏（缩小动画）。隐藏中的节点不应再被拾取，见 GetNodeVisibility
        public void Hide(int id, bool immediate = false)
        {
            SetVisible(id, false, immediate);
        }

        public void Show(int id, bool immediate = false)
        {
            SetVisible(id, true, immediate);
        }

        public void SetVisible(int id, bool visible, bool immediate = false)
        {
            if (!IsValid(id)) return;
            visibilityTarget[id] = visible ? 1f : 0f;
            if (immediate) visibility[id] = visibilityTarget[id];
        }

        public bool IsHidden(int id)
        {
            return IsValid(id) && visibilityTarget[id] <= 0f;
        }

        // 节点当前的显示缩放系数（0–1）：隐藏动画 × 写实交接时的缩小。拾取时小于约 0.5 的节点应跳过
        public float GetNodeVisibility(int id)
        {
            return IsValid(id) ? DisplayScale(id) : 0f;
        }

        // 节点当前的世界空间包围球（拾取用）。节点不可见时返回 false
        public bool TryGetNodeWorldSphere(int id, out Vector3 center, out float radius, float minRadius = 0.01f)
        {
            center = Vector3.zero;
            radius = 0f;
            if (!IsValid(id)) return false;
            float k = DisplayScale(id);
            if (k <= 0.001f) return false;

            var pose = currentPoses[id];
            Vector3 lossy = transform.lossyScale;
            Vector3 s = Vector3.Scale(pose.scale * k, lossy);
            center = transform.TransformPoint(pose.position);
            radius = Mathf.Max(Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 0.5f, minRadius);
            return true;
        }

        bool IsValid(int id)
        {
            return IsReady && id >= 0 && id < Count;
        }

        void InitNodeStates()
        {
            visualStates = new NodeVisualState[Count];
            stateColors = new Color[Count];
            blinkFrequencies = new float[Count];
            stateStartTimes = new float[Count];
            stateFromColors = new Color[Count];
            visibility = new float[Count];
            visibilityTarget = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                visibility[i] = 1f;
                visibilityTarget[i] = 1f;
            }
        }

        void SetState(int id, NodeVisualState state, Color color)
        {
            // 从当前看到的颜色淡入新状态
            stateFromColors[id] = DisplayColor(id);
            visualStates[id] = state;
            stateColors[id] = color;
            stateStartTimes[id] = Time.time;
        }

        void UpdateNodeStates(float deltaTime)
        {
            float step = hideDuration > 0f ? deltaTime / hideDuration : 1f;
            for (int i = 0; i < Count; i++)
                visibility[i] = Mathf.MoveTowards(visibility[i], visibilityTarget[i], step);

            realnessWeight = Mathf.MoveTowards(realnessWeight, realnessOverride ? 1f : 0f, deltaTime * realnessSpeed * 2f);
            if (realnessOverride)
                realnessCurrent = Mathf.MoveTowards(realnessCurrent, realnessTarget, deltaTime * realnessSpeed);
        }

        float DisplayScale(int i)
        {
            float k = RealReveal > 0f ? RevealShrink(realHeights[i]) : 1f;
            float v = visibility[i];
            return k * v * v * (3f - 2f * v);
        }

        Color BaseColor(int i)
        {
            Color c = currentColors[i];
            if (realnessWeight > 0f)
                c = Color.Lerp(c, OrganPalette.ColorAt(nodeSet.nodes[i], realnessCurrent), realnessWeight);
            return c;
        }

        Color DisplayColor(int i)
        {
            Color baseColor = BaseColor(i);
            Color target;
            switch (visualStates[i])
            {
                case NodeVisualState.Blink:
                {
                    // 从原色开始，按余弦在原色和闪烁色之间往返
                    float t = (Time.time - stateStartTimes[i]) * blinkFrequencies[i];
                    float pulse = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
                    target = Color.Lerp(baseColor, stateColors[i], pulse * highlightStrength);
                    break;
                }
                case NodeVisualState.Highlight:
                case NodeVisualState.Tint:
                    target = Color.Lerp(baseColor, stateColors[i], highlightStrength);
                    break;
                default:
                    target = baseColor;
                    break;
            }

            float fade = Mathf.Clamp01((Time.time - stateStartTimes[i]) / StateFadeDuration);
            if (fade < 1f && stateStartTimes[i] > 0f) target = Color.Lerp(stateFromColors[i], target, fade);
            target.a = 1f;
            return target;
        }
    }
}
