using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ghost.Morph;
using UnityEditor;
using UnityEngine;

namespace Ghost.Narrative.EditorTools
{
    // 把策划的节点文本表 docs/script/03_node_text.csv 导入 NodeDetails.asset。
    // 按列名取值（列顺序可以变）；第一列"部位"写成"中文名 英文枚举名"，例如"根部 Root"，中文名就是 S4 的标题。
    // 只覆盖 CSV 里有内容的格子；"（说明）"行和"待确认"列跳过；S1 status 模板不动。
    // 编辑器加载（包括脚本重新编译）时，CSV 比上次导入新就自动导入；也可以用菜单手动导入。
    [InitializeOnLoad]
    public static class NodeTextCsvImporter
    {
        static NodeTextCsvImporter()
        {
            EditorApplication.delayCall += ImportIfChanged;
        }

        static string PrefKey => "Ghost.NodeTextCsv.LastImport." + Application.dataPath;

        static void ImportIfChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = FullPath();
            if (!File.Exists(path)) return;
            string stamp = File.GetLastWriteTimeUtc(path).Ticks.ToString();
            if (EditorPrefs.GetString(PrefKey, "") == stamp) return;
            if (Import()) EditorPrefs.SetString(PrefKey, stamp);
        }

        static string FullPath() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvPath));

        // 相对 Unity 工程根目录
        const string CsvPath = "../docs/script/03_node_text.csv";

        const string ColOrgan = "部位";
        const string ColProjectName = "S2 名称";
        const string ColProject = "S2 缺水（待补充资源）";
        const string ColPhysicalName = "S3名称";
        const string ColPhysical = "S3+ physical";

        [MenuItem("Ghost/Narrative/Import Node Text CSV")]
        static void ImportMenu() => Import();

        // 成功返回 true
        public static bool Import()
        {
            string path = FullPath();
            if (!File.Exists(path))
            {
                Debug.LogError($"[Dialogue] 找不到节点文本表：{path}");
                return false;
            }
            var rows = CsvUtil.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (rows.Count < 2)
            {
                Debug.LogError($"[Dialogue] 节点文本表是空的：{path}");
                return false;
            }

            var header = rows[0];
            int iOrgan = Find(header, ColOrgan);
            if (iOrgan < 0)
            {
                Debug.LogError($"[Dialogue] 节点文本表缺少列「{ColOrgan}」");
                return false;
            }
            int iProjectName = Find(header, ColProjectName);
            int iProject = Find(header, ColProject);
            int iPhysicalName = Find(header, ColPhysicalName);
            int iPhysical = Find(header, ColPhysical);

            var table = NarrativeAssets.EnsureNodeDetails();
            Undo.RecordObject(table, "Import Node Text CSV");
            int count = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string organCell = Cell(row, iOrgan);
                if (!TryParseOrgan(organCell, out Organ organ, out string realName)) continue; // 说明行、空行

                var entry = table.entries.Find(e => e.organ == organ);
                if (entry == null)
                {
                    entry = new NodeDetailTable.Entry { organ = organ };
                    table.entries.Add(entry);
                }
                Set(ref entry.realName, realName);
                Set(ref entry.projectName, Cell(row, iProjectName));
                Set(ref entry.project, Cell(row, iProject));
                Set(ref entry.physicalName, Cell(row, iPhysicalName));
                Set(ref entry.physical, Cell(row, iPhysical));
                count++;
            }
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dialogue] 已从 {path} 导入 {count} 个部位到 {NarrativeAssets.NodeDetailsPath}", table);
            return true;
        }

        static void Set(ref string field, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) field = value.Trim();
        }

        // "根部 Root" → Organ.Root，中文名"根部"
        static bool TryParseOrgan(string cell, out Organ organ, out string chineseName)
        {
            organ = default;
            chineseName = null;
            if (string.IsNullOrWhiteSpace(cell)) return false;
            var parts = cell.Trim().Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !Enum.TryParse(parts[parts.Length - 1], true, out organ)) return false;
            chineseName = string.Join("", parts, 0, parts.Length - 1);
            return true;
        }

        static int Find(List<string> header, string name)
        {
            for (int i = 0; i < header.Count; i++)
                if (header[i].Trim() == name) return i;
            Debug.LogWarning($"[Dialogue] 节点文本表缺少列「{name}」，这一列跳过");
            return -1;
        }

        static string Cell(List<string> row, int index) => index >= 0 && index < row.Count ? row[index] : null;
    }
}
