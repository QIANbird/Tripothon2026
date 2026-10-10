using System;
using UnityEngine;

namespace Ghost.Narrative
{
    // 说话人。字幕颜色和显示名由 SubtitlePanel 按说话人决定
    public enum Speaker
    {
        EmotionalFemale,   // 有情感的女声
        MechanicalFemale,  // 机械女声（系统播报）
        Protagonist,       // 主角
        Agent,             // Agent（如 AI 询问的旁白）
    }

    // 显示通道。Auto = 按说话人决定（SubtitlePanel.styles）；其他值强制走指定通道。
    // AgentPopup = 聊天气泡（Agent弹窗_02）；AgentCaution = Caution 标签卡（Agent弹窗_01）。
    // 枚举值只追加在末尾，已有资产的序列化值不变。
    public enum LineChannel
    {
        Auto,
        Subtitle,
        AgentPopup,
        AgentCaution,
    }

    // 一句台词。策划在 DialogueSequence 资产的 Inspector 里填，或从台词表 CSV 导入
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("台词表里的 ID（例如 S3_004）；手填的台词可以留空")]
        public string id = "";
        public Speaker speaker = Speaker.EmotionalFemale;
        [Tooltip("Auto = 按说话人决定显示在字幕还是 Agent 弹窗")]
        public LineChannel channel = LineChannel.Auto;
        [TextArea(2, 5)]
        public string text = "";
        [Tooltip("配音。没有时按字数估算时长")]
        public AudioClip clip;
        [Tooltip("大于 0 时直接用这个时长（秒），忽略音频长度和字数估算")]
        public float durationOverride;
        [Tooltip("这句播完后再停顿多久（秒）。小于 0 时用 DialoguePlayer 的默认间隔")]
        public float pauseAfter = -1f;

        public DialogueLine() { }

        public DialogueLine(Speaker speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }
}
