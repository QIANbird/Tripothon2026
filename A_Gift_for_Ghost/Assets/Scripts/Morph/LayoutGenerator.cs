using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Morph
{
    // 从写实形态（Real）往回推，生成其余 4 种形态的姿态：
    // Real → Geometric（原位，形状规整化）→ Network（保留拓扑，拉开打散）
    // → Circuit（投影到平面，吸附网格）→ Matrix（打乱顺序，排成方阵）。
    // 所有形态共用同一个中心点，固定相机下变形始终在画面中央。平面形态都在本地 XY 平面上，面朝 -Z（相机方向）。
    public static class LayoutGenerator
    {
        [System.Serializable]
        public class Settings
        {
            public int seed = 11;
            // 所有形态的中心，约在植株中部
            public Vector3 center = new Vector3(0f, 0.32f, 0f);

            [Header("Geometric")]
            public float geometricShrink = 0.85f;
            public float geometricStemGap = 0.75f;

            [Header("Network")]
            // 1.0 = 外轮廓和 Geometric / Real 基本一致：S3→S4 变形时节点几乎原地不动，只是方块"长成"叶片和三角片。
            // （原来 1.8，变形时整体往中心缩，看起来在变小）
            public float networkSpread = 1.0f;
            public float networkJitter = 0.05f;
            public float networkNodeSize = 0.018f;

            [Header("Circuit")]
            public float circuitScale = 1.5f;
            // 用 z 把前后重叠的节点在平面上错开
            public float circuitDepthShear = 0.6f;
            public float circuitCell = 0.035f;
            public Vector3 circuitNodeSize = new Vector3(0.024f, 0.024f, 0.006f);

            [Header("Matrix")]
            public float matrixSpacing = 0.045f;
            public float matrixNodeSize = 0.032f;
        }

        public static void BuildAll(List<PlantNode> nodes, Settings s)
        {
            BuildGeometric(nodes, s);
            BuildNetwork(nodes, s);
            BuildCircuit(nodes, s);
            BuildMatrix(nodes, s);
        }

        // 节点留在原位，保持写实形态的朝向和比例，整体缩小一点，块与块之间露出缝隙，读起来是“拼起来的植株”。
        // 叶、果实等换成部位形状由 NodeShapes 负责
        public static void BuildGeometric(List<PlantNode> nodes, Settings s)
        {
            foreach (var node in nodes)
            {
                var real = node.GetPose(MorphForm.Real);
                Vector3 scale = real.scale;
                if (node.organ == Organ.Stem || node.organ == Organ.Root)
                    // 茎、根只缩短，不变细，否则太细看不见
                    scale.y *= s.geometricStemGap;
                else
                    scale *= s.geometricShrink;
                node.SetPose(MorphForm.Geometric, new NodePose(real.position, real.rotation, scale));
            }
        }

        // 以中心为原点放大，再加随机抖动，节点统一成小方块。连线（父子关系）不变
        public static void BuildNetwork(List<PlantNode> nodes, Settings s)
        {
            var rng = new System.Random(s.seed);
            foreach (var node in nodes)
            {
                var real = node.GetPose(MorphForm.Real);
                Vector3 offset = (real.position - s.center) * s.networkSpread;
                Vector3 jitter = RandomInSphere(rng) * s.networkJitter;
                var pose = new NodePose(s.center + offset + jitter, Quaternion.identity,
                    Vector3.one * s.networkNodeSize);
                node.SetPose(MorphForm.Network, pose);
            }
        }

        // 投影到 XY 平面并吸附网格；格子被占时找最近的空格，保证节点不重叠。
        // 连线在渲染时画成直角折线（M7）
        public static void BuildCircuit(List<PlantNode> nodes, Settings s)
        {
            var occupied = new HashSet<Vector2Int>();
            // 按深度从根往外放，根附近的节点优先拿到原位，结构更清晰
            var order = new List<PlantNode>(nodes);
            order.Sort((a, b) => a.depth != b.depth ? a.depth.CompareTo(b.depth) : a.id.CompareTo(b.id));

            foreach (var node in order)
            {
                Vector3 p = node.GetPose(MorphForm.Real).position - s.center;
                Vector2 flat = new Vector2(p.x + p.z * s.circuitDepthShear, p.y) * s.circuitScale;
                var cell = new Vector2Int(Mathf.RoundToInt(flat.x / s.circuitCell), Mathf.RoundToInt(flat.y / s.circuitCell));
                cell = NearestFreeCell(cell, occupied);
                occupied.Add(cell);

                Vector3 pos = s.center + new Vector3(cell.x * s.circuitCell, cell.y * s.circuitCell, 0f);
                node.SetPose(MorphForm.Circuit, new NodePose(pos, Quaternion.identity, s.circuitNodeSize));
            }
        }

        // 打乱顺序后排成接近正方形的方阵，看不出植物轮廓
        public static void BuildMatrix(List<PlantNode> nodes, Settings s)
        {
            int count = nodes.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            var slots = new int[count];
            for (int i = 0; i < count; i++) slots[i] = i;
            var rng = new System.Random(s.seed + 1);
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }

            Vector3 origin = s.center - new Vector3((cols - 1) * 0.5f, (rows - 1) * 0.5f, 0f) * s.matrixSpacing;
            for (int i = 0; i < count; i++)
            {
                int slot = slots[i];
                int col = slot % cols;
                // 从上往下排
                int row = rows - 1 - slot / cols;
                Vector3 pos = origin + new Vector3(col, row, 0f) * s.matrixSpacing;
                nodes[i].SetPose(MorphForm.Matrix, new NodePose(pos, Quaternion.identity, Vector3.one * s.matrixNodeSize));
            }
        }

        static Vector2Int NearestFreeCell(Vector2Int start, HashSet<Vector2Int> occupied)
        {
            if (!occupied.Contains(start)) return start;
            // 按环逐圈向外找，同一圈里取离起点最近的空格
            for (int radius = 1; radius < 64; radius++)
            {
                Vector2Int best = start;
                float bestDist = float.MaxValue;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
                        var cell = new Vector2Int(start.x + dx, start.y + dy);
                        if (occupied.Contains(cell)) continue;
                        float dist = dx * dx + dy * dy;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = cell;
                        }
                    }
                }
                if (bestDist < float.MaxValue) return best;
            }
            return start;
        }

        static Vector3 RandomInSphere(System.Random rng)
        {
            while (true)
            {
                var v = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f);
                if (v.sqrMagnitude <= 1f) return v;
            }
        }
    }
}
