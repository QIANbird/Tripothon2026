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

        public int NodeCount => morpher != null && morpher.CurrentPoses != null ? morpher.CurrentPoses.Length : 0;

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
            return Pick(ray, Filter, out distance);
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

                float radius = Mathf.Max(size * radiusFactor, minPickRadius);
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
            return best;
        }
    }
}
