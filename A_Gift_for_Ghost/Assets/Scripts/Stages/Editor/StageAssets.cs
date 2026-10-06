using Ghost.Narrative;
using Ghost.Narrative.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Ghost.Stages.EditorTools
{
    // 教学和 S2 的对白资产（全部是【占位台词】，以 [占位] 开头，等策划替换）。
    // 资产已存在时直接返回，不覆盖策划的修改；想恢复默认就删掉资产再生成主场景。
    public static class StageAssets
    {
        public const string TutorialIntroPath = NarrativeAssets.Folder + "/TutorialIntro.asset";
        public const string TutorialEasyPath = NarrativeAssets.Folder + "/TutorialAfterEasy.asset";
        public const string TutorialMediumPath = NarrativeAssets.Folder + "/TutorialAfterMedium.asset";
        public const string TutorialHardPath = NarrativeAssets.Folder + "/TutorialAfterHard.asset";

        public static DialogueSequence EnsureTutorialIntro() => Ensure(TutorialIntroPath, 0.8f,
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 看到那个在闪的方块了吗？它代表一处异常。"),
            new DialogueLine(Speaker.Agent, "[占位] 点击异常节点，即授权我处理一次。"));

        public static DialogueSequence EnsureTutorialAfterEasy() => Ensure(TutorialEasyPath, 0.3f,
            new DialogueLine(Speaker.Agent, "[占位] 已处理。简单的问题，一次授权就够了。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 有些问题要多试几次。再看这一个。"));

        public static DialogueSequence EnsureTutorialAfterMedium() => Ensure(TutorialMediumPath, 0.3f,
            new DialogueLine(Speaker.Agent, "[占位] 已处理。共授权三次。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 最后一个……试试看。"));

        public static DialogueSequence EnsureTutorialAfterHard() => Ensure(TutorialHardPath, 0.2f,
            new DialogueLine(Speaker.Agent, "[占位] 无法解决。当前权限下没有可执行的方案，稍后重试。"),
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 有些问题，它解决不了。注意它还在闪。"));

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
            new DialogueLine(Speaker.EmotionalFemale, "[占位] 点一下那些还在闪的果实看看。"));

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
