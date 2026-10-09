using Ghost.Narrative;
using Ghost.Narrative.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Ghost.Stages.EditorTools
{
    // S2–S4 的对白资产（全部是【占位台词】，以 [占位] 开头，等策划替换）。
    // 资产已存在时直接返回，不覆盖策划的修改；想恢复默认就删掉资产再生成主场景。
    // 教学的对白已改用台词表 TUT 段（DialogueCsvImporter 生成的 Script/<段key>.asset），见 LoadScript。
    public static class StageAssets
    {
        // 台词表 TUT 段各段的 key（= 这一段第一句的 ID），和 docs/script/02_dialogue.csv 对应
        public const string TutorialIntroKey = "TUT_002";
        public const string TutorialAfterEasyKey = "TUT_004";
        public const string TutorialAfterHardKey = "TUT_006";
        public const string TutorialInspectKey = "TUT_009";
        public const string TutorialAfterInspectKey = "TUT_010";

        // 读台词表导入的一段对白；找不到时报警告，阶段里这段会直接跳过
        public static DialogueSequence LoadScript(string key)
        {
            string path = $"{DialogueCsvImporter.OutFolder}/{key}.asset";
            var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (seq == null)
                Debug.LogWarning($"[Stages] 找不到台词表对白 {path}，先用菜单 Ghost/Narrative/Import Dialogue CSV 导入");
            return seq;
        }

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
