using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 画节点之间的父子连线：回路形态是直角折线，网络形态是直线，其余形态淡出。
    // 所有连线合在一个动态网格里（Lines 拓扑），一次绘制；跟随 NodeMorpher 每帧的插值结果。
    // 每条连线以子节点 id 标识（每个节点只有一个父节点）。SetLinkColor 可以单独给某条线染色（S2 划过的线变蓝），
    // 两端任一节点被隐藏时连线跟着淡出。
    // 生长进度（SetLinkGrowth，0–1）：连线从一端沿折线长到另一端，0 = 隐藏，1 = 完整（默认）。
    // S2 缺失节点：它自己和它子节点的连线都设为 0（断线），玩家拖近正确缺口时从两侧节点朝它长出来。
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
        // 生长进度：当前值、目标值、速度（每秒；<= 0 = 立即），以及从哪一端长出
        float[] growth;
        float[] growthTarget;
        float[] growthSpeed;
        bool[] growFromParent;
        // 按节点 id 索引：以它为父节点、且有连线的子节点 id
        List<int>[] linkedChildrenOf;

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
            growth = new float[linkChildren.Count];
            growthTarget = new float[linkChildren.Count];
            growthSpeed = new float[linkChildren.Count];
            growFromParent = new bool[linkChildren.Count];
            for (int i = 0; i < linkChildren.Count; i++) growth[i] = growthTarget[i] = 1f;
            linkedChildrenOf = new List<int>[nodeCount];
            foreach (int child in linkChildren)
            {
                int parent = morpher.nodeSet.nodes[child].parentId;
                if (linkedChildrenOf[parent] == null) linkedChildrenOf[parent] = new List<int>();
                linkedChildrenOf[parent].Add(child);
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

            elbowScale = Mathf.MoveTowards(elbowScale, elbowTarget, Time.deltaTime * elbowSpeed);
            float fade = Time.deltaTime * colorFadeSpeed;
            float maxAlpha = 0f;
            for (int i = 0; i < linkChildren.Count; i++)
            {
                overrideWeight[i] = Mathf.MoveTowards(overrideWeight[i], overrideTarget[i], fade);
                if (growth[i] != growthTarget[i])
                    growth[i] = growthSpeed[i] > 0f
                        ? Mathf.MoveTowards(growth[i], growthTarget[i], Time.deltaTime * growthSpeed[i])
                        : growthTarget[i];
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
                if (growth[i] <= 0f) alpha = 0f;
                maxAlpha = Mathf.Max(maxAlpha, alpha);

                // 直角折线：先水平走到子节点的 x，再竖直走到子节点；直线时拐点在中点
                Vector3 corner = Vector3.Lerp((a + b) * 0.5f, new Vector3(b.x, a.y, (a.z + b.z) * 0.5f), elbow);

                int v = i * 4;
                if (growth[i] >= 1f)
                {
                    vertices[v] = a;
                    vertices[v + 1] = corner;
                    vertices[v + 2] = corner;
                    vertices[v + 3] = b;
                }
                else if (growFromParent[i]) WriteGrown(v, a, corner, b, growth[i]);
                else WriteGrown(v, b, corner, a, growth[i]);
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

        // 折线 p0 → p1 → p2 只画从 p0 起、总长 progress 比例的一段
        void WriteGrown(int v, Vector3 p0, Vector3 p1, Vector3 p2, float progress)
        {
            float l1 = Vector3.Distance(p0, p1);
            float l2 = Vector3.Distance(p1, p2);
            float target = Mathf.Max(0f, progress) * (l1 + l2);
            vertices[v] = p0;
            if (target <= l1)
            {
                Vector3 end = l1 > 1e-6f ? Vector3.Lerp(p0, p1, target / l1) : p0;
                vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = end;
            }
            else
            {
                vertices[v + 1] = vertices[v + 2] = p1;
                vertices[v + 3] = l2 > 1e-6f ? Vector3.Lerp(p1, p2, (target - l1) / l2) : p2;
            }
        }

        // Start 之后连线表才建好；之前 HasLink 全部返回 false
        public bool IsReady => linkIndexOf != null;

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

        // 子节点 childId 与父节点之间连线的生长进度（0 = 隐藏，1 = 完整）。
        // fromParent：true 从父节点一端长出，false 从子节点一端长出。speed：每秒变化量，<= 0 立即生效
        public void SetLinkGrowth(int childId, float progress, bool fromParent = true, float speed = 0f)
        {
            if (!HasLink(childId)) return;
            int i = linkIndexOf[childId];
            growthTarget[i] = Mathf.Clamp01(progress);
            growthSpeed[i] = speed;
            growFromParent[i] = fromParent;
            if (speed <= 0f) growth[i] = growthTarget[i];
        }

        public float GetLinkGrowth(int childId)
        {
            return HasLink(childId) ? growth[linkIndexOf[childId]] : 0f;
        }

        public void SetLinkHidden(int childId, bool hidden)
        {
            SetLinkGrowth(childId, hidden ? 0f : 1f);
        }

        // 和节点 id 相连的所有连线（它到父节点的、它的子节点到它的）一起设生长进度，都朝这个节点长：
        // 父侧那条从父节点长出，子侧的从各个子节点长出。S2 缺口断线 / 伸线用
        public void SetNodeLinksGrowth(int id, float progress, float speed = 0f)
        {
            if (linkIndexOf == null || id < 0 || id >= linkIndexOf.Length) return;
            SetLinkGrowth(id, progress, fromParent: true, speed);
            var children = linkedChildrenOf[id];
            if (children == null) return;
            foreach (int child in children) SetLinkGrowth(child, progress, fromParent: false, speed);
        }

        // 和节点 id 相连的连线的子节点 id（它自己有连线时含它自己）。ids 先清空
        public void GetNodeLinks(int id, List<int> ids)
        {
            ids.Clear();
            if (linkIndexOf == null || id < 0 || id >= linkIndexOf.Length) return;
            if (HasLink(id)) ids.Add(id);
            if (linkedChildrenOf[id] != null) ids.AddRange(linkedChildrenOf[id]);
        }

        // 所有连线恢复完整（离开阶段时调用）
        public void ResetAllLinkGrowth()
        {
            if (growth == null) return;
            for (int i = 0; i < growth.Length; i++)
            {
                growth[i] = growthTarget[i] = 1f;
                growthSpeed[i] = 0f;
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
