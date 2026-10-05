using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ghost.Morph.EditorTools
{
    // 在拆好件的植物模型表面采样节点，生成写实形态（Real），替换程序生成的假植物。
    // 部位按子物体名前缀判断（soil / root / stem / leaf / pepper / fruit / tomato / bud / bug），
    // 网格自己的名字认不出时往上找父物体（Tripo 导出的虫子模型，网格名是 tripo_node_xxx，外层叫 bug_01）。
    // 同一个父物体下的多个网格算一个部件。
    // 每个部件：按面积随机撒候选点 → 最远点采样选出均匀分布的节点 → 候选点就近归属，平均出节点的法线和贴图颜色。
    // 拓扑：除土以外的节点用 Prim 最小生成树从茎底部长出（跨部位的边加权，避免果实连到果实）；土挂在最近的根上。
    public static class MeshAnchorSampler
    {
        [System.Serializable]
        public class Settings
        {
            public int seed = 5;
            // 归一化后的植株高度（米）
            public float targetHeight = 0.6f;
            public int soilCount = 30;
            public int rootCount = 10;
            public int stemCount = 48;
            public int leafCount = 90;
            // 每个果实固定几个节点（大椭球拼成果实），不按面积分
            public int nodesPerFruit = 2;
            // 同一部位有多个部件时按面积分配节点数，每个部件至少这么多
            public int minPerPart = 6;
            // 模型里没有 bug 部件时，在叶背随机放几只假虫子
            public int bugCount = 3;
            // 每个节点撒多少候选点
            public int candidatesPerNode = 16;
        }

        struct Candidate
        {
            public Vector3 position;
            public Vector3 normal;
            public Color color;
        }

        class Part
        {
            public string name;
            public Organ organ;
            public readonly List<Candidate> candidates = new List<Candidate>();
            public float area;
            public int budget;
        }

        class Anchor
        {
            public string part;
            public Organ organ;
            public Vector3 position;
            public Vector3 normal;
            public Color color;
            public float spacing;
            // 归属到这个节点的候选点的主方向（PCA）：axisLong 最长，axisShort 最短；extent 是三个方向上的半宽 * 2
            public Vector3 center;
            public Vector3 axisLong;
            public Vector3 axisShort;
            public Vector3 extent;
        }

        public static void Sample(GameObject model, PlantNodeSet set, Settings s)
        {
            var rng = new System.Random(s.seed);
            var parts = CollectParts(model, s, rng);
            if (parts.Count == 0)
            {
                Debug.LogError($"[Morph] {model.name} 里没有能识别部位的子物体");
                return;
            }

            parts.RemoveAll(p => p.candidates.Count == 0);
            if (parts.Count == 0)
            {
                Debug.LogError($"[Morph] {model.name} 的部件都没有可采样的面");
                return;
            }

            // 归一化：底部中心放到原点，高度缩放到 targetHeight
            var bounds = new Bounds(parts[0].candidates[0].position, Vector3.zero);
            foreach (var part in parts)
                foreach (var c in part.candidates) bounds.Encapsulate(c.position);
            Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float scale = s.targetHeight / Mathf.Max(bounds.size.y, 1e-4f);

            var samples = new List<Anchor>();
            foreach (var part in parts)
            {
                for (int i = 0; i < part.candidates.Count; i++)
                {
                    var c = part.candidates[i];
                    c.position = (c.position - offset) * scale;
                    part.candidates[i] = c;
                }
                part.area *= scale * scale;
                samples.AddRange(SamplePart(part, rng));
            }

            set.nodes = BuildNodes(samples, s, rng);
            set.sourceModel = model;
            set.sourceOffset = offset;
            set.sourceScale = scale;
        }

        static Organ? OrganFromName(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.StartsWith("soil")) return Organ.Soil;
            if (n.StartsWith("root")) return Organ.Root;
            if (n.StartsWith("stem")) return Organ.Stem;
            if (n.StartsWith("leaf")) return Organ.Leaf;
            if (n.StartsWith("pepper") || n.StartsWith("fruit") || n.StartsWith("tomato")) return Organ.Fruit;
            if (n.StartsWith("bud")) return Organ.Bud;
            if (n.StartsWith("bug")) return Organ.Bug;
            return null;
        }

        // 从网格所在物体往上找第一个能认出部位的物体（不含模型根，根的名字可能是 pepper_plant 之类）
        static Transform FindPartOwner(Transform t, Transform root, out Organ organ)
        {
            for (var cur = t; cur != null && cur != root; cur = cur.parent)
            {
                var o = OrganFromName(cur.name);
                if (o != null)
                {
                    organ = o.Value;
                    return cur;
                }
            }
            organ = default;
            return null;
        }

        static List<Part> CollectParts(GameObject model, Settings s, System.Random rng)
        {
            var parts = new List<Part>();
            var textures = new Dictionary<Texture, Texture2D>();
            var root = model.transform;

            var partByOwner = new Dictionary<Transform, Part>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var owner = FindPartOwner(filter.transform, root, out var organ);
                if (owner == null)
                {
                    Debug.LogWarning($"[Morph] 跳过无法识别部位的子物体：{filter.name}");
                    continue;
                }
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                // 模型根空间（保留根节点自身的旋转，FBX 的轴向转换在这里）
                Matrix4x4 toRoot = root.parent == null
                    ? filter.transform.localToWorldMatrix
                    : root.parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var renderer = filter.GetComponent<MeshRenderer>();
                var texture = LoadReadableTexture(renderer != null ? renderer.sharedMaterial : null, textures);

                if (!partByOwner.TryGetValue(owner, out var part))
                {
                    part = new Part { name = owner.name, organ = organ };
                    partByOwner[owner] = part;
                    parts.Add(part);
                }
                ScatterCandidates(mesh, toRoot, texture, part, rng);
            }

            AssignBudgets(parts, s);
            foreach (var part in parts)
            {
                // 候选点按预算重新截取，保证每个节点平均分到 candidatesPerNode 个
                int keep = Mathf.Min(part.candidates.Count, Mathf.Max(part.budget * s.candidatesPerNode, part.budget));
                if (keep < part.candidates.Count) part.candidates.RemoveRange(keep, part.candidates.Count - keep);
            }

            foreach (var tex in textures.Values)
                if (tex != null) Object.DestroyImmediate(tex);
            return parts;
        }

        // 预先多撒一些（上限 4000），预算定下来后再截取
        const int MaxCandidates = 4000;

        static void ScatterCandidates(Mesh mesh, Matrix4x4 toRoot, Texture2D texture, Part part, System.Random rng)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var triangles = mesh.triangles;
            int triCount = triangles.Length / 3;
            if (triCount == 0) return;

            var world = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) world[i] = toRoot.MultiplyPoint3x4(vertices[i]);

            // 累计面积，按面积加权随机选三角形
            var cumulative = new float[triCount];
            float total = 0f;
            for (int t = 0; t < triCount; t++)
            {
                Vector3 a = world[triangles[t * 3]], b = world[triangles[t * 3 + 1]], c = world[triangles[t * 3 + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                cumulative[t] = total;
            }
            // 一个部件可能由多个网格组成，面积累加
            part.area += total;
            if (total <= 0f) return;

            bool hasNormals = normals.Length == vertices.Length;
            bool hasUv = uvs.Length == vertices.Length && texture != null;
            for (int i = 0; i < MaxCandidates; i++)
            {
                int t = System.Array.BinarySearch(cumulative, (float)rng.NextDouble() * total);
                if (t < 0) t = ~t;
                t = Mathf.Min(t, triCount - 1);
                int i0 = triangles[t * 3], i1 = triangles[t * 3 + 1], i2 = triangles[t * 3 + 2];

                // 三角形内均匀分布的重心坐标
                float r1 = Mathf.Sqrt((float)rng.NextDouble());
                float r2 = (float)rng.NextDouble();
                float w0 = 1f - r1, w1 = r1 * (1f - r2), w2 = r1 * r2;

                var candidate = new Candidate
                {
                    position = world[i0] * w0 + world[i1] * w1 + world[i2] * w2,
                };
                Vector3 n = hasNormals
                    ? normals[i0] * w0 + normals[i1] * w1 + normals[i2] * w2
                    : Vector3.Cross(world[i1] - world[i0], world[i2] - world[i0]);
                candidate.normal = toRoot.MultiplyVector(n).normalized;
                if (hasUv)
                {
                    Vector2 uv = uvs[i0] * w0 + uvs[i1] * w1 + uvs[i2] * w2;
                    candidate.color = texture.GetPixelBilinear(uv.x, uv.y);
                }
                else
                {
                    candidate.color = OrganPalette.RealColor(part.organ);
                }
                part.candidates.Add(candidate);
            }
        }

        static void AssignBudgets(List<Part> parts, Settings s)
        {
            foreach (Organ organ in System.Enum.GetValues(typeof(Organ)))
            {
                int total;
                switch (organ)
                {
                    case Organ.Soil: total = s.soilCount; break;
                    case Organ.Root: total = s.rootCount; break;
                    case Organ.Stem: total = s.stemCount; break;
                    case Organ.Leaf: total = s.leafCount; break;
                    // 花苞、虫子这类小部件每个只放 1 个节点
                    default: total = 0; break;
                }

                var group = parts.FindAll(p => p.organ == organ);
                if (group.Count == 0) continue;
                float area = 0f;
                foreach (var p in group) area += p.area;
                foreach (var p in group)
                {
                    if (organ == Organ.Fruit) p.budget = s.nodesPerFruit;
                    else if (total == 0) p.budget = 1;
                    else p.budget = Mathf.Max(s.minPerPart, Mathf.RoundToInt(total * (area > 0f ? p.area / area : 1f / group.Count)));
                }
            }
        }

        // 最远点采样：每次选离已选节点最远的候选点，得到均匀铺满表面的节点
        static List<Anchor> SamplePart(Part part, System.Random rng)
        {
            var result = new List<Anchor>();
            var cands = part.candidates;
            int n = cands.Count;
            int budget = Mathf.Min(part.budget, n);
            if (budget <= 0) return result;

            var minDist = new float[n];
            for (int i = 0; i < n; i++) minDist[i] = float.MaxValue;
            var chosen = new List<int>();
            int next = rng.Next(n);
            for (int k = 0; k < budget; k++)
            {
                chosen.Add(next);
                Vector3 p = cands[next].position;
                int farthest = 0;
                float best = -1f;
                for (int i = 0; i < n; i++)
                {
                    float d = (cands[i].position - p).sqrMagnitude;
                    if (d < minDist[i]) minDist[i] = d;
                    if (minDist[i] > best)
                    {
                        best = minDist[i];
                        farthest = i;
                    }
                }
                next = farthest;
            }

            // 候选点归属到最近的节点，平均法线和颜色，避免单点采到贴图接缝
            var owners = new int[n];
            var normalSum = new Vector3[budget];
            var colorSum = new Color[budget];
            var posSum = new Vector3[budget];
            var counts = new int[budget];
            for (int i = 0; i < n; i++)
            {
                int owner = 0;
                float best = float.MaxValue;
                for (int k = 0; k < budget; k++)
                {
                    float d = (cands[i].position - cands[chosen[k]].position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        owner = k;
                    }
                }
                owners[i] = owner;
                normalSum[owner] += cands[i].normal;
                colorSum[owner] += cands[i].color;
                posSum[owner] += cands[i].position;
                counts[owner]++;
            }

            // 每个节点平均覆盖的表面边长，用来定节点大小
            float spacing = Mathf.Sqrt(part.area / budget);
            var covariance = new float[budget, 6];
            var centers = new Vector3[budget];
            for (int k = 0; k < budget; k++)
                centers[k] = counts[k] > 0 ? posSum[k] / counts[k] : cands[chosen[k]].position;
            for (int i = 0; i < n; i++)
            {
                int k = owners[i];
                Vector3 d = cands[i].position - centers[k];
                covariance[k, 0] += d.x * d.x; covariance[k, 1] += d.x * d.y; covariance[k, 2] += d.x * d.z;
                covariance[k, 3] += d.y * d.y; covariance[k, 4] += d.y * d.z; covariance[k, 5] += d.z * d.z;
            }

            for (int k = 0; k < budget; k++)
            {
                var c = cands[chosen[k]];
                Color color = counts[k] > 0 ? colorSum[k] / counts[k] : c.color;
                Vector3 normal = normalSum[k].sqrMagnitude > 1e-8f ? normalSum[k].normalized : c.normal;
                var anchor = new Anchor
                {
                    part = part.name,
                    organ = part.organ,
                    position = c.position,
                    center = centers[k],
                    normal = normal,
                    // 贴图是 sRGB，项目是线性色彩空间，实例颜色要转成线性才和模型看起来一致
                    color = color.linear,
                    spacing = spacing,
                };
                PrincipalFrame(covariance, k, Mathf.Max(counts[k], 1), normal, anchor);

                // 实际包围范围：把归属点投影到主方向上量最大最小值
                Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                Vector3 axisMid = Vector3.Cross(anchor.axisShort, anchor.axisLong).normalized;
                for (int i = 0; i < n; i++)
                {
                    if (owners[i] != k) continue;
                    Vector3 d = cands[i].position - centers[k];
                    var q = new Vector3(Vector3.Dot(d, anchor.axisLong), Vector3.Dot(d, axisMid), Vector3.Dot(d, anchor.axisShort));
                    min = Vector3.Min(min, q);
                    max = Vector3.Max(max, q);
                }
                anchor.extent = counts[k] > 1 ? max - min : Vector3.one * spacing;
                result.Add(anchor);
            }
            return result;
        }

        // 3x3 对称协方差矩阵的主方向（幂迭代）。叶片用法线作为最短轴，在切平面里找最长轴，更稳定
        static void PrincipalFrame(float[,] cov, int k, int count, Vector3 normal, Anchor anchor)
        {
            float xx = cov[k, 0] / count, xy = cov[k, 1] / count, xz = cov[k, 2] / count;
            float yy = cov[k, 3] / count, yz = cov[k, 4] / count, zz = cov[k, 5] / count;
            Vector3 Mul(Vector3 v) => new Vector3(
                xx * v.x + xy * v.y + xz * v.z,
                xy * v.x + yy * v.y + yz * v.z,
                xz * v.x + yz * v.y + zz * v.z);

            Vector3 shortAxis = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            // 在垂直于最短轴的平面里迭代
            Vector3 seed = Mathf.Abs(shortAxis.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 v1 = Vector3.ProjectOnPlane(seed, shortAxis).normalized;
            for (int it = 0; it < 16; it++)
            {
                Vector3 next = Vector3.ProjectOnPlane(Mul(v1), shortAxis);
                if (next.sqrMagnitude < 1e-16f) break;
                v1 = next.normalized;
            }
            anchor.axisLong = v1;
            anchor.axisShort = shortAxis;
        }

        static List<PlantNode> BuildNodes(List<Anchor> samples, Settings s, System.Random rng)
        {
            var tree = samples.FindAll(x => x.organ != Organ.Soil && x.organ != Organ.Bug);
            var soil = samples.FindAll(x => x.organ == Organ.Soil);
            var bugs = samples.FindAll(x => x.organ == Organ.Bug);
            var nodes = new List<PlantNode>();
            var nodeSamples = new List<Anchor>();

            // Prim：从最低的茎节点开始，按插入顺序编号，保证父节点排在子节点前面
            int start = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < tree.Count; i++)
            {
                if (tree[i].organ == Organ.Stem && tree[i].position.y < lowest)
                {
                    lowest = tree[i].position.y;
                    start = i;
                }
            }

            int count = tree.Count;
            var inTree = new bool[count];
            var bestCost = new float[count];
            var bestParent = new int[count];
            var newId = new int[count];
            for (int i = 0; i < count; i++)
            {
                bestCost[i] = float.MaxValue;
                bestParent[i] = -1;
            }
            bestCost[start] = 0f;

            for (int step = 0; step < count; step++)
            {
                int u = -1;
                for (int i = 0; i < count; i++)
                    if (!inTree[i] && (u < 0 || bestCost[i] < bestCost[u])) u = i;
                inTree[u] = true;
                newId[u] = nodes.Count;
                AddNode(nodes, nodeSamples, tree[u], bestParent[u] < 0 ? -1 : newId[bestParent[u]]);

                for (int v = 0; v < count; v++)
                {
                    if (inTree[v]) continue;
                    float cost = Vector3.Distance(tree[u].position, tree[v].position) * EdgePenalty(tree[u], tree[v]);
                    if (cost < bestCost[v])
                    {
                        bestCost[v] = cost;
                        bestParent[v] = u;
                    }
                }
            }

            // 土挂在最近的根（没有根就挂茎底部）
            foreach (var clod in soil)
            {
                int parent = 0;
                float best = float.MaxValue;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].organ != Organ.Root && i != 0) continue;
                    float d = (nodeSamples[i].position - clod.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        parent = i;
                    }
                }
                AddNode(nodes, nodeSamples, clod, parent);
            }

            // 模型里的虫子挂在最近的叶片上；没有虫子模型时随机放几只假虫子
            foreach (var bug in bugs)
                AddNode(nodes, nodeSamples, bug, NearestNode(nodes, nodeSamples, bug.position, Organ.Leaf));
            if (bugs.Count == 0) AddBugs(nodes, nodeSamples, s, rng);

            foreach (var node in nodes) node.depth = node.parentId < 0 ? 0 : nodes[node.parentId].depth + 1;
            BuildRealPoses(nodes, nodeSamples);
            return nodes;
        }

        // 离 position 最近的某部位节点；没有这个部位时返回离它最近的任意节点
        static int NearestNode(List<PlantNode> nodes, List<Anchor> nodeSamples, Vector3 position, Organ organ)
        {
            int best = -1, fallback = 0;
            float bestDist = float.MaxValue, fallbackDist = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                float d = (nodeSamples[i].position - position).sqrMagnitude;
                if (nodes[i].organ == organ && d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
                if (d < fallbackDist)
                {
                    fallbackDist = d;
                    fallback = i;
                }
            }
            return best >= 0 ? best : fallback;
        }

        // 同一部件内部连线最便宜；茎和其他部位之间次之；果实和果实、叶和果实之间很贵
        static float EdgePenalty(Anchor a, Anchor b)
        {
            if (a.part == b.part) return 1f;
            bool aStem = a.organ == Organ.Stem, bStem = b.organ == Organ.Stem;
            if (aStem || bStem)
            {
                Organ other = aStem ? b.organ : a.organ;
                return other == Organ.Stem ? 1f : 1.5f;
            }
            if (a.organ == Organ.Leaf && b.organ == Organ.Leaf) return 1.5f;
            if (a.organ == Organ.Root || b.organ == Organ.Root) return 10f;
            return 6f;
        }

        static void AddNode(List<PlantNode> nodes, List<Anchor> nodeSamples, Anchor sample, int parentId)
        {
            nodes.Add(new PlantNode
            {
                id = nodes.Count,
                organ = sample.organ,
                part = sample.part,
                parentId = parentId,
                realColor = sample.color,
            });
            nodeSamples.Add(sample);
        }

        // 虫子贴在叶背：从叶节点里挑，沿法线反方向偏一点
        static void AddBugs(List<PlantNode> nodes, List<Anchor> nodeSamples, Settings s, System.Random rng)
        {
            var leaves = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].organ == Organ.Leaf) leaves.Add(i);

            for (int b = 0; b < s.bugCount && leaves.Count > 0; b++)
            {
                int pick = rng.Next(leaves.Count);
                int leaf = leaves[pick];
                leaves.RemoveAt(pick);
                var ls = nodeSamples[leaf];
                AddNode(nodes, nodeSamples, new Anchor
                {
                    part = "bug",
                    organ = Organ.Bug,
                    position = ls.position - ls.normal * 0.008f,
                    normal = -ls.normal,
                    color = OrganPalette.RealColor(Organ.Bug),
                    spacing = 0.01f,
                }, leaf);
            }
        }

        // 茎和根顺着父节点方向拉长，前后相接；叶是贴着表面的薄片；果实、土是小方块
        static void BuildRealPoses(List<PlantNode> nodes, List<Anchor> nodeSamples)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var sample = nodeSamples[i];
                float sp = sample.spacing;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, sample.normal);
                Vector3 position = sample.position;
                Vector3 scale;

                switch (node.organ)
                {
                    case Organ.Stem:
                    case Organ.Root:
                    {
                        bool stem = node.organ == Organ.Stem;
                        float thickness = stem ? 0.01f : 0.006f;
                        float length = sp;
                        if (node.parentId >= 0)
                        {
                            Vector3 toParent = nodeSamples[node.parentId].position - sample.position;
                            if (toParent.sqrMagnitude > 1e-8f)
                            {
                                rot = Quaternion.FromToRotation(Vector3.up, toParent.normalized);
                                length = Mathf.Clamp(toParent.magnitude, 0.01f, 0.08f);
                                // 方块从节点朝父节点伸出去，相邻两段首尾相接
                                position = sample.position + toParent.normalized * length * 0.5f;
                            }
                        }
                        scale = new Vector3(thickness, length, thickness);
                        break;
                    }
                    case Organ.Leaf:
                    {
                        // 菱形叶片：长轴沿叶片走向，短轴是法线；比实际覆盖范围略大一点，相邻叶片能接上
                        Vector3 e = sample.extent;
                        rot = Quaternion.LookRotation(sample.axisLong, sample.axisShort);
                        position = sample.center;
                        scale = new Vector3(
                            Mathf.Clamp(e.y * 1.3f, 0.012f, 0.09f),
                            0.006f,
                            Mathf.Clamp(e.x * 1.3f, 0.015f, 0.12f));
                        break;
                    }
                    case Organ.Fruit:
                    {
                        // 每个果实只有几个节点，各自是覆盖半个果实的椭球
                        Vector3 e = sample.extent;
                        rot = Quaternion.LookRotation(sample.axisLong, sample.axisShort);
                        position = sample.center;
                        scale = new Vector3(
                            Mathf.Clamp(e.y, 0.02f, 0.08f),
                            Mathf.Clamp(Mathf.Max(e.z, e.y * 0.6f), 0.02f, 0.08f),
                            Mathf.Clamp(e.x, 0.02f, 0.1f));
                        break;
                    }
                    case Organ.Soil:
                        scale = Vector3.one * Mathf.Clamp(sp * 0.7f, 0.012f, 0.035f);
                        break;
                    case Organ.Bug:
                        if (sample.extent.sqrMagnitude > 0f)
                        {
                            // 虫子模型：整只虫子是一个节点，按实际大小
                            Vector3 e = sample.extent;
                            rot = Quaternion.LookRotation(sample.axisLong, sample.axisShort);
                            position = sample.center;
                            scale = new Vector3(
                                Mathf.Clamp(e.y, 0.004f, 0.04f),
                                Mathf.Clamp(Mathf.Max(e.z, e.y * 0.5f), 0.004f, 0.04f),
                                Mathf.Clamp(e.x, 0.006f, 0.05f));
                        }
                        else
                        {
                            scale = new Vector3(0.008f, 0.005f, 0.012f);
                        }
                        break;
                    default:
                        scale = Vector3.one * Mathf.Clamp(sp, 0.01f, 0.03f);
                        break;
                }

                var pose = new NodePose(position, rot, scale);
                for (int f = 0; f < PlantNode.FormCount; f++) node.poses[f] = pose;
            }
        }

        // 贴图默认不可读，直接读磁盘上的图片文件解码一份可读的副本（只在编辑器里用）
        static Texture2D LoadReadableTexture(Material material, Dictionary<Texture, Texture2D> cache)
        {
            if (material == null) return null;
            var source = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            if (source == null) return null;
            if (cache.TryGetValue(source, out var cached)) return cached;

            Texture2D readable = null;
            string path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!readable.LoadImage(File.ReadAllBytes(path)))
                {
                    Object.DestroyImmediate(readable);
                    readable = null;
                }
            }
            if (readable == null) Debug.LogWarning($"[Morph] 读不到贴图 {path}，节点用部位默认色");
            cache[source] = readable;
            return readable;
        }
    }
}
