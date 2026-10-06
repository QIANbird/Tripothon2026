using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 把全部节点从当前形态插值到目标形态（缓动 + 错峰延迟），用 GPU Instancing 一次画完。
    // 节点不是 GameObject；中途切换形态时从当前插值到一半的位置继续，不会跳变。
    // 逐节点的闪烁、高亮、隐藏和整体写实度见 NodeMorpherStates.cs。
    public partial class NodeMorpher : MonoBehaviour
    {
        public PlantNodeSet nodeSet;
        [Tooltip("抽象形态的节点网格（立方体）；叶、果实等在几何植株形态换成 NodeShapes 里的形状")]
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

        [Header("写实交接")]
        [Tooltip("真模型显现时，节点按高度缩小消失的过渡带（占植株高度的比例），和 RevealLit 着色器一致")]
        public float revealBand = 0.15f;
        [Tooltip("离开写实形态时，等真模型褪去再开始变形的时长（秒）")]
        public float exitDelay = 0.5f;

        [Header("调试")]
        [Tooltip("运行时在 Inspector 里改这个值即可触发变形")]
        public MorphForm inspectorTarget;

        public event Action<MorphForm> MorphStarted;
        public event Action<MorphForm> MorphCompleted;

        public MorphForm CurrentForm { get; private set; }
        public bool IsMorphing { get; private set; }
        // 最近一次 MorphTo 的总时长（秒，含逐节点延迟）。MorphStarted 发出时已经是新值，PlantFit 用它同步过渡
        public float MorphDuration => totalDuration;

        // 每帧插值后的节点姿态（本地空间）和连线参数，供 NodeLinkRenderer 等读取，不要修改
        public NodePose[] CurrentPoses => currentPoses;
        public Vector2[] CurrentLinkParams => currentLinks;

        // 真模型显现进度（0–1），由 RealModelHandoff 每帧写入；节点据此按高度缩小
        public float RealReveal { get; set; }
        // 写实形态下节点的高度范围（本地空间），供交接时对齐模型溶解的高度
        public float RealMinY { get; private set; }
        public float RealMaxY { get; private set; }

        // Instancing 单次调用的上限
        const int MaxInstances = 1023;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        NodePose[] fromPoses;
        NodePose[] currentPoses;
        Color[] fromColors;
        Vector4[] currentColors;
        Vector2[] fromLinks;
        Vector2[] currentLinks;
        float[] fromShapes;
        float[] currentShapes;
        float[] delays;
        float[] realHeights;

        // 每种网格一批：立方体一批，每种部位形状各一批
        class Batch
        {
            public Mesh mesh;
            public Matrix4x4[] matrices;
            public Vector4[] colors;
            public MaterialPropertyBlock props;
            public int count;
        }

        Batch cubeBatch;
        Batch[] shapeBatches;
        // 节点用哪一批部位形状，-1 表示始终是立方体
        int[] shapeOf;
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
            fromLinks = new Vector2[Count];
            currentLinks = new Vector2[Count];
            fromShapes = new float[Count];
            currentShapes = new float[Count];
            delays = new float[Count];
            BuildBatches();
            foreach (var node in nodeSet.nodes) maxDepth = Mathf.Max(maxDepth, node.depth);
            CacheRealHeights();
            InitNodeStates();

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
                currentLinks[i] = OrganPalette.LinkParams(form);
                currentShapes[i] = NodeShapes.ShapeWeight(form);
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
            // 真模型还在显示时，先等它褪去
            float holdDelay = RealReveal * exitDelay;
            float maxDelay = 0f;
            for (int i = 0; i < Count; i++)
            {
                var node = nodeSet.nodes[i];
                fromPoses[i] = currentPoses[i];
                fromColors[i] = currentColors[i];
                fromLinks[i] = currentLinks[i];
                fromShapes[i] = currentShapes[i];

                float depth01 = maxDepth > 0 ? (float)node.depth / maxDepth : 0f;
                float order = towardReal ? depth01 : 1f - depth01;
                delays[i] = holdDelay + order * depthStagger + Hash01(node.id) * randomStagger;
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
            UpdateNodeStates(Time.deltaTime);
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
                currentLinks[i] = Vector2.Lerp(fromLinks[i], OrganPalette.LinkParams(CurrentForm), eased);
                currentShapes[i] = Mathf.Lerp(fromShapes[i], NodeShapes.ShapeWeight(CurrentForm), eased);
            }

            if (elapsed >= totalDuration)
            {
                IsMorphing = false;
                MorphCompleted?.Invoke(CurrentForm);
            }
        }

        void Draw()
        {
            // 真模型完全显现时节点全部隐藏
            if (RealReveal >= 1f) return;

            cubeBatch.count = 0;
            foreach (var batch in shapeBatches) batch.count = 0;

            var localToWorld = transform.localToWorldMatrix;
            for (int i = 0; i < Count; i++)
            {
                var pose = currentPoses[i];
                pose.scale *= DisplayScale(i);
                if (pose.scale.sqrMagnitude < 1e-12f) continue;
                Color color = DisplayColor(i);

                // 立方体缩小的同时部位形状长大，两者在中途重叠，避免一下子换形状
                int shape = shapeOf[i];
                float w = shape < 0 ? 0f : currentShapes[i];
                float cubeK = 1f - w * w;
                float shapeK = 1f - (1f - w) * (1f - w);
                if (cubeK > 0.001f) Add(cubeBatch, localToWorld, pose, cubeK, color);
                if (shape >= 0 && shapeK > 0.001f) Add(shapeBatches[shape], localToWorld, pose, shapeK, color);
            }

            Submit(cubeBatch);
            foreach (var batch in shapeBatches) Submit(batch);
        }

        void BuildBatches()
        {
            shapeOf = new int[Count];
            var shapeCounts = new int[NodeShapes.ShapeCount];
            for (int i = 0; i < Count; i++)
            {
                shapeOf[i] = (int)NodeShapes.ShapeOf(nodeSet.nodes[i].organ);
                if (shapeOf[i] >= 0) shapeCounts[shapeOf[i]]++;
            }

            cubeBatch = NewBatch(mesh, Count);
            shapeBatches = new Batch[NodeShapes.ShapeCount];
            for (int s = 0; s < NodeShapes.ShapeCount; s++)
                shapeBatches[s] = NewBatch(NodeShapes.GetMesh((NodeShapes.Shape)s), shapeCounts[s]);
        }

        static Batch NewBatch(Mesh batchMesh, int capacity)
        {
            capacity = Mathf.Min(capacity, MaxInstances);
            return new Batch
            {
                mesh = batchMesh,
                matrices = new Matrix4x4[capacity],
                // MaterialPropertyBlock 的数组长度在第一次设置时就固定了，至少给 1
                colors = new Vector4[Mathf.Max(capacity, 1)],
                props = new MaterialPropertyBlock(),
            };
        }

        static void Add(Batch batch, Matrix4x4 localToWorld, NodePose pose, float scale, Vector4 color)
        {
            if (batch.count >= batch.matrices.Length) return;
            pose.scale *= scale;
            batch.matrices[batch.count] = localToWorld * pose.ToMatrix();
            batch.colors[batch.count] = color;
            batch.count++;
        }

        void Submit(Batch batch)
        {
            if (batch.count == 0 || batch.mesh == null) return;
            batch.props.SetVectorArray(BaseColorId, batch.colors);
            var rp = new RenderParams(material)
            {
                matProps = batch.props,
                // 网络形态会散开到 1 m 以上，包围盒给大一点，避免被视锥剔除
                worldBounds = new Bounds(transform.position, Vector3.one * 6f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMeshInstanced(rp, batch.mesh, 0, batch.matrices, batch.count);
        }

        void OnValidate()
        {
            if (Application.isPlaying && currentPoses != null && inspectorTarget != CurrentForm)
                MorphTo(inspectorTarget);
        }

        void CacheRealHeights()
        {
            realHeights = new float[Count];
            RealMinY = float.MaxValue;
            RealMaxY = float.MinValue;
            foreach (var node in nodeSet.nodes)
            {
                float y = node.GetPose(MorphForm.Real).position.y;
                RealMinY = Mathf.Min(RealMinY, y);
                RealMaxY = Mathf.Max(RealMaxY, y);
            }
            float range = Mathf.Max(RealMaxY - RealMinY, 1e-4f);
            for (int i = 0; i < Count; i++)
                realHeights[i] = (nodeSet.nodes[i].GetPose(MorphForm.Real).position.y - RealMinY) / range;
        }

        // 和 RevealLit 着色器同一个公式：模型在高度 h 开始出现时节点开始缩小，过渡带结束时节点消失
        float RevealShrink(float height01)
        {
            float band = Mathf.Max(revealBand, 1e-4f);
            float x = Mathf.Clamp01((RealReveal * (1f + band) - height01) / band);
            return 1f - x * x * (3f - 2f * x);
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
