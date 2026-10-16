using System;
using System.Collections.Generic;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Stages
{
    // S1 / S2 共用的"缺失节点"挑选（docs/tasks/s1-s2-missing-nodes.md）。纯函数：同样的节点集 + 覆盖列表永远得到同样的结果，
    // 跳关直接进 S2 时和走完 S1 看到的是同一批节点。
    //
    // 规则：叶子 / 茎（经脉）/ 果实 / 根系各挑一个"中间节点"——自己有连线，并且至少一个子节点有连线，这样缺口两侧都有断线。
    // 同一器官有多个候选时：优先不是枯叶（part 含 "dying"，S2 的缺水节点）的，再按有连线的子节点数多的、id 小的。
    // 没有中间节点时退回到该器官的末端节点（只有父侧断线），并打 Warning。
    public static class MissingNodePicker
    {
        // 挑选顺序，也是返回列表的顺序
        public static readonly Organ[] Organs = { Organ.Leaf, Organ.Stem, Organ.Fruit, Organ.Root };

        // hasLink：子节点 id → 它和父节点之间有没有连线（传 NodeLinkRenderer.HasLink）。
        // overrides：可选覆盖，按 Organs 的顺序；某一位 < 0 或越界时这一位照常自动挑选
        public static List<int> Pick(PlantNodeSet set, Func<int, bool> hasLink, IList<int> overrides = null)
        {
            var result = new List<int>(Organs.Length);
            if (set == null || set.Count == 0 || hasLink == null) return result;

            var linkedChildren = new int[set.Count];
            foreach (var n in set.nodes)
                if (n.parentId >= 0 && hasLink(n.id)) linkedChildren[n.parentId]++;

            for (int o = 0; o < Organs.Length; o++)
            {
                Organ organ = Organs[o];
                int id = -1;
                if (overrides != null && o < overrides.Count && overrides[o] >= 0)
                {
                    var forced = set.Get(overrides[o]);
                    if (forced != null) id = forced.id;
                    else Debug.LogWarning($"[MissingNode] 覆盖列表第 {o} 位 id {overrides[o]} 不存在，改为自动挑选");
                }

                if (id < 0) id = Best(set, organ, hasLink, linkedChildren, requireChild: true);
                if (id < 0)
                {
                    id = Best(set, organ, hasLink, linkedChildren, requireChild: false);
                    if (id >= 0) Debug.LogWarning($"[MissingNode] {organ} 没有中间节点，退回到末端节点 {id}（缺口只有父侧断线）");
                }
                if (id < 0)
                {
                    Debug.LogWarning($"[MissingNode] {organ} 没有可用节点，跳过");
                    continue;
                }
                if (result.Contains(id))
                {
                    Debug.LogWarning($"[MissingNode] 节点 {id} 被重复选中（{organ}），跳过");
                    continue;
                }
                result.Add(id);
            }

            Debug.Log($"[MissingNode] 缺失节点：{Describe(set, result)}");
            return result;
        }

        static int Best(PlantNodeSet set, Organ organ, Func<int, bool> hasLink, int[] linkedChildren, bool requireChild)
        {
            int best = -1;
            int bestScore = int.MinValue;
            foreach (var n in set.nodes)
            {
                if (n.organ != organ || n.parentId < 0 || !hasLink(n.id)) continue;
                if (requireChild && linkedChildren[n.id] == 0) continue;
                bool dying = !string.IsNullOrEmpty(n.part) && n.part.Contains("dying");
                int score = (dying ? 0 : 1000) + linkedChildren[n.id];
                // 遍历按 id 升序，分数相同时保留先遇到的（id 小的）
                if (score > bestScore)
                {
                    bestScore = score;
                    best = n.id;
                }
            }
            return best;
        }

        // S2 的散落姿态（植株本地空间，和 missing 一一对应）：其余节点 Circuit 布局的 XY 包围盒向外留 margin，
        // 按左 / 右 / 下方摆放（叶子左、果实右、茎和根在下方左右两侧）。位置固定，不随机。
        // margin 按节点边长计；旋转和缩放沿用节点自己的 Circuit 姿态
        public static NodePose[] ScatterPoses(PlantNodeSet set, IList<int> missing, float margin = 1.5f)
        {
            var poses = new NodePose[missing != null ? missing.Count : 0];
            if (set == null || poses.Length == 0) return poses;

            var excluded = new HashSet<int>(missing);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            float size = 0f;
            foreach (var n in set.nodes)
            {
                var p = n.GetPose(MorphForm.Circuit);
                size = Mathf.Max(size, Mathf.Abs(p.scale.x));
                if (excluded.Contains(n.id)) continue;
                min = Vector3.Min(min, p.position);
                max = Vector3.Max(max, p.position);
            }
            if (float.IsInfinity(min.x)) min = max = Vector3.zero;
            float pad = size * margin;
            float z = (min.z + max.z) * 0.5f;
            float midY = (min.y + max.y) * 0.5f;
            float bottomY = min.y - pad;
            float left = min.x - pad, right = max.x + pad;
            float thirdL = Mathf.Lerp(min.x, max.x, 1f / 3f), thirdR = Mathf.Lerp(min.x, max.x, 2f / 3f);

            for (int i = 0; i < poses.Length; i++)
            {
                var node = set.Get(missing[i]);
                if (node == null) continue;
                Vector3 pos;
                switch (node.organ)
                {
                    case Organ.Leaf: pos = new Vector3(left, midY, z); break;
                    case Organ.Fruit: pos = new Vector3(right, midY, z); break;
                    case Organ.Stem: pos = new Vector3(thirdL, bottomY, z); break;
                    case Organ.Root: pos = new Vector3(thirdR, bottomY, z); break;
                    // 覆盖列表里放了别的器官：沿底边往右排
                    default: pos = new Vector3(Mathf.Lerp(min.x, max.x, (i + 0.5f) / poses.Length), bottomY - pad, z); break;
                }
                var circuit = node.GetPose(MorphForm.Circuit);
                poses[i] = new NodePose(pos, circuit.rotation, circuit.scale);
            }
            return poses;
        }

        static string Describe(PlantNodeSet set, List<int> ids)
        {
            var parts = new List<string>(ids.Count);
            foreach (int id in ids)
            {
                var n = set.Get(id);
                parts.Add(n == null ? id.ToString() : $"{n.organ} {id}{(string.IsNullOrEmpty(n.part) ? "" : $" ({n.part})")}");
            }
            return string.Join(", ", parts);
        }
    }
}
