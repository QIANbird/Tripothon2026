using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Morph
{
    // 每个形态自动适配画面：Plant 的父物体（PlantFit）做"缩放 + 平移"，让目标形态的主体落在固定相机画面中央。
    // 相机不动（docs/VR_GUIDELINES.md 第 3 节：不强制移动玩家镜头），只改植株的尺度和位置，VR 迁移时也不用改相机。
    //
    // 平面形态（Matrix / Circuit）：用全部节点的 AABB（含节点自身半尺寸）。
    // 立体形态（Network / Geometric / Real）：用包围球，S3 / S4 旋转后仍全部入画。
    // 过渡：订阅 NodeMorpher.MorphStarted，时长 = 这次变形的总时长。
    public class PlantFit : MonoBehaviour
    {
        public NodeMorpher morpher;
        public Camera viewCamera;

        [Header("适配")]
        [Tooltip("主体占竖直视野的比例（略留边，避免贴边被裁）")]
        [Range(0.3f, 1f)] public float fillHeight = 0.78f;
        [Tooltip("水平方向最多占可用宽度的比例")]
        [Range(0.3f, 1f)] public float fillWidth = 0.88f;
        [Tooltip("左侧为 HUD 留出的屏幕宽度比例。任务面板关闭时为 0，植株居中")]
        [Range(0f, 0.5f)] public float hudLeftFraction = 0f;
        [Tooltip("右侧边距占屏幕宽度的比例")]
        [Range(0f, 0.3f)] public float rightMarginFraction = 0f;
        [Tooltip("植株中心在竖直方向的画面位置（0.5 = 正中）。略高于中线，给下方字幕留空间")]
        [Range(0.3f, 0.7f)] public float verticalCenter = 0.52f;
        [Tooltip("离近裁剪面至少留多远（米）")]
        public float nearClearance = 0.05f;
        [Tooltip("植株中心离相机的距离（米）")]
        public float fitDistance = 0.75f;
        [Tooltip("在算出的缩放上再除以这个系数，>1 让整株略小、四周留白")]
        [Range(1f, 1.4f)] public float padding = 1.08f;

        [Header("主体包围盒")]
        [Tooltip("丢掉最低这么多比例的离群节点。0 = 全部入画")]
        [Range(0f, 0.25f)] public float trimLow = 0f;
        [Tooltip("丢掉最高这么多比例的离群节点。1 = 全部入画")]
        [Range(0.75f, 1f)] public float trimHigh = 1f;

        [Header("过渡")]
        [Tooltip("MorphDuration 不可用时的默认过渡时长（秒）")]
        public float fallbackDuration = 2.4f;

        // 适配后（或过渡中）植株的世界缩放和位置变化时发出，TargetRotator 等据此重新记录静止姿态
        public event System.Action Fitted;

        public bool IsTransitioning { get; private set; }
        // Pick 阶段：植株按真实尺寸固定在世界里，不再按形态 / 屏幕比例适配（Hold / Release）
        public bool IsHeld { get; private set; }

        struct FormBounds
        {
            public Vector3 min, max;
            public Vector3 Center => (min + max) * 0.5f;
            public Vector3 Size => max - min;
        }

        readonly Dictionary<MorphForm, FormBounds> cache = new Dictionary<MorphForm, FormBounds>();
        readonly Dictionary<MorphForm, float> radiusCache = new Dictionary<MorphForm, float>();
        float fromScale, toScale;
        Vector3 fromPos, toPos;
        float t0, duration;
        int lastScreenW, lastScreenH;

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        void OnEnable()
        {
            if (morpher != null) morpher.MorphStarted += HandleMorphStarted;
        }

        void OnDisable()
        {
            if (morpher != null) morpher.MorphStarted -= HandleMorphStarted;
        }

        void Start()
        {
            if (morpher != null) SnapTo(morpher.CurrentForm);
        }

        void HandleMorphStarted(MorphForm form)
        {
            if (IsHeld) return;
            float d = morpher.MorphDuration > 0f ? morpher.MorphDuration : fallbackDuration;
            TransitionTo(form, d);
        }

        // 停止自动适配，用 seconds 秒缓动到给定的本地位置和缩放并保持住（Pick 阶段：真实尺寸，scale = 1）
        public void Hold(Vector3 localPosition, float scale, float seconds)
        {
            IsHeld = true;
            fromScale = transform.localScale.x;
            fromPos = transform.localPosition;
            toScale = scale;
            toPos = localPosition;
            t0 = Time.time;
            duration = Mathf.Max(0.01f, seconds);
            IsTransitioning = true;
        }

        // 恢复自动适配：用 seconds 秒缓动回当前形态的适配。之后同一帧里如果开始变形，MorphStarted 会接着改目标
        public void Release(float seconds)
        {
            if (!IsHeld) return;
            IsHeld = false;
            if (morpher != null && viewCamera != null) TransitionTo(morpher.CurrentForm, seconds);
        }

        // 立即适配到某个形态
        public void SnapTo(MorphForm form)
        {
            Compute(form, out toScale, out toPos);
            fromScale = toScale;
            fromPos = toPos;
            IsTransitioning = false;
            Apply(toScale, toPos);
        }

        // 用 seconds 秒缓动到某个形态的适配
        public void TransitionTo(MorphForm form, float seconds)
        {
            fromScale = transform.localScale.x;
            fromPos = transform.localPosition;
            Compute(form, out toScale, out toPos);
            t0 = Time.time;
            duration = Mathf.Max(0.01f, seconds);
            IsTransitioning = true;
        }

        void LateUpdate()
        {
            if (morpher == null || viewCamera == null) return;
            // 窗口大小变化（Game View 调整）时按当前形态重新适配
            if (!IsHeld && (Screen.width != lastScreenW || Screen.height != lastScreenH))
            {
                lastScreenW = Screen.width;
                lastScreenH = Screen.height;
                if (!IsTransitioning) { SnapTo(morpher.CurrentForm); return; }
                Compute(morpher.CurrentForm, out toScale, out toPos);
            }
            if (!IsTransitioning) return;
            float k = Mathf.Clamp01((Time.time - t0) / duration);
            float e = k * k * (3f - 2f * k);
            Apply(Mathf.Lerp(fromScale, toScale, e), Vector3.Lerp(fromPos, toPos, e));
            if (k >= 1f) IsTransitioning = false;
        }

        void Apply(float scale, Vector3 localPos)
        {
            transform.localScale = Vector3.one * scale;
            transform.localPosition = localPos;
            Fitted?.Invoke();
        }

        // 适配计算（相机水平正视）。约定：PlantFit 在场景根，Plant 是它的子物体、静止时本地变换为单位变换
        // （TargetRotator 只在 Plant 的父空间里转，静止时归零）。
        void Compute(MorphForm form, out float scale, out Vector3 localPos)
        {
            var b = BoundsOf(form);
            var cam = viewCamera.transform;
            Vector3 center = b.Center;
            Vector3 size = Vector3.Max(b.Size, Vector3.one * 0.01f);

            float tanV = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Max(0.1f, viewCamera.aspect);
            float tanH = tanV * aspect;
            float usableW = Mathf.Max(0.2f, 1f - hudLeftFraction - rightMarginFraction);
            float D = Mathf.Max(0.2f, fitDistance);
            float pad = Mathf.Max(1f, padding);

            if (UsesBoundingSphere(form))
            {
                float radius = Mathf.Max(0.01f, RadiusOf(form));
                float maxRv = D * tanV * fillHeight;
                float maxRh = D * tanH * usableW * fillWidth;
                scale = Mathf.Max(0.01f, Mathf.Min(maxRv, maxRh) / (radius * pad));

                float minNear = viewCamera.nearClipPlane + nearClearance;
                if (D - scale * radius < minNear)
                    scale = Mathf.Max(0.01f, (D - minNear) / radius);
            }
            else
            {
                float halfDepth = size.z * 0.5f;
                float fv = 2f * tanV * fillHeight;
                float sV = fv * D / (size.y * pad + fv * halfDepth);
                float fh = 2f * tanH * usableW * fillWidth;
                float sH = fh * D / (size.x * pad + fh * halfDepth);
                scale = Mathf.Max(0.01f, Mathf.Min(sV, sH));

                float minNear = viewCamera.nearClipPlane + nearClearance;
                if (D - scale * halfDepth < minNear)
                    scale = Mathf.Max(0.01f, (D - minNear) / Mathf.Max(0.001f, halfDepth));
            }

            // 位置：主体中心落在可用区域的水平中央、verticalCenter；偏移按中心所在深度 D 换算，避免近面/远面把中心拽偏
            float viewX = hudLeftFraction + usableW * 0.5f;
            float ndcX = viewX * 2f - 1f, ndcY = verticalCenter * 2f - 1f;
            Vector3 targetCenterWorld = cam.position + cam.forward * D
                                        + cam.right * (ndcX * tanH * D)
                                        + cam.up * (ndcY * tanV * D);
            Transform parent = transform.parent;
            Vector3 targetLocal = parent != null ? parent.InverseTransformPoint(targetCenterWorld) : targetCenterWorld;
            localPos = targetLocal - center * scale;
        }

        static bool UsesBoundingSphere(MorphForm form)
        {
            return form == MorphForm.Network || form == MorphForm.Geometric || form == MorphForm.Real;
        }

        FormBounds BoundsOf(MorphForm form)
        {
            if (cache.TryGetValue(form, out var b)) return b;
            var nodes = morpher.nodeSet.nodes;
            int n = nodes.Count;
            var xs = new float[n]; var ys = new float[n]; var zs = new float[n];
            var halves = new float[n];
            for (int i = 0; i < n; i++)
            {
                var pose = nodes[i].GetPose(form);
                xs[i] = pose.position.x; ys[i] = pose.position.y; zs[i] = pose.position.z;
                halves[i] = Mathf.Max(pose.scale.x, Mathf.Max(pose.scale.y, pose.scale.z)) * 0.5f;
            }
            System.Array.Sort(xs); System.Array.Sort(ys); System.Array.Sort(zs);
            float pad = n > 0 ? Max(halves) : 0f;
            b.min = new Vector3(Q(xs, trimLow), Q(ys, trimLow), Q(zs, trimLow)) - Vector3.one * pad;
            b.max = new Vector3(Q(xs, trimHigh), Q(ys, trimHigh), Q(zs, trimHigh)) + Vector3.one * pad;
            cache[form] = b;
            return b;
        }

        float RadiusOf(MorphForm form)
        {
            if (radiusCache.TryGetValue(form, out var r)) return r;
            var b = BoundsOf(form);
            Vector3 c = b.Center;
            var nodes = morpher.nodeSet.nodes;
            r = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                var pose = nodes[i].GetPose(form);
                float half = Mathf.Max(pose.scale.x, Mathf.Max(pose.scale.y, pose.scale.z)) * 0.5f;
                r = Mathf.Max(r, (pose.position - c).magnitude + half);
            }
            r = Mathf.Max(r, 0.01f);
            radiusCache[form] = r;
            return r;
        }

        static float Max(float[] a)
        {
            float m = 0f;
            for (int i = 0; i < a.Length; i++) if (a[i] > m) m = a[i];
            return m;
        }

        static float Q(float[] sorted, float q)
        {
            if (sorted.Length == 0) return 0f;
            float f = Mathf.Clamp01(q) * (sorted.Length - 1);
            int i = Mathf.FloorToInt(f);
            int j = Mathf.Min(sorted.Length - 1, i + 1);
            return Mathf.Lerp(sorted[i], sorted[j], f - i);
        }

        // 布局资产改了以后清缓存（编辑器工具用）
        public void ClearCache()
        {
            cache.Clear();
            radiusCache.Clear();
        }

        // 调试 / 验收：某形态的主体包围盒（Plant 本地空间）
        public void GetFormBounds(MorphForm form, out Vector3 min, out Vector3 max)
        {
            var b = BoundsOf(form);
            min = b.min;
            max = b.max;
        }
    }
}
