using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ghost.Narrative.EditorTools
{
    // 把策划的台词表 docs/script/02_dialogue.csv 导入成 DialogueSequence 资产 + 一个 DialogueLibrary 索引。
    //
    // 分段规则：触发条件是"上一句结束"或"<上一句ID> 之后 / 结束"的台词，接在上一句后面成为同一段；
    // 其他触发条件开始新的一段，段的 key 就是这一段第一句的 ID。文本为空的行（视频、演出）跳过，不打断分段。
    // 通道：字幕 → Subtitle；Agent弹窗 / Agent弹窗_02 → AgentPopup（聊天气泡）；
    // Agent弹窗_01 → AgentCaution（Caution 标签卡）；其他（待确认、空）→ Auto，按说话人决定。
    // 通道为"Agent询问"、以 "_query" 结尾（带 Yes 的弹窗）或"操作提示"（左上角提示文字）的行
    // 不由 DialoguePlayer 播放：每行单独成一段（key = 自己的 ID），阶段脚本读它的文本自己显示；
    // 这一行不打断前后台词的分段。
    //
    // 生成到 Assets/Data/Narrative/Script/，每段一个资产（文件名 = key）。重新导入时原地更新（GUID 不变），
    // 同一 ID 的台词保留已挂的配音和时长设置；表里删掉的段，对应资产一起删除。不改其他目录里手做的对白资产。
    // 编辑器加载（包括脚本重新编译）时，CSV 比上次导入新就自动导入；也可以用菜单手动导入。
    [InitializeOnLoad]
    public static class DialogueCsvImporter
    {
        const string CsvPath = "../docs/script/02_dialogue.csv"; // 相对 Unity 工程根目录
        public const string OutFolder = NarrativeAssets.Folder + "/Script";
        public const string LibraryPath = NarrativeAssets.Folder + "/DialogueLibrary.asset";

        static DialogueCsvImporter()
        {
            EditorApplication.delayCall += ImportIfChanged;
        }

        // 导入规则变了（例如新增通道）就把版本号加一，下次编辑器加载时强制重新导入一次
        const int ImporterVersion = 2;
        static string PrefKey => "Ghost.DialogueCsv.LastImport.v" + ImporterVersion + "." + Application.dataPath;
        static string FullPath() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvPath));

        static void ImportIfChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path = FullPath();
            if (!File.Exists(path)) return;
            string stamp = File.GetLastWriteTimeUtc(path).Ticks.ToString();
            if (EditorPrefs.GetString(PrefKey, "") == stamp) return;
            if (Import()) EditorPrefs.SetString(PrefKey, stamp);
        }

        [MenuItem("Ghost/Narrative/Import Dialogue CSV")]
        static void ImportMenu() => Import();

        class Group
        {
            public string key, section, trigger;
            public readonly List<DialogueLine> lines = new List<DialogueLine>();
        }

        // 成功返回 true
        public static bool Import()
        {
            string path = FullPath();
            if (!File.Exists(path))
            {
                Debug.LogError($"[Dialogue] 找不到台词表：{path}");
                return false;
            }
            var rows = CsvUtil.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (rows.Count < 2)
            {
                Debug.LogError($"[Dialogue] 台词表是空的：{path}");
                return false;
            }
            var header = rows[0];
            int iId = CsvUtil.Column(header, "ID");
            int iSection = CsvUtil.Column(header, "段落");
            int iTrigger = CsvUtil.Column(header, "触发条件");
            int iChannel = CsvUtil.Column(header, "通道");
            int iSpeaker = CsvUtil.Column(header, "说话人");
            int iText = CsvUtil.Column(header, "文本");
            if (iId < 0 || iTrigger < 0 || iSpeaker < 0 || iText < 0)
            {
                Debug.LogError("[Dialogue] 台词表缺少必需的列（ID / 触发条件 / 说话人 / 文本）");
                return false;
            }

            // ---- 分段 ----
            var groups = new List<Group>();
            Group current = null;
            string prevId = null;
            int lineCount = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = CsvUtil.Cell(row, iId);
                string text = CsvUtil.Cell(row, iText);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(text)) continue; // 空行、视频、演出

                string trigger = CsvUtil.Cell(row, iTrigger);
                string channelCell = CsvUtil.Cell(row, iChannel);
                if (IsCue(channelCell))
                {
                    var cue = new Group { key = id, section = CsvUtil.Cell(row, iSection), trigger = trigger };
                    TryParseSpeaker(CsvUtil.Cell(row, iSpeaker), out Speaker cueSpeaker); // "—" 等无法识别时按默认处理，不报警告
                    cue.lines.Add(new DialogueLine(cueSpeaker, text) { id = id, channel = LineChannel.AgentCaution });
                    groups.Add(cue);
                    lineCount++;
                    continue; // 不更新 current / prevId：下一句"上一句结束"仍接在前一段后面
                }
                if (current == null || !Chains(trigger, prevId))
                {
                    current = new Group { key = id, section = CsvUtil.Cell(row, iSection), trigger = trigger };
                    groups.Add(current);
                }
                if (!TryParseSpeaker(CsvUtil.Cell(row, iSpeaker), out Speaker speaker))
                    Debug.LogWarning($"[Dialogue] {id} 的说话人「{CsvUtil.Cell(row, iSpeaker)}」无法识别，按亲切的声音处理");
                current.lines.Add(new DialogueLine(speaker, text)
                {
                    id = id,
                    channel = ParseChannel(channelCell),
                });
                prevId = id;
                lineCount++;
            }

            // ---- 写资产 ----
            EnsureFolder();
            var keep = new HashSet<string>();
            var library = AssetDatabase.LoadAssetAtPath<DialogueLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<DialogueLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.entries.Clear();

            foreach (var g in groups)
            {
                string assetPath = $"{OutFolder}/{g.key}.asset";
                keep.Add(assetPath);
                var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(assetPath);
                if (seq == null)
                {
                    seq = ScriptableObject.CreateInstance<DialogueSequence>();
                    AssetDatabase.CreateAsset(seq, assetPath);
                }
                // 保留同一 ID 台词上已挂的配音、时长和停顿
                var old = new Dictionary<string, DialogueLine>();
                foreach (var l in seq.lines)
                    if (l != null && !string.IsNullOrEmpty(l.id)) old[l.id] = l;
                foreach (var l in g.lines)
                {
                    if (!old.TryGetValue(l.id, out var o)) continue;
                    l.clip = o.clip;
                    l.durationOverride = o.durationOverride;
                    l.pauseAfter = o.pauseAfter;
                }
                seq.lines = g.lines;
                EditorUtility.SetDirty(seq);
                library.entries.Add(new DialogueLibrary.Entry { key = g.key, section = g.section, trigger = g.trigger, sequence = seq });
            }

            // 表里已经删掉的段
            int removed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueSequence", new[] { OutFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (keep.Contains(p)) continue;
                AssetDatabase.DeleteAsset(p);
                removed++;
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dialogue] 已从 {path} 导入 {lineCount} 句台词，共 {groups.Count} 段 → {OutFolder}" +
                      (removed > 0 ? $"（删除 {removed} 个已不在表里的段）" : ""), library);
            return true;
        }

        // "上一句结束"，或者明确写了上一句的 ID："TUT_002 之后" / "TUT_003 结束"
        static bool Chains(string trigger, string prevId)
        {
            if (string.IsNullOrEmpty(prevId)) return false;
            if (trigger == "上一句结束") return true;
            var m = Regex.Match(trigger, @"^([A-Za-z0-9_]+)\s*(之后|结束)$");
            return m.Success && m.Groups[1].Value == prevId;
        }

        static bool TryParseSpeaker(string cell, out Speaker speaker)
        {
            switch (cell)
            {
                case "亲切的声音":
                case "温柔女声":
                case "有情感的女声": speaker = Speaker.EmotionalFemale; return true;
                case "没有温度的声音":
                case "Agent": speaker = Speaker.Agent; return true;
                case "机械女声": speaker = Speaker.MechanicalFemale; return true;
                case "主角": speaker = Speaker.Protagonist; return true;
                default: speaker = Speaker.EmotionalFemale; return false;
            }
        }

        // 不进对白段、由阶段脚本单独显示的行
        static bool IsCue(string channelCell)
        {
            if (string.IsNullOrEmpty(channelCell)) return false;
            if (channelCell == "Agent询问" || channelCell == "Agent 询问" || channelCell == "操作提示")
                return true;
            return channelCell.EndsWith("_query");
        }

        static LineChannel ParseChannel(string cell)
        {
            switch (cell)
            {
                case "字幕": return LineChannel.Subtitle;
                case "Agent弹窗_01": return LineChannel.AgentCaution;
                case "Agent弹窗_02":
                case "Agent弹窗":
                case "Agent 弹窗": return LineChannel.AgentPopup;
                default: return LineChannel.Auto; // 待确认 / 空
            }
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(NarrativeAssets.Folder)) AssetDatabase.CreateFolder("Assets/Data", "Narrative");
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(NarrativeAssets.Folder, "Script");
        }
    }
}
