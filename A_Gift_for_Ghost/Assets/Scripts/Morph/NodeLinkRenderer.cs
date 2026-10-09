using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 画节点之间的父子连线：回路形态是直角折线，网络形态是直线，其余形态淡出。
    // 所有连线合在一个动态网格里（Lines 拓扑），一次绘制；跟随 NodeMorpher 每帧的插值结果。
    // 每条连线以子节点 id 标识（每个节点只有一个父节点）。SetLinkColor 可以单独给某条线染色（S2 划过的线变蓝），
    // 两端任一节点被隐藏时连线跟着淡出。
    [RequireComponent(typeof(NodeMorpher))]
    [DefaultExecutionOrder(100)] // 在 NodeMorpher.Update 之后读取本帧姿态
    public class NodeLinkRenderer : MonoBehaviour
    {
        [Tooltip("需要 Ghost/MorphLine 材质（顶点色 + 透明）")]
        public Material material;
        [Tooltip("土块不连线，否则网络形态里根部会出现一大团放射线")]
        public bool skipSoil = true;
        [Tooltip("连线染色的过渡速度（每秒变化量）")]
        public float colorFadeSpeed = 6f;
        [Tooltip("染色的连线至少这么不透明，在连线很淡的形态里也看得见")]
        [Range(0f, 1f)] public float overrideMinAlpha = 0.6f;
        [Tooltip("直角折线程度的倍数：1 = 按形态（回路是直角），0 = 一律两点直连。阶段用 SetElbowScale 改（S2 随进度从直连过渡到直角）")]
        [Range(0f, 1f)] public float elbowScale = 1f;
        [Tooltip("SetElbowScale 非立即生效时的过渡速度（每秒变化量）")]
        public float elbowSpeed = 1.2f;

        float elbowTarget = 1f;

        // 设置直角程度倍数。immediate = false 时按 elbowSpeed 平滑过渡
        public void SetElbowScale(float value, bool immediate = false)
        {
            elbowTarget = Mathf.Clamp01(value);
            if (immediate) elbowScale = elbowTarget;
        }

        NodeMorpher morpher;
        Mesh mesh;
        // 每条连线是 父 → 拐点 → 子 两段，共 4 个顶点
        readonly List<int> linkChildren = new List<int>();
        Vector3[] vertices;
        Color[] colors;
        // 按子节点 id 索引：-1 表示这个节点没有连线
        int[] linkIndexOf;
        Color[] overrideColors;
        float[] overrideTarget;
        float[] overrideWeight;

        void Start()
        {
            morpher = GetComponent<NodeMorpher>();
            elbowTarget = elbowScale;
            if (material == null || morpher.nodeSet == null)
            {
                Debug.LogError("[Morph] NodeLinkRenderer 缺少 material 或 NodeMorpher 没有 nodeSet", this);
                enabled = false;
                return;
            }

            foreach (var node in morpher.nodeSet.nodes)
            {
                if (node.parentId < 0) continue;
                if (skipSoil && node.organ == Organ.Soil) continue;
                linkChildren.Add(node.id);
            }

            int nodeCount = morpher.nodeSet.Count;
            linkIndexOf = new int[nodeCount];
            for (int i = 0; i < nodeCount; i++) linkIndexOf[i] = -1;
            for (int i = 0; i < linkChildren.Count; i++) linkIndexOf[linkChildren[i]] = i;
            overrideColors = new Color[linkChildren.Count];
            overrideTarget = new float[linkChildren.Count];
            overrideWeight = new float[linkChildren.Count];

            int vertexCount = linkChildren.Count * 4;
            vertices = new Vector3[vertexCount];
            colors = new Color[vertexCount];
            var indices = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++) indices[i] = i;

            mesh = new Mesh { name = "NodeLinks" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            // 网络形态会散开，包围盒给大一点，避免被视锥剔除
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
        }

        void LateUpdate()
        {
            var poses = morpher.CurrentPoses;
            var links = morpher.CurrentLinkParams;
            if (poses == null) return;

            elbowScale = Mathf.MoveTowards(elbowScale, elbowTarget, Time.deltaTime * elbowSpeed);
            float fade = Time.deltaTime * colorFadeSpeed;
            float maxAlpha = 0f;
            for (int i = 0; i < linkChildren.Count; i++)
            {
                overrideWeight[i] = Mathf.MoveTowards(overrideWeight[i], overrideTarget[i], fade);
                int child = linkChildren[i];
                int parent = morpher.nodeSet.nodes[child].parentId;
                Vector3 a = poses[parent].position;
                Vector3 b = poses[child].position;

                // 两端节点各自插值，取较小的不透明度，避免一端还没到位就先连上
                Vector2 pa = links[parent];
                Vector2 pb = links[child];
                float elbow = Mathf.Min(pa.x, pb.x) * elbowScale;
                float alpha = Mathf.Min(pa.y, pb.y);
                if (overrideWeight[i] > 0f) alpha = Mathf.Lerp(alpha, Mathf.Max(alpha, overrideMinAlpha), overrideWeight[i]);
                // 两端节点被隐藏或缩没时连线一起淡出
                alpha *= Mathf.Min(morpher.GetNodeVisibility(parent), morpher.GetNodeVisibility(child));
                maxAlpha = Mathf.Max(maxAlpha, alpha);

                // 直角折线：先水平走到子节点的 x，再竖直走到子节点；直线时拐点在中点
                Vector3 corner = Vector3.Lerp((a + b) * 0.5f, new Vector3(b.x, a.y, (a.z + b.z) * 0.5f), elbow);

                int v = i * 4;
                vertices[v] = a;
                vertices[v + 1] = corner;
                vertices[v + 2] = corner;
                vertices[v + 3] = b;
                var color = OrganPalette.LinkColor;
                if (overrideWeight[i] > 0f) color = Color.Lerp(color, overrideColors[i], overrideWeight[i]);
                color.a = alpha;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
            }

            // 所有连线都看不见时不提交绘制
            if (maxAlpha <= 0.001f) return;

            mesh.vertices = vertices;
            mesh.colors = colors;
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMesh(rp, mesh, 0, transform.localToWorldMatrix);
        }

        // 这个节点和它父节点之间有没有连线（土块默认没有）
        public bool HasLink(int childId)
        {
            return linkIndexOf != null && childId >= 0 && childId < linkIndexOf.Length && linkIndexOf[childId] >= 0;
        }

        // a、b 是否直接相连（父子关系，顺序不限）。返回子节点 id，没有连线时返回 -1
        public int FindLink(int a, int b)
        {
            if (morpher == null || morpher.nodeSet == null) return -1;
            var na = morpher.nodeSet.Get(a);
            var nb = morpher.nodeSet.Get(b);
            if (na != null && na.parentId == b && HasLink(a)) return a;
            if (nb != null && nb.parentId == a && HasLink(b)) return b;
            return -1;
        }

        // 给子节点 childId 与其父节点之间的连线染色（带淡入）
        public void SetLinkColor(int childId, Color color)
        {
            if (!HasLink(childId)) return;
            int i = linkIndexOf[childId];
            overrideColors[i] = color;
            overrideTarget[i] = 1f;
        }

        // 给 a、b 之间的连线染色（顺序不限）。两者不直接相连时返回 false
        public bool SetLinkColor(int a, int b, Color color)
        {
            int child = FindLink(a, b);
            if (child < 0) return false;
            SetLinkColor(child, color);
            return true;
        }

        public void ClearLinkColor(int childId)
        {
            if (HasLink(childId)) overrideTarget[linkIndexOf[childId]] = 0f;
        }

        public void ClearAllLinkColors()
        {
            if (overrideTarget == null) return;
            for (int i = 0; i < overrideTarget.Length; i++) overrideTarget[i] = 0f;
        }

        public bool IsLinkColored(int childId)
        {
            return HasLink(childId) && overrideTarget[linkIndexOf[childId]] > 0f;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
