using Ghost.Morph;
using UnityEditor;
using UnityEngine;

namespace Ghost.Narrative.EditorTools
{
    // 生成剧情相关的默认资产。资产已存在时直接返回，不会覆盖策划的修改；
    // 想恢复默认内容，删掉资产后重新执行菜单。
    public static class NarrativeAssets
    {
        public const string Folder = "Assets/Data/Narrative";
        public const string IntroPath = Folder + "/Intro.asset";
        public const string NodeDetailsPath = Folder + "/NodeDetails.asset";

        [MenuItem("Ghost/Narrative/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureIntroSequence();
            EnsureNodeDetails();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dialogue] 默认资产已就绪：{IntroPath}、{NodeDetailsPath}");
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Narrative");
        }

        // 开场三句对白（PROJECT_SUMMARY 第 4 节"开场剧情"），暂无配音，按字数估算时长
        public static DialogueSequence EnsureIntroSequence()
        {
            var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(IntroPath);
            if (seq != null) return seq;
            EnsureFolder();
            seq = ScriptableObject.CreateInstance<DialogueSequence>();
            seq.startDelay = 1.5f; // 先黑一会儿，再开口
            seq.lines.Add(new DialogueLine(Speaker.EmotionalFemale, "嗨，你醒了。"));
            seq.lines.Add(new DialogueLine(Speaker.MechanicalFemale, "神经连接已恢复。检测到稳定的意识活动。语言和运动通道尚未恢复。"));
            seq.lines.Add(new DialogueLine(Speaker.EmotionalFemale,
                "你的感官系统与运动神经都沉寂了太久了。我现在是通过脑机接口与你对话。别担心，我们会通过一些虚拟世界的练习帮助你恢复行为能力。先从简单的开始，注意观察你行动的反馈。"));
            AssetDatabase.CreateAsset(seq, IntroPath);
            return seq;
        }

        // 节点详情表。全部是【占位文字】（以 [占位] 开头），等策划替换
        public static NodeDetailTable EnsureNodeDetails()
        {
            var table = AssetDatabase.LoadAssetAtPath<NodeDetailTable>(NodeDetailsPath);
            if (table != null) return table;
            EnsureFolder();
            table = ScriptableObject.CreateInstance<NodeDetailTable>();
            // S1 统一模板：只有状态、进度、置信度，数值由阶段脚本填
            const string s1 = "[占位] 任务 #{id}　{status}\n进度 {progress}%　置信度 {confidence}%";
            void Add(Organ organ, string project, string physical) =>
                table.entries.Add(new NodeDetailTable.Entry { organ = organ, status = s1, project = project, physical = physical });

            Add(Organ.Soil,
                "[占位] 基础供给层：资源储备低于阈值，输入流量持续下降。",
                "[占位] 深褐色的土，表面干裂成块，摸上去粗糙、发硬，几乎没有湿气。");
            Add(Organ.Root,
                "[占位] 支撑系统：负载正常，吸收效率偏低。",
                "[占位] 浅褐色的细根，一缕缕扎进土里，带着细小的须毛，微微有点韧。");
            Add(Organ.Stem,
                "[占位] 主干通道：传输链路稳定，局部带宽不足。",
                "[占位] 绿色的茎，圆柱形，表面有一层细绒毛，掐一下能闻到青涩的味道。");
            Add(Organ.Leaf,
                "[占位] 边缘系统：能量转换模块效率下降，部分单元离线。",
                "[占位] 深绿色的叶片，椭圆形，叶缘有些卷曲发软；叶背颜色更浅，摸上去有细细的叶脉。");
            Add(Organ.Bud,
                "[占位] 待启动模块：处于预备状态，等待资源分配。",
                "[占位] 小小的花苞，淡绿中透着一点白，紧紧裹着，像握起来的拳头。");
            Add(Organ.Fruit,
                "[占位] 核心产出：交付物质量指标异常，原因未知。",
                "[占位] 彩椒，红得发亮，表面光滑饱满，捏起来有弹性；可是靠近果蒂的地方有几个细小的咬痕。");
            Add(Organ.Bug,
                "[占位] 未识别进程：占用少量资源，来源不明。",
                "[占位] 一只绿色的小虫，比米粒大一点，藏在叶子背面，身体软软的，正在慢慢挪动。");

            AssetDatabase.CreateAsset(table, NodeDetailsPath);
            return table;
        }
    }
}
