using System;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Interaction
{
    // 节点拾取：节点不是 GameObject，没有 Collider，所以用射线和每个节点的包围球求交。
    // 包围球中心 = 节点位置（NodeMorpher 本地空间 → 世界），半径 = 最大缩放分量 × radiusFactor，
    // 但不小于 minPickRadius，保证很小的节点也能点到。
    // 只依赖 NodeMorpher.CurrentPoses 和 transform；缩放接近 0 的节点视为隐藏，跳过。
    // 约 250 个节点每帧一次普通循环，开销可以忽略。
    public class NodePicker : MonoBehaviour
    {
        public NodeMorpher morpher;

        [Tooltip("包围球半径 = 节点最大缩放分量 × 这个系数（立方体内切 0.5，外接约 0.87）")]
        [Range(0.3f, 1f)] public float radiusFactor = 0.6f;

        [Tooltip("最小拾取半径（米），让很小的节点也能点到")]
        public float minPickRadius = 0.012f;

        [Tooltip("缩放（世界空间最大分量）小于这个值的节点视为隐藏，不参与拾取")]
        public float hiddenScale = 0.002f;

        // 额外筛选：返回 false 的节点不参与拾取（比如 S3 只允许点 Bug，或 G3 提交后跳过已隐藏节点）。
        // 为 null 时所有可见节点都参与
        public Func<int, bool> Filter { get; set; }

        // 命中后的修正（阶段用）：参数是射线和按 Filter 命中的节点（可能是 -1），返回实际算命中的节点。
        // 只作用于 Pick(ray, out distance)，悬停、点击、右键、VR 选中都走这里，判定一致。
        // S4：叶片的拾取球把虫子包住，射线够得着虫子时改成虫子。为 null 时不修正
        public Func<Ray, int, int> Resolver { get; set; }

        public int NodeCount => morpher != null && morpher.CurrentPoses != null ? morpher.CurrentPoses.Length : 0;

        // 替身拾取球：节点 nodeId 在植株本地姿态 pose 处多一个拾取球，命中时 Pick 照常返回 nodeId（同样经过 Filter）。
        // S2 的缺口用：槽位上没有节点，但要能右键看详情；返回缺失节点自己的 id，交互层不会拿到不存在的节点 id。
        // 阶段用 HitsAlias 区分射线命中的是替身还是节点本身。没注册时行为和原来完全一样
        readonly System.Collections.Generic.Dictionary<int, NodePose> aliases = new System.Collections.Generic.Dictionary<int, NodePose>();

        // 单个节点的最小拾取半径（米，世界空间），覆盖 minPickRadius。S4 的虫子很小，用它放大检测范围
        readonly System.Collections.Generic.Dictionary<int, float> minRadii = new System.Collections.Generic.Dictionary<int, float>();

        public void SetMinRadius(int nodeId, float radius)
        {
            minRadii[nodeId] = radius;
        }

        public void ClearMinRadii()
        {
            minRadii.Clear();
        }

        float MinRadius(int nodeId)
        {
            return minRadii.Count > 0 && minRadii.TryGetValue(nodeId, out float r) ? Mathf.Max(r, minPickRadius) : minPickRadius;
        }

        public void SetAlias(int nodeId, NodePose pose)
        {
            aliases[nodeId] = pose;
        }

        public void ClearAlias(int nodeId)
        {
            aliases.Remove(nodeId);
        }

        public void ClearAliases()
        {
            aliases.Clear();
        }

        // 射线对 nodeId 的命中是替身球（比节点本身更近，或只命中替身）
        public bool HitsAlias(Ray ray, int nodeId)
        {
            if (morpher == null || !aliases.TryGetValue(nodeId, out var alias)) return false;
            var poses = morpher.CurrentPoses;
            if (poses == null || nodeId < 0 || nodeId >= poses.Length) return false;
            float aliasHit = HitSphere(ray, alias, minPickRadius);
            if (float.IsPositiveInfinity(aliasHit)) return false;
            return aliasHit <= HitSphere(ray, poses[nodeId], MinRadius(nodeId));
        }

        // 射线到本地姿态 pose 的拾取球的距离；没命中或节点隐藏时返回 +∞
        float HitSphere(Ray ray, NodePose pose, float minRadius)
        {
            var t = morpher.transform;
            Vector3 lossy = t.lossyScale;
            float lossyMax = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            Vector3 s = pose.scale;
            float size = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))) * lossyMax;
            if (size < hiddenScale) return float.PositiveInfinity;

            float radius = Mathf.Max(size * radiusFactor, minRadius);
            Vector3 oc = t.TransformPoint(pose.position) - ray.origin;
            Vector3 dir = ray.direction.normalized;
            float along = Vector3.Dot(oc, dir);
            float perpSq = oc.sqrMagnitude - along * along;
            float rSq = radius * radius;
            if (perpSq > rSq) return float.PositiveInfinity;
            float hit = along - Mathf.Sqrt(rSq - perpSq);
            if (hit < 0f) hit = along + Mathf.Sqrt(rSq - perpSq); // 起点在球内
            return hit < 0f ? float.PositiveInfinity : hit;
        }


        // 节点部位；id 无效时返回 null
        public Organ? OrganOf(int id)
        {
            var node = morpher != null && morpher.nodeSet != null ? morpher.nodeSet.Get(id) : null;
            return node != null ? node.organ : (Organ?)null;
        }

        // 节点当前的世界坐标（供 UI 把详情弹窗放在节点旁边）
        public bool TryGetNodeWorldPosition(int id, out Vector3 position)
        {
            position = Vector3.zero;
            var poses = morpher != null ? morpher.CurrentPoses : null;
            if (poses == null || id < 0 || id >= poses.Length) return false;
            position = morpher.transform.TransformPoint(poses[id].position);
            return true;
        }

        // 返回射线命中的最近节点 id，没有命中返回 -1。distance 为射线起点到命中点的距离
        public int Pick(Ray ray, out float distance)
        {
            int node = Pick(ray, Filter, out distance);
            if (Resolver == null) return node;
            int resolved = Resolver(ray, node);
            if (resolved != node && resolved >= 0)
            {
                var poses = morpher.CurrentPoses;
                float d = poses != null && resolved < poses.Length ? HitSphere(ray, poses[resolved], MinRadius(resolved)) : distance;
                if (!float.IsPositiveInfinity(d)) distance = d;
            }
            return resolved;
        }

        public int Pick(Ray ray, Func<int, bool> filter, out float distance)
        {
            distance = float.PositiveInfinity;
            if (morpher == null || !morpher.isActiveAndEnabled) return -1;
            var poses = morpher.CurrentPoses;
            if (poses == null) return -1;
            // 写实模型完全显现时节点不再绘制
            if (morpher.RealReveal >= 1f) return -1;

            var t = morpher.transform;
            Vector3 lossy = t.lossyScale;
            float lossyMax = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            Vector3 origin = ray.origin;
            Vector3 dir = ray.direction.normalized;

            int best = -1;
            for (int i = 0; i < poses.Length; i++)
            {
                Vector3 s = poses[i].scale;
                float size = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))) * lossyMax;
                if (size < hiddenScale) continue;

                float radius = Mathf.Max(size * radiusFactor, MinRadius(i));
                Vector3 center = t.TransformPoint(poses[i].position);

                // 射线-球求交：只取射线前方的第一个交点
                Vector3 oc = center - origin;
                float along = Vector3.Dot(oc, dir);
                float perpSq = oc.sqrMagnitude - along * along;
                float rSq = radius * radius;
                if (perpSq > rSq) continue;
                float hit = along - Mathf.Sqrt(rSq - perpSq);
                if (hit < 0f) hit = along + Mathf.Sqrt(rSq - perpSq); // 起点在球内
                if (hit < 0f || hit >= distance) continue;
                if (filter != null && !filter(i)) continue;

                distance = hit;
                best = i;
            }

            // 替身拾取球（缺口）：和节点一起按距离比较
            foreach (var kv in aliases)
            {
                if (kv.Key < 0 || kv.Key >= poses.Length) continue;
                float hit = HitSphere(ray, kv.Value, minPickRadius);
                if (hit >= distance) continue;
                if (filter != null && !filter(kv.Key)) continue;
                distance = hit;
                best = kv.Key;
            }
            return best;
        }
    }
}
