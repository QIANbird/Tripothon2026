using System.Text.RegularExpressions;
using UnityEngine;

namespace Ghost.Narrative
{
    // 台词表文本的小工具
    public static class ScriptText
    {
        static readonly Regex RandomToken = new Regex(@"\{(\d+)-(\d+)\}");

        // 把 {a-b} 换成 a–b 之间的随机整数（含两端）。a 写成两位以上且以 0 开头时（例如 {01-99}）按 a 的位数补零
        public static string FillRandom(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return RandomToken.Replace(text, m =>
            {
                int a = int.Parse(m.Groups[1].Value);
                int b = int.Parse(m.Groups[2].Value);
                if (b < a) (a, b) = (b, a);
                int v = Random.Range(a, b + 1);
                string first = m.Groups[1].Value;
                return first.Length > 1 && first[0] == '0' ? v.ToString().PadLeft(first.Length, '0') : v.ToString();
            });
        }

        // 一段的第一句文本（"Agent询问"、"操作提示"这类单行段用）；没有时返回 fallback
        public static string FirstLine(DialogueSequence sequence, string fallback = "")
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Count == 0 || sequence.lines[0] == null) return fallback;
            return string.IsNullOrEmpty(sequence.lines[0].text) ? fallback : sequence.lines[0].text;
        }
    }
}
