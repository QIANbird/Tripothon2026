using Ghost.Narrative;
using Ghost.Narrative.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Ghost.Stages.EditorTools
{
    // 各阶段对白的来源：台词表 docs/script/02_dialogue.csv 导入的 Script/<段key>.asset，见 LoadScript 和下面的 key。
    // 文件后半段的 Ensure* 是旧的 S2–S4 占位对白，已不再使用。
    public static class StageAssets
    {
        // 台词表各段的 key（= 这一段第一句的 ID），和 docs/script/02_dialogue.csv 对应。
        // "Agent询问" / "操作提示"行单独成段，阶段脚本只取它的文本（见 DialogueCsvImporter）
        public const string IntroKey = "INTRO_001";
        public const string IntroAfterClickKey = "INTRO_009";

        public const string TutorialIntroKey = "TUT_002";
        public const string TutorialAfterEasyKey = "TUT_004";
        public const string TutorialAfterHardKey = "TUT_008";
        public const string TutorialInspectKey = "TUT_014";
        public const string TutorialAfterInspectKey = "TUT_015";

        public const string S1IntroKey = "S1_001";
        public const string S1ClickPoolPrefix = "S1_P";
        public const string S1CompleteKey = "S1_002";
        public const string S1QueryKey = "S1_005";

        public const string S2IntroKey = "S2_001";
        public const string S2WrongStartKey = "S2_W01";
        public const string S2SolvedKey = "S2_006";
        public const string S2FinishKey = "S2_008";

        public const string S3IntroKey = "S3_001";
        public const string S3FirstMarkedKey = "S3_006";
        public const string S3AllFoundKey = "S3_007";
        public const string S3QueryKey = "S3_011";

        public const string S4IntroKey = "S4_001";
        public const string S4ProgressKey = "S4_005";
        public const string S4AllRemovedKey = "S4_006";
        public const string TransitionKey = "S4_008";

        public const string PickIntroKey = "PICK_001";
        public const string PickControlsHintKey = "PICK_003";
        public const string PickHintAfterLineId = "PICK_002"; // 这句播完后显示操作提示
        public const string PickFirstBiteKey = "PICK_006";

        // 读台词表导入的一段对白；找不到时报警告，阶段里这段会直接跳过
        public static DialogueSequence LoadScript(string key)
        {
            string path = $"{DialogueCsvImporter.OutFolder}/{key}.asset";
            var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (seq == null)
                Debug.LogWarning($"[Stages] 找不到台词表对白 {path}，先用菜单 Ghost/Narrative/Import Dialogue CSV 导入");
            return seq;
        }

        // key 以 prefix 开头的所有段（例如 S1 点方块的随机池 S1_P01、S1_P02……），按 key 排序
        public static DialogueSequence[] LoadScriptsWithPrefix(string prefix)
        {
            var list = new System.Collections.Generic.List<DialogueSequence>();
            if (AssetDatabase.IsValidFolder(DialogueCsvImporter.OutFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:DialogueSequence", new[] { DialogueCsvImporter.OutFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(prefix)) continue;
                    var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
                    if (seq != null) list.Add(seq);
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (list.Count == 0) Debug.LogWarning($"[Stages] 台词表里没有 {prefix}* 开头的段");
            return list.ToArray();
        }

        // 以下 S2–S4 的旧占位对白已不再挂到场景（改用台词表），方法暂时保留
        // G7：S2 连线修复
        public const string S2IntroPath = NarrativeAssets.Folder + "/S2Intro.asset";
        public const string S2WrongStartPath = NarrativeAssets.Folder + "/S2WrongStart.asset";
        public const string S2WaterSolvedPath = NarrativeAssets.Folder + "/S2WaterSolved.asset";

        public static DialogueSequence EnsureS2Intro() => Ensure(S2IntroPath, 0.8f,
            new DialogueLine(Speaker.Agent, "[占位] 检测到生长资源不足。供给需要从支撑系统开始分配。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 从亮着的根部按住，沿着连线拖到闪烁的地方。"));

        public static DialogueSequence EnsureS2WrongStart() => Ensure(S2WrongStartPath, 0f,
            new DialogueLine(Speaker.Agent, "[占位] 需要从支撑系统开始。"));

        public static DialogueSequence EnsureS2WaterSolved() => Ensure(S2WaterSolvedPath, 0.3f,
            new DialogueLine(Speaker.Agent, "[占位] 供给已恢复。核心产出仍然异常，原因未知。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 在还在闪的果实上按住右键，看看它的详情。"));

        // G8：S3 找虫
        public const string S3IntroPath = NarrativeAssets.Folder + "/S3Intro.asset";
        public const string S3AllFoundPath = NarrativeAssets.Folder + "/S3AllFound.asset";

        public static DialogueSequence EnsureS3Intro() => Ensure(S3IntroPath, 0.8f,
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 颜色回来了。仔细看，有些东西不属于这株植物。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 在空白处按住拖动，可以转动它。叶子背面也看看。"),
            new DialogueLine(Speaker.Agent, "[占位] 点击可疑节点进行标记。按住右键可以查看节点详情。"));

        public static DialogueSequence EnsureS3AllFound() => Ensure(S3AllFoundPath, 0.3f,
            new DialogueLine(Speaker.Agent, "[占位] 已标记全部异常个体。"));

        // G9：S4 除虫
        public const string S4IntroPath = NarrativeAssets.Folder + "/S4Intro.asset";
        public const string S4FirstRemovedPath = NarrativeAssets.Folder + "/S4FirstRemoved.asset";

        public static DialogueSequence EnsureS4Intro() => Ensure(S4IntroPath, 0.8f,
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 这一次，手是你的了。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 转动盆栽，看看叶子底下藏着什么。找到了就把它摘掉。"));

        public static DialogueSequence EnsureS4FirstRemoved() => Ensure(S4FirstRemovedPath, 0.2f,
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 看，颜色更清楚了。继续。"));

        // 右键查看详情改版后，旧资产里的"点击查看"台词改成右键。Ensure* 不覆盖已有资产，所以按原文逐句替换；
        // 策划已经改过的句子（原文对不上）不动。返回替换的句数
        [MenuItem("Ghost/Stages/Update Dialogue For Right-Click Inspect")]
        public static int MigrateRightClickInspect()
        {
            var replacements = new (string path, string from, string to)[]
            {
                (S2WaterSolvedPath, "[占位] 点一下那些还在闪的果实看看。", "[占位] 在还在闪的果实上按住右键，看看它的详情。"),
                (S3IntroPath, "[占位] 点击可疑节点进行标记。", "[占位] 点击可疑节点进行标记。按住右键可以查看节点详情。"),
            };
            int n = 0;
            foreach (var (path, from, to) in replacements)
            {
                var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
                if (seq == null) continue;
                foreach (var line in seq.lines)
                {
                    if (line.text != from) continue;
                    line.text = to;
                    n++;
                    EditorUtility.SetDirty(seq);
                }
            }
            if (n > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[Stages] 右键查看：更新了 {n} 句台词");
            return n;
        }

        static DialogueSequence Ensure(string path, float startDelay, params DialogueLine[] lines)
        {
            var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (seq != null) return seq;
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(NarrativeAssets.Folder)) AssetDatabase.CreateFolder("Assets/Data", "Narrative");
            seq = ScriptableObject.CreateInstance<DialogueSequence>();
            seq.startDelay = startDelay;
            seq.lines.AddRange(lines);
            AssetDatabase.CreateAsset(seq, path);
            return seq;
        }
    }
}
