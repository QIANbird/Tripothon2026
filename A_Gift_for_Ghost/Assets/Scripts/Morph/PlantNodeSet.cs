using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Morph
{
    // 一株植物的全部节点。技术美术生成并保存为资产，程序读取后挂交互（PROJECT_SUMMARY 5.3 共享接口 1）
    [CreateAssetMenu(fileName = "PlantNodeSet", menuName = "Ghost/Plant Node Set")]
    public class PlantNodeSet : ScriptableObject
    {
        public List<PlantNode> nodes = new List<PlantNode>();

        // 从模型采样时记录来源和归一化参数：节点坐标 = (模型根空间坐标 - sourceOffset) * sourceScale。
        // 写实阶段要把真模型叠到节点上时，按这两个值摆放模型。程序生成的植物没有来源
        public GameObject sourceModel;
        public Vector3 sourceOffset;
        public float sourceScale = 1f;

        public int Count => nodes.Count;

        public PlantNode Get(int id)
        {
            // 生成器保证 id 等于下标
            return id >= 0 && id < nodes.Count ? nodes[id] : null;
        }

        public PlantNode GetParent(PlantNode node)
        {
            return Get(node.parentId);
        }

        // 校验数据是否满足约定，返回 null 表示正常
        public string Validate()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) return $"节点 {i} 为空";
                if (n.id != i) return $"节点 {i} 的 id 是 {n.id}，应等于下标";
                if (n.parentId >= i) return $"节点 {i} 的父节点 {n.parentId} 必须排在它前面";
                if (n.poses == null || n.poses.Length != PlantNode.FormCount)
                    return $"节点 {i} 的姿态数量不是 {PlantNode.FormCount}";
            }
            return null;
        }
    }
}
