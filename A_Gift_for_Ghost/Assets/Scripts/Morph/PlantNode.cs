using System;
using UnityEngine;

namespace Ghost.Morph
{
    // 节点代表植物的哪个部位。决定写实/几何形态下的形状和颜色，也供各阶段玩法筛选（比如 S3 找 Bug）
    public enum Organ
    {
        Soil,
        Root,
        Stem,
        Leaf,
        Bud,
        Fruit,
        Bug,
    }

    // 同一组节点的 5 种排布，抽象程度从高到低。
    // S1=Matrix，S2=Circuit，S3 从 Network 收拢到 Geometric，S4=Real。
    public enum MorphForm
    {
        Matrix = 0,
        Circuit = 1,
        Network = 2,
        Geometric = 3,
        Real = 4,
    }

    // 节点在某一种形态下的姿态（相对 NodeMorpher 所在物体的本地空间）
    [Serializable]
    public struct NodePose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public NodePose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public static NodePose Lerp(NodePose a, NodePose b, float t)
        {
            return new NodePose(
                Vector3.LerpUnclamped(a.position, b.position, t),
                Quaternion.SlerpUnclamped(a.rotation, b.rotation, t),
                Vector3.LerpUnclamped(a.scale, b.scale, t));
        }

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.TRS(position, rotation, scale);
        }
    }

    // 一个节点的全部数据。节点在所有形态里是同一个对象，只是姿态不同
    [Serializable]
    public class PlantNode
    {
        public int id;
        public Organ organ;
        // 植物拓扑里的父节点，根节点为 -1。S2 的回路连线和 S3 的网络连线都由它生成
        public int parentId = -1;
        // 离根节点的层数，用于错峰动画（从根往外长）
        public int depth;
        // 下标对应 MorphForm
        public NodePose[] poses = new NodePose[FormCount];

        public const int FormCount = 5;

        public NodePose GetPose(MorphForm form)
        {
            return poses[(int)form];
        }

        public void SetPose(MorphForm form, NodePose pose)
        {
            poses[(int)form] = pose;
        }
    }
}
