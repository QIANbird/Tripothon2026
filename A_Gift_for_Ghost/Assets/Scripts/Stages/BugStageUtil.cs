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

        // 真实部位名，表里没填名字时的默认值
        public static string RealName(Organ organ)
        {
            switch (organ)
            {
                case Organ.Soil: return "土壤";
                case Organ.Root: return "根部";
                case Organ.Stem: return "茎";
                case Organ.Leaf: return "叶片";
                case Organ.Bud: return "花朵";
                case Organ.Fruit: return "果实";
                case Organ.Bug: return "虫";
                default: return "未知";
            }
        }

        // 详情标题（不带节点编号）：S3 用 NameKind.Physical，S4 用 NameKind.Real
        public static string Title(NodeDetailTable table, PlantNode node, NameKind kind)
        {
            string fallback = RealName(node.organ);
            return table != null ? table.GetName(node.organ, kind, fallback) : fallback;
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
