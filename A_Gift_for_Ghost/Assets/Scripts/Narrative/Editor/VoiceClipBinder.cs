using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ghost.Narrative.EditorTools
{
    // 按文件名把配音挂到台词上：VoiceFolder 下（含子目录）名为 <台词ID> 或 VO_<台词ID> 的音频，
    // 自动填进 Script/ 里同 ID 台词的 clip。文件名对照 docs/script/05_audio_list.xlsx 的"配音"页。
    // 往 VoiceFolder 拖入 / 替换音频后自动执行；也可以用菜单手动执行。
    // 只覆盖找到同名音频的台词，没有音频的台词保持原样（不清空手挂的 clip）。
    public static class VoiceClipBinder
    {
        public const string VoiceFolder = "Assets/Audio/VO";
        const string Prefix = "VO_";

        [MenuItem("Ghost/Narrative/Bind Voice Clips")]
        public static void BindMenu() => Bind(true);

        public static void Bind(bool log)
        {
            if (!AssetDatabase.IsValidFolder(VoiceFolder))
            {
                if (log) Debug.LogWarning($"[Voice] 没有找到 {VoiceFolder}，先把配音放进去");
                return;
            }

            // 收集 ID → 音频
            var clips = new Dictionary<string, AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(p);
                string id = name.StartsWith(Prefix) ? name.Substring(Prefix.Length) : name;
                if (clips.ContainsKey(id)) Debug.LogWarning($"[Voice] {id} 有多个音频，使用 {p}");
                clips[id] = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }

            int bound = 0;
            var used = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueSequence", new[] { DialogueCsvImporter.OutFolder }))
            {
                var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(AssetDatabase.GUIDToAssetPath(guid));
                if (seq == null) continue;
                bool dirty = false;
                foreach (var line in seq.lines)
                {
                    if (line == null || string.IsNullOrEmpty(line.id)) continue;
                    if (!clips.TryGetValue(line.id, out var clip)) continue;
                    used.Add(line.id);
                    if (line.clip == clip) continue;
                    line.clip = clip;
                    dirty = true;
                    bound++;
                }
                if (dirty) EditorUtility.SetDirty(seq);
            }
            AssetDatabase.SaveAssets();

            if (!log && bound == 0) return;
            var unused = new List<string>();
            foreach (var id in clips.Keys) if (!used.Contains(id)) unused.Add(id);
            Debug.Log($"[Voice] 新挂上 {bound} 句配音，共找到 {clips.Count} 个音频" +
                      (unused.Count > 0 ? $"；没有对应台词的：{string.Join(", ", unused)}" : ""));
        }

        // 配音导入设置：单声道、强制 Mono、Vorbis 压缩、加载时解压。2D 播放不需要立体声，也省 Quest 内存
        class Postprocessor : AssetPostprocessor
        {
            static bool InVoiceFolder(string path) =>
                path.Replace('\\', '/').StartsWith(VoiceFolder + "/");

            void OnPreprocessAudio()
            {
                if (!InVoiceFolder(assetPath)) return;
                var importer = (AudioImporter)assetImporter;
                importer.forceToMono = true;
                var s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                importer.defaultSampleSettings = s;
            }

            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var p in imported)
                {
                    if (!InVoiceFolder(p)) continue;
                    EditorApplication.delayCall += () => Bind(false);
                    return;
                }
                foreach (var p in moved)
                {
                    if (!InVoiceFolder(p)) continue;
                    EditorApplication.delayCall += () => Bind(false);
                    return;
                }
            }
        }
    }
}
