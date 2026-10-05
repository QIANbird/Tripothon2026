using UnityEngine;

namespace Ghost.Morph
{
    // 编辑器里用 Gizmos 预览节点数据，不参与游戏渲染。检查生成器和布局用
    public class PlantNodePreview : MonoBehaviour
    {
        public PlantNodeSet nodeSet;
        public MorphForm form = MorphForm.Real;
        public bool drawLinks = true;

        void OnDrawGizmos()
        {
            if (nodeSet == null) return;

            var nodes = nodeSet.nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var pose = node.GetPose(form);
                Gizmos.matrix = transform.localToWorldMatrix * pose.ToMatrix();
                Gizmos.color = OrganPalette.RealColor(node.organ);
                Gizmos.DrawCube(Vector3.zero, Vector3.one);
            }

            if (!drawLinks) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.35f, 0.45f, 0.6f);
            for (int i = 0; i < nodes.Count; i++)
            {
                var parent = nodeSet.GetParent(nodes[i]);
                if (parent == null) continue;
                Gizmos.DrawLine(parent.GetPose(form).position, nodes[i].GetPose(form).position);
            }
        }
    }
}
