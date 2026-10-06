using System.Collections.Generic;
using Ghost.Morph;
using Ghost.Narrative;
using UnityEngine;

namespace Ghost.Stages
{
    // S3（找虫）和 S4（除虫）共用的小工具：收集虫子节点、物体名、物体描述（Physical 档）
    public static class BugStageUtil
    {
        // S3 / S4 虫子节点的默认颜色：和叶片同是绿色系，但更黄更亮，仔细看才分得出
        public static readonly Color DefaultBugColor = new Color(0.46f, 0.58f, 0.10f);

        // 节点集里所有 Organ.Bug 节点，按 id 排序
        public static List<int> CollectBugs(PlantNodeSet set, string logTag, Object context)
        {
            var bugs = new List<int>();
            if (set == null) return bugs;
            foreach (var n in set.nodes)
                if (n.organ == Organ.Bug) bugs.Add(n.id);
            if (bugs.Count == 0)
                Debug.LogWarning($"[{logTag}] 节点集里没有 Organ.Bug 节点，本关无法通关（见 G9 待办：接入虫子模型）", context);
            else
                Debug.Log($"[{logTag}] 虫子节点 {bugs.Count} 个：{string.Join(", ", bugs)}", context);
            return bugs;
        }

        // 【占位】物体名，和 NodeDetails.asset 的 Physical 档对应
        public static string PhysicalTitle(PlantNode node)
        {
            string name;
            switch (node.organ)
            {
                case Organ.Soil: name = "土壤"; break;
                case Organ.Root: name = "根"; break;
                case Organ.Stem: name = "茎"; break;
                case Organ.Leaf: name = "叶片"; break;
                case Organ.Bud: name = "花苞"; break;
                case Organ.Fruit: name = "果实"; break;
                case Organ.Bug: name = "小虫"; break;
                default: name = "未知"; break;
            }
            return $"{name} #{node.id}";
        }

        // 物体描述（NodeDetailTable 的 Physical 档），extraLine 非空时另起一行附在后面
        public static string PhysicalBody(NodeDetailTable table, PlantNode node, string extraLine = null)
        {
            string body = table != null ? table.Get(node.id, node.organ, DetailDepth.Physical) : "";
            if (string.IsNullOrEmpty(extraLine)) return body;
            return string.IsNullOrEmpty(body) ? extraLine : body + "\n" + extraLine;
        }
    }
}
