using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 画节点之间的父子连线：回路形态是直角折线，网络形态是直线，其余形态淡出。
    // 所有连线合在一个动态网格里（Lines 拓扑），一次绘制；跟随 NodeMorpher 每帧的插值结果。
    [RequireComponent(typeof(NodeMorpher))]
    [DefaultExecutionOrder(100)] // 在 NodeMorpher.Update 之后读取本帧姿态
    public class NodeLinkRenderer : MonoBehaviour
    {
        [Tooltip("需要 Ghost/MorphLine 材质（顶点色 + 透明）")]
        public Material material;
        [Tooltip("土块不连线，否则网络形态里根部会出现一大团放射线")]
        public bool skipSoil = true;

        NodeMorpher morpher;
        Mesh mesh;
        // 每条连线是 父 → 拐点 → 子 两段，共 4 个顶点
        readonly List<int> linkChildren = new List<int>();
        Vector3[] vertices;
        Color[] colors;

        void Start()
        {
            morpher = GetComponent<NodeMorpher>();
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

            float maxAlpha = 0f;
            for (int i = 0; i < linkChildren.Count; i++)
            {
                int child = linkChildren[i];
                int parent = morpher.nodeSet.nodes[child].parentId;
                Vector3 a = poses[parent].position;
                Vector3 b = poses[child].position;

                // 两端节点各自插值，取较小的不透明度，避免一端还没到位就先连上
                Vector2 pa = links[parent];
                Vector2 pb = links[child];
                float elbow = Mathf.Min(pa.x, pb.x);
                float alpha = Mathf.Min(pa.y, pb.y);
                maxAlpha = Mathf.Max(maxAlpha, alpha);

                // 直角折线：先水平走到子节点的 x，再竖直走到子节点；直线时拐点在中点
                Vector3 corner = Vector3.Lerp((a + b) * 0.5f, new Vector3(b.x, a.y, (a.z + b.z) * 0.5f), elbow);

                int v = i * 4;
                vertices[v] = a;
                vertices[v + 1] = corner;
                vertices[v + 2] = corner;
                vertices[v + 3] = b;
                var color = OrganPalette.LinkColor;
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

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
