using System;
using System.Collections.Generic;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Narrative
{
    // 详情深度，对应 PROJECT_SUMMARY 第 4 节：详情随阶段越来越具体
    public enum DetailDepth
    {
        Status = 0,   // S1（及教学）：只有任务状态、进度、置信度
        Project = 1,  // S2：项目语言的模糊描述（根 = 支撑系统，叶 = 边缘系统……）
        Physical = 2, // S3（及以后）：物体本身的颜色、形状、纹理、质感
    }

    // 节点详情表：部位 × 深度 → 一段文字，另可按节点 id 单独覆盖。策划直接在 Inspector 里改。
    // 文字里可以写占位符，由阶段脚本传值替换：{status} {progress} {confidence} {part} {id}，
    // 以及任意自定义的 {key}。
    [CreateAssetMenu(menuName = "Ghost/Node Detail Table", fileName = "NodeDetails")]
    public class NodeDetailTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Organ organ;
            [TextArea(2, 6)] public string status;
            [TextArea(2, 6)] public string project;
            [TextArea(2, 6)] public string physical;

            public string Get(DetailDepth depth)
            {
                switch (depth)
                {
                    case DetailDepth.Status: return status;
                    case DetailDepth.Project: return project;
                    default: return physical;
                }
            }
        }

        [Serializable]
        public class NodeOverride
        {
            public int nodeId;
            public DetailDepth depth;
            [TextArea(2, 6)] public string text;
        }

        [Tooltip("每个部位一行（Soil、Root、Stem、Leaf、Bud、Fruit、Bug）")]
        public List<Entry> entries = new List<Entry>();
        [Tooltip("个别节点的专属文字，优先于 entries")]
        public List<NodeOverride> overrides = new List<NodeOverride>();
        [Tooltip("查不到时显示的文字")]
        public string fallback = "暂无数据";

        // 按部位和深度取原始文字（不替换占位符）
        public string Get(Organ organ, DetailDepth depth)
        {
            foreach (var e in entries)
                if (e.organ == organ)
                {
                    var text = e.Get(depth);
                    return string.IsNullOrEmpty(text) ? fallback : text;
                }
            return fallback;
        }

        // 先查节点覆盖，再按部位查
        public string Get(int nodeId, Organ organ, DetailDepth depth)
        {
            foreach (var o in overrides)
                if (o.nodeId == nodeId && o.depth == depth && !string.IsNullOrEmpty(o.text)) return o.text;
            return Get(organ, depth);
        }

        // 取文字并替换占位符，例如 Format(id, organ, DetailDepth.Status, ("status", "异常"), ("progress", 42))
        public string Format(int nodeId, Organ organ, DetailDepth depth, params (string key, object value)[] values)
        {
            return Fill(Get(nodeId, organ, depth), values);
        }

        public static string Fill(string template, params (string key, object value)[] values)
        {
            if (string.IsNullOrEmpty(template) || values == null) return template;
            foreach (var (key, value) in values)
                template = template.Replace("{" + key + "}", value?.ToString() ?? "");
            return template;
        }

        // 阶段 → 深度的默认对应（stageName 用 GameFlow 里的名字）。阶段脚本也可以直接指定深度
        public static DetailDepth DepthForStage(string stageName)
        {
            switch (stageName)
            {
                case "Tutorial":
                case "S1": return DetailDepth.Status;
                case "S2": return DetailDepth.Project;
                default: return DetailDepth.Physical;
            }
        }
    }
}
