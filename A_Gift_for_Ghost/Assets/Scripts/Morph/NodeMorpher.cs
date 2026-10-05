using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 把全部节点从当前形态插值到目标形态（缓动 + 错峰延迟），用 GPU Instancing 一次画完。
    // 节点不是 GameObject；中途切换形态时从当前插值到一半的位置继续，不会跳变。
    public class NodeMorpher : MonoBehaviour
    {
        public PlantNodeSet nodeSet;
        public Mesh mesh;
        [Tooltip("需要开启 GPU Instancing 的 Ghost/MorphNode 材质")]
        public Material material;
        public MorphForm startForm = MorphForm.Matrix;

        [Header("动画")]
        [Tooltip("单个节点飞到目标位置的时长（秒）")]
        public float nodeDuration = 1.2f;
        [Tooltip("按离根远近错开的最大延迟（秒）")]
        public float depthStagger = 0.9f;
        [Tooltip("额外的随机延迟（秒），让动作不那么整齐")]
        public float randomStagger = 0.35f;

        [Header("调试")]
        [Tooltip("运行时在 Inspector 里改这个值即可触发变形")]
        public MorphForm inspectorTarget;

        public event Action<MorphForm> MorphStarted;
        public event Action<MorphForm> MorphCompleted;

        public MorphForm CurrentForm { get; private set; }
        public bool IsMorphing { get; private set; }

        // Instancing 单次调用的上限
        const int MaxInstances = 1023;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        NodePose[] fromPoses;
        NodePose[] currentPoses;
        Color[] fromColors;
        Vector4[] currentColors;
        float[] delays;
        Matrix4x4[] matrices;
        MaterialPropertyBlock props;
        int maxDepth;
        float elapsed;
        float totalDuration;

        int Count => nodeSet.Count;

        void Awake()
        {
            if (nodeSet == null || mesh == null || material == null)
            {
                Debug.LogError("[Morph] NodeMorpher 缺少 nodeSet、mesh 或 material", this);
                enabled = false;
                return;
            }
            if (Count > MaxInstances)
                Debug.LogWarning($"[Morph] 节点数 {Count} 超过单次 Instancing 上限 {MaxInstances}，多出的不会绘制", this);

            fromPoses = new NodePose[Count];
            currentPoses = new NodePose[Count];
            fromColors = new Color[Count];
            currentColors = new Vector4[Count];
            delays = new float[Count];
            matrices = new Matrix4x4[Count];
            props = new MaterialPropertyBlock();
            foreach (var node in nodeSet.nodes) maxDepth = Mathf.Max(maxDepth, node.depth);

            SnapTo(startForm);
        }

        // 直接切到某个形态，不播动画（调试跳关用）
        public void SnapTo(MorphForm form)
        {
            for (int i = 0; i < Count; i++)
            {
                var node = nodeSet.nodes[i];
                currentPoses[i] = node.GetPose(form);
                currentColors[i] = OrganPalette.FormColor(node, form);
            }
            CurrentForm = form;
            inspectorTarget = form;
            IsMorphing = false;
        }

        public void MorphTo(MorphForm form)
        {
            if (!IsMorphing && form == CurrentForm) return;

            // 往写实方向变形时从根往外长；往抽象方向时从枝梢先散开
            bool towardReal = form > CurrentForm;
            float maxDelay = 0f;
            for (int i = 0; i < Count; i++)
            {
                var node = nodeSet.nodes[i];
                fromPoses[i] = currentPoses[i];
                fromColors[i] = currentColors[i];

                float depth01 = maxDepth > 0 ? (float)node.depth / maxDepth : 0f;
                float order = towardReal ? depth01 : 1f - depth01;
                delays[i] = order * depthStagger + Hash01(node.id) * randomStagger;
                maxDelay = Mathf.Max(maxDelay, delays[i]);
            }

            CurrentForm = form;
            inspectorTarget = form;
            elapsed = 0f;
            totalDuration = maxDelay + nodeDuration;
            IsMorphing = true;
            MorphStarted?.Invoke(form);
        }

        void Update()
        {
            if (IsMorphing) Animate(Time.deltaTime);
            Draw();
        }

        void Animate(float deltaTime)
        {
            elapsed += deltaTime;
            for (int i = 0; i < Count; i++)
            {
                var node = nodeSet.nodes[i];
                float t = nodeDuration > 0f ? Mathf.Clamp01((elapsed - delays[i]) / nodeDuration) : 1f;
                float eased = EaseInOutCubic(t);
                currentPoses[i] = NodePose.Lerp(fromPoses[i], node.GetPose(CurrentForm), eased);
                currentColors[i] = Color.Lerp(fromColors[i], OrganPalette.FormColor(node, CurrentForm), eased);
            }

            if (elapsed >= totalDuration)
            {
                IsMorphing = false;
                MorphCompleted?.Invoke(CurrentForm);
            }
        }

        void Draw()
        {
            int count = Mathf.Min(Count, MaxInstances);
            var localToWorld = transform.localToWorldMatrix;
            for (int i = 0; i < count; i++) matrices[i] = localToWorld * currentPoses[i].ToMatrix();

            props.SetVectorArray(BaseColorId, currentColors);
            var rp = new RenderParams(material)
            {
                matProps = props,
                // 网络形态会散开到 1 m 以上，包围盒给大一点，避免被视锥剔除
                worldBounds = new Bounds(transform.position, Vector3.one * 6f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count);
        }

        void OnValidate()
        {
            if (Application.isPlaying && currentPoses != null && inspectorTarget != CurrentForm)
                MorphTo(inspectorTarget);
        }

        static float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        static float Hash01(int id)
        {
            uint h = (uint)id * 2246822519u + 374761393u;
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
