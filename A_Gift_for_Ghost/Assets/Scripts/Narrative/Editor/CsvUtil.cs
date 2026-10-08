using System.Collections.Generic;
using System.Text;

namespace Ghost.Narrative.EditorTools
{
    // 策划表格（docs/script/*.csv）共用的解析
    public static class CsvUtil
    {
        // 简单 CSV 解析：支持 BOM、引号包裹、引号内逗号和换行、"" 转义
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else if (c == '\r') { } // 引号内换行统一成 \n
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        // 按表头名找列，找不到返回 -1
        public static int Column(List<string> header, string name)
        {
            for (int i = 0; i < header.Count; i++)
                if (header[i].Trim() == name) return i;
            return -1;
        }

        public static string Cell(List<string> row, int index) =>
            index >= 0 && index < row.Count ? row[index].Trim() : "";
    }
}
