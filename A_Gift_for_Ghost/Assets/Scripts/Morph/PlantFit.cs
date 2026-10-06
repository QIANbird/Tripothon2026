using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Morph
{
    // 每个形态自动适配画面：Plant 的父物体（PlantFit）做"缩放 + 平移"，让目标形态的主体在固定相机里占约 fillHeight 的竖直视野。
    // 相机不动（AGENTS.md 第 3 节：不强制移动玩家镜头），只改植株的尺度和位置，VR 迁移时也不用改相机。
    //
    // 主体包围盒：取该形态所有节点位置（Plant 本地空间）在 x / y 上的 trimLow–trimHigh 分位（默认 5%–95%，去掉离群节点），
    //   再按节点自身的半尺寸外扩一点；z 取同样分位，用离相机最近的一面（z 最小值）算距离，保证不穿近裁剪面。
    // 水平方向：按屏幕宽高比，留出左侧 HUD 栏（hudLeftFraction）和右侧边距，取竖直 / 水平两个方向中较小的缩放。
    // 过渡：订阅 NodeMorpher.MorphStarted，时长 = 这次变形的总时长（NodeMorpher.MorphDuration），smoothstep 缓动。
    //   Network ↔ Geometric 两种形态的包围盒尺寸相近（LayoutGenerator 的 networkSpread 已调小），所以 S3→S4 缩放几乎不变。
    public class PlantFit : MonoBehaviour
    {
        public NodeMorpher morpher;
        public Camera viewCamera;

        [Header("适配")]
        [Tooltip("主体占竖直视野的比例")]
        [Range(0.3f, 1f)] public float fillHeight = 0.8f;
        [Tooltip("水平方向最多占可用宽度的比例")]
        [Range(0.3f, 1f)] public float fillWidth = 0.92f;
        [Tooltip("左侧 HUD 栏占屏幕宽度的比例（参考 1920 宽时 520 + 40 边距 ≈ 0.29），植株只放在它右边")]
        [Range(0f, 0.5f)] public float hudLeftFraction = 0.29f;
        [Tooltip("右侧边距占屏幕宽度的比例")]
        [Range(0f, 0.3f)] public float rightMarginFraction = 0.03f;
        [Tooltip("植株中心在竖直方向的画面位置（0.5 = 正中）。略高于中线，给下方字幕留空间")]
        [Range(0.3f, 0.7f)] public float verticalCenter = 0.54f;
        [Tooltip("离近裁剪面至少留多远（米）")]
        public float nearClearance = 0.05f;
        [Tooltip("主体包围盒中心离相机的距离（米）。约 0.75 m：Matrix / Geometric 接近 1:1 真实尺寸，伸手可及（VR 友好）")]
        public float fitDistance = 0.75f;

        [Header("主体包围盒")]
        [Range(0f, 0.25f)] public float trimLow = 0.05f;
        [Range(0.75f, 1f)] public float trimHigh = 0.95f;

        [Header("过渡")]
        [Tooltip("MorphDuration 不可用时的默认过渡时长（秒）")]
        public float fallbackDuration = 2.4f;

        // 适配后（或过渡中）植株的世界缩放和位置变化时发出，TargetRotator 等据此重新记录静止姿态
        public event System.Action Fitted;

        public bool IsTransitioning { get; private set; }

        struct FormBounds
        {
            public Vector3 min, max;
            public Vector3 Center => (min + max) * 0.5f;
            public Vector3 Size => max - min;
        }

        readonly Dictionary<MorphForm, FormBounds> cache = new Dictionary<MorphForm, FormBounds>();
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
            float d = morpher.MorphDuration > 0f ? morpher.MorphDuration : fallbackDuration;
            TransitionTo(form, d);
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
            if (Screen.width != lastScreenW || Screen.height != lastScreenH)
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
        // 包围盒中心放在相机前方 fitDistance 处；缩放 s 时近面距离 = fitDistance - s * depth/2。
        //   竖直：s * h = 2 * (fitDistance - s * depth/2) * tan(vfov/2) * fillHeight → 解出 s；水平用可用宽度同理，取小。
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
            float halfDepth = size.z * 0.5f;

            float fv = 2f * tanV * fillHeight;
            float sV = fv * D / (size.y + fv * halfDepth);
            float fh = 2f * tanH * usableW * fillWidth;
            float sH = fh * D / (size.x + fh * halfDepth);
            scale = Mathf.Max(0.01f, Mathf.Min(sV, sH));

            // 近裁剪面检查：近面离相机至少 near + nearClearance
            float minNear = viewCamera.nearClipPlane + nearClearance;
            if (D - scale * halfDepth < minNear)
                scale = Mathf.Max(0.01f, (D - minNear) / Mathf.Max(0.001f, halfDepth));

            // 位置：主体中心落在画面（可用区域水平中心，verticalCenter）；在近面所在深度上对齐
            // （竖直视野按近面算，所以中心的屏幕位置也按近面深度换算，保证上下边缘对称）
            float nearDepth = D - scale * halfDepth;
            float viewX = hudLeftFraction + usableW * 0.5f;
            float ndcX = viewX * 2f - 1f, ndcY = verticalCenter * 2f - 1f;
            Vector3 targetCenterWorld = cam.position + cam.forward * D
                                        + cam.right * (ndcX * tanH * nearDepth)
                                        + cam.up * (ndcY * tanV * nearDepth);
            Transform parent = transform.parent;
            Vector3 targetLocal = parent != null ? parent.InverseTransformPoint(targetCenterWorld) : targetCenterWorld;
            localPos = targetLocal - center * scale;
        }

        FormBounds BoundsOf(MorphForm form)
        {
            if (cache.TryGetValue(form, out var b)) return b;
            var nodes = morpher.nodeSet.nodes;
            int n = nodes.Count;
            var xs = new float[n]; var ys = new float[n]; var zs = new float[n];
            float half = 0f;
            for (int i = 0; i < n; i++)
            {
                var pose = nodes[i].GetPose(form);
                xs[i] = pose.position.x; ys[i] = pose.position.y; zs[i] = pose.position.z;
                half += Mathf.Max(pose.scale.x, Mathf.Max(pose.scale.y, pose.scale.z)) * 0.5f;
            }
            half = n > 0 ? half / n : 0f;
            System.Array.Sort(xs); System.Array.Sort(ys); System.Array.Sort(zs);
            b.min = new Vector3(Q(xs, trimLow), Q(ys, trimLow), Q(zs, trimLow)) - Vector3.one * half;
            b.max = new Vector3(Q(xs, trimHigh), Q(ys, trimHigh), Q(zs, trimHigh)) + Vector3.one * half;
            cache[form] = b;
            return b;
        }

        static float Q(float[] sorted, float q)
        {
            if (sorted.Length == 0) return 0f;
            float f = q * (sorted.Length - 1);
            int i = Mathf.FloorToInt(f);
            int j = Mathf.Min(sorted.Length - 1, i + 1);
            return Mathf.Lerp(sorted[i], sorted[j], f - i);
        }

        // 布局资产改了以后清缓存（编辑器工具用）
        public void ClearCache() => cache.Clear();

        // 调试 / 验收：某形态的主体包围盒（Plant 本地空间）
        public void GetFormBounds(MorphForm form, out Vector3 min, out Vector3 max)
        {
            var b = BoundsOf(form);
            min = b.min;
            max = b.max;
        }
    }
}
