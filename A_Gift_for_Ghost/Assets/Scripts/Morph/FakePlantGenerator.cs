using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Morph
{
    // 程序化生成一株假植物，只填写实形态（Real）的姿态，其余形态由布局生成器从它往回推。
    // 比例参考场景里的甜椒模型：主茎居中，枝条按黄金角螺旋排布，叶片偏大，果实挂在中下部，底部一团土。
    // 尺寸按真实植株（约 0.6 m 高），1 单位 = 1 米。
    public static class FakePlantGenerator
    {
        [System.Serializable]
        public class Settings
        {
            public int seed = 7;
            public float height = 0.6f;
            public int stemSegments = 14;
            public int branchCount = 10;
            public int maxFruits = 6;
            public int bugCount = 3;
            public int soilCount = 36;
            public int rootCount = 4;
            public float soilRadius = 0.13f;
        }

        // 生成过程的状态：节点列表 + 每个节点的"关节点"（子节点从这里长出去，和渲染用的 position 不同）
        class Builder
        {
            public readonly List<PlantNode> nodes = new List<PlantNode>();
            public readonly List<Vector3> joints = new List<Vector3>();
            public System.Random rng;

            public float Range(float min, float max)
            {
                return min + (float)rng.NextDouble() * (max - min);
            }

            public int Add(Organ organ, int parentId, Vector3 joint, NodePose real)
            {
                var node = new PlantNode
                {
                    id = nodes.Count,
                    organ = organ,
                    parentId = parentId,
                    depth = parentId < 0 ? 0 : nodes[parentId].depth + 1,
                };
                // 其他形态先复制写实姿态，保证数据完整；M3 的布局生成器会覆盖
                for (int f = 0; f < PlantNode.FormCount; f++) node.poses[f] = real;
                nodes.Add(node);
                joints.Add(joint);
                return node.id;
            }

            // 从父节点关节沿 dir 长出一段茎，方块中心放在这段茎的中点
            public int AddSegment(Organ organ, int parentId, Vector3 dir, float length, float thickness)
            {
                Vector3 start = joints[parentId];
                Vector3 end = start + dir * length;
                var pose = new NodePose(
                    (start + end) * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, dir),
                    new Vector3(thickness, length, thickness));
                return Add(organ, parentId, end, pose);
            }
        }

        public static List<PlantNode> Generate(Settings s)
        {
            var b = new Builder { rng = new System.Random(s.seed) };

            int root = b.Add(Organ.Root, -1, Vector3.zero,
                new NodePose(Vector3.zero, Quaternion.identity, Vector3.one * 0.02f));

            AddRoots(b, s, root);
            AddSoil(b, s, root);

            var stem = AddMainStem(b, s, root);
            var leaves = new List<int>();
            int fruits = 0;

            // 枝条：从主茎下 1/5 到上 1/10 之间均匀取起点，方位角按黄金角递增
            for (int i = 0; i < s.branchCount; i++)
            {
                float t = s.branchCount == 1 ? 0.5f : (float)i / (s.branchCount - 1);
                int stemIndex = Mathf.RoundToInt(Mathf.Lerp(stem.Count * 0.25f, stem.Count * 0.9f, t));
                stemIndex = Mathf.Clamp(stemIndex, 0, stem.Count - 1);
                float azimuth = i * 137.5f + b.Range(-15f, 15f);
                // 下面的枝条更长、更平展，上面的更短、更直立
                int segments = Mathf.RoundToInt(Mathf.Lerp(5, 2, t));
                float rise = Mathf.Lerp(0.35f, 0.75f, t);
                bool canFruit = t < 0.75f && fruits < s.maxFruits;
                fruits += AddBranch(b, stem[stemIndex], azimuth, segments, rise, canFruit, leaves);
            }

            // 顶端：一个花苞和两片嫩叶
            int top = stem[stem.Count - 1];
            AddBud(b, top, Vector3.up);
            for (int i = 0; i < 2; i++)
            {
                float az = i * 180f + b.Range(-20f, 20f);
                leaves.Add(AddLeaf(b, top, Horizontal(az), 0.7f));
            }

            AddBugs(b, s, leaves);
            return b.nodes;
        }

        static void AddRoots(Builder b, Settings s, int root)
        {
            for (int i = 0; i < s.rootCount; i++)
            {
                float az = i * 360f / s.rootCount + b.Range(-20f, 20f);
                int parent = root;
                for (int seg = 0; seg < 2; seg++)
                {
                    Vector3 dir = (Horizontal(az) * (0.6f + seg * 0.4f) + Vector3.down).normalized;
                    parent = b.AddSegment(Organ.Root, parent, dir, 0.045f, 0.008f - seg * 0.002f);
                }
            }
        }

        static void AddSoil(Builder b, Settings s, int root)
        {
            for (int i = 0; i < s.soilCount; i++)
            {
                // sqrt 让土块在圆盘上均匀分布；高度做成中间高的土堆
                float r = Mathf.Sqrt(b.Range(0f, 1f)) * s.soilRadius;
                float az = b.Range(0f, 360f);
                float mound = 0.035f * (1f - (r / s.soilRadius) * (r / s.soilRadius));
                Vector3 pos = Horizontal(az) * r + Vector3.up * (mound + b.Range(0f, 0.008f));
                float size = b.Range(0.014f, 0.028f);
                var rot = Quaternion.Euler(b.Range(0f, 360f), b.Range(0f, 360f), b.Range(0f, 360f));
                b.Add(Organ.Soil, root, pos, new NodePose(pos, rot, Vector3.one * size));
            }
        }

        static List<int> AddMainStem(Builder b, Settings s, int root)
        {
            var stem = new List<int>();
            float segLen = s.height * 0.85f / s.stemSegments;
            Vector3 dir = Vector3.up;
            int parent = root;
            for (int i = 0; i < s.stemSegments; i++)
            {
                float t = (float)i / (s.stemSegments - 1);
                // 轻微摆动，再往竖直方向拉回，避免歪倒
                Vector3 wobble = new Vector3(b.Range(-1f, 1f), 0f, b.Range(-1f, 1f)) * 0.12f;
                dir = Vector3.Lerp(dir + wobble, Vector3.up, 0.5f).normalized;
                parent = b.AddSegment(Organ.Stem, parent, dir, segLen, Mathf.Lerp(0.016f, 0.007f, t));
                stem.Add(parent);
            }
            return stem;
        }

        // 返回在这根枝条上挂了几个果实
        static int AddBranch(Builder b, int stemNode, float azimuth, int segments, float rise,
            bool canFruit, List<int> leaves)
        {
            int fruits = 0;
            Vector3 outward = Horizontal(azimuth);
            Vector3 dir = (outward * (1f - rise) + Vector3.up * rise).normalized;
            int parent = stemNode;
            float segLen = 0.055f;
            int fruitSegment = canFruit ? b.rng.Next(1, Mathf.Max(2, segments - 1)) : -1;

            for (int seg = 0; seg < segments; seg++)
            {
                // 越往外越向水平方向弯，模拟枝条受重力
                dir = Vector3.Lerp(dir, (outward + Vector3.up * 0.2f).normalized, 0.25f).normalized;
                parent = b.AddSegment(Organ.Stem, parent, dir, segLen, Mathf.Lerp(0.007f, 0.004f, (float)seg / segments));

                // 叶片左右交替
                float side = seg % 2 == 0 ? 70f : -70f;
                leaves.Add(AddLeaf(b, parent, Horizontal(azimuth + side + b.Range(-15f, 15f)), 1f));

                if (seg == fruitSegment)
                {
                    AddFruit(b, parent);
                    fruits++;
                }
            }

            // 枝梢：一片叶，偶尔加一个花苞
            leaves.Add(AddLeaf(b, parent, outward, 0.85f));
            if (b.Range(0f, 1f) < 0.5f) AddBud(b, parent, dir);
            return fruits;
        }

        // 一片叶子由两个节点组成：叶片内侧（宽）和外侧（窄），外侧以内侧为父节点
        static int AddLeaf(Builder b, int parent, Vector3 outward, float sizeScale)
        {
            float length = b.Range(0.075f, 0.095f) * sizeScale;
            float width = b.Range(0.055f, 0.07f) * sizeScale;
            // 叶片向外伸并略微下垂
            Vector3 forward = (outward + Vector3.down * b.Range(0.1f, 0.4f)).normalized;
            Vector3 normal = Vector3.Cross(forward, Vector3.Cross(Vector3.up, forward)).normalized;
            var rot = Quaternion.LookRotation(forward, normal);

            Vector3 start = b.joints[parent] + forward * 0.012f;
            Vector3 innerCenter = start + forward * length * 0.5f;
            int inner = b.Add(Organ.Leaf, parent, start + forward * length,
                new NodePose(innerCenter, rot, new Vector3(width, 0.004f, length)));

            // 外侧那段更下垂一点
            Vector3 tipForward = (forward + Vector3.down * 0.25f).normalized;
            var tipRot = Quaternion.LookRotation(tipForward, normal);
            Vector3 tipCenter = b.joints[inner] + tipForward * length * 0.35f;
            b.Add(Organ.Leaf, inner, b.joints[inner] + tipForward * length * 0.7f,
                new NodePose(tipCenter, tipRot, new Vector3(width * 0.6f, 0.004f, length * 0.7f)));
            return inner;
        }

        static void AddFruit(Builder b, int parent)
        {
            // 果实垂在枝条下方
            Vector3 pos = b.joints[parent] + Vector3.down * 0.045f + Horizontal(b.Range(0f, 360f)) * 0.01f;
            var rot = Quaternion.Euler(b.Range(-10f, 10f), b.Range(0f, 360f), b.Range(-10f, 10f));
            b.Add(Organ.Fruit, parent, pos, new NodePose(pos, rot, new Vector3(0.05f, 0.06f, 0.05f)));
        }

        static void AddBud(Builder b, int parent, Vector3 dir)
        {
            Vector3 pos = b.joints[parent] + dir * 0.012f;
            b.Add(Organ.Bud, parent, pos,
                new NodePose(pos, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(0.012f, 0.016f, 0.012f)));
        }

        // 虫子贴在叶片背面（S3 的玩法：旋转结构才能看到）
        static void AddBugs(Builder b, Settings s, List<int> leaves)
        {
            var candidates = new List<int>(leaves);
            for (int i = 0; i < s.bugCount && candidates.Count > 0; i++)
            {
                int pick = b.rng.Next(candidates.Count);
                int leaf = candidates[pick];
                candidates.RemoveAt(pick);

                var leafPose = b.nodes[leaf].GetPose(MorphForm.Real);
                Vector3 underside = leafPose.rotation * Vector3.down;
                Vector3 pos = leafPose.position + underside * 0.006f;
                b.Add(Organ.Bug, leaf, pos, new NodePose(pos, leafPose.rotation, new Vector3(0.008f, 0.005f, 0.012f)));
            }
        }

        static Vector3 Horizontal(float azimuthDegrees)
        {
            float rad = azimuthDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        }
    }
}
