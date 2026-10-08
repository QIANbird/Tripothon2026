using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Narrative
{
    // 台词表导入后的索引：段的 key（这一段第一句的 ID，例如 "S3_004"）→ DialogueSequence。
    // 由 DialogueCsvImporter 生成，运行时只读。阶段脚本以后可以用 Get("S3_004") 取对白，不用逐个拖资产。
    [CreateAssetMenu(menuName = "Ghost/Dialogue Library", fileName = "DialogueLibrary")]
    public class DialogueLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("这一段第一句的 ID")]
            public string key;
            [Tooltip("台词表里的段落（INTRO、TUT、S1……）")]
            public string section;
            [Tooltip("触发条件（策划原文，仅供查看）")]
            public string trigger;
            public DialogueSequence sequence;
        }

        public List<Entry> entries = new List<Entry>();

        // 按段 key 取；也接受段内任意一句的 ID
        public DialogueSequence Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in entries)
                if (e.key == id) return e.sequence;
            foreach (var e in entries)
                if (e.sequence != null && e.sequence.lines.Exists(l => l.id == id)) return e.sequence;
            Debug.LogWarning($"[Dialogue] 台词库里没有 {id}", this);
            return null;
        }

        // 某个段落的全部对白段，按表格顺序
        public List<DialogueSequence> GetSection(string section)
        {
            var list = new List<DialogueSequence>();
            foreach (var e in entries)
                if (e.section == section && e.sequence != null) list.Add(e.sequence);
            return list;
        }
    }
}
