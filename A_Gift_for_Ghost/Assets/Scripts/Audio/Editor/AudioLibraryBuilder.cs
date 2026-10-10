using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ghost.Audio.EditorTools
{
    // 扫描 Assets/Audio/{SFX,BGM,AMB} 生成 AudioLibrary：文件名 = 编号（05_audio_list.xlsx），
    // 末尾的 _01 / _02 … 是同一编号的变体。往这些文件夹放入 / 替换 / 删除音频后自动重建，也可以用菜单手动执行。
    // 导入设置：BGM / AMB 用 Streaming（边播边读，不占内存，大文件也不卡加载），SFX 加载时解压。
    public static class AudioLibraryBuilder
    {
        public const string Root = "Assets/Audio";
        public static readonly string[] Folders = { Root + "/SFX", Root + "/BGM", Root + "/AMB" };
        public const string LibraryFolder = "Assets/Data/Audio";
        public const string LibraryPath = LibraryFolder + "/AudioLibrary.asset";

        static readonly Regex Variant = new Regex(@"^(.+?)_(\d+)$");

        // 新编号第一次进库时的单条音量（在 GameAudio.sfxVolume 之上再乘）。已经在库里的编号保留 Inspector 里调过的值。
        // 10-11 试听：这几个偏响，压低一半
        static readonly Dictionary<string, float> DefaultVolume = new Dictionary<string, float>
        {
            { "SFX_S2_EXPAND", 0.5f },
            { "SFX_S3_COLOR", 0.5f },
            { "SFX_AGENT_SHUTDOWN", 0.5f },
        };
        static bool pending;

        [MenuItem("Ghost/Audio/Rebuild Audio Library")]
        static void RebuildMenu() => Rebuild(true);

        public static AudioLibrary EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library != null) return library;
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(LibraryFolder)) AssetDatabase.CreateFolder("Assets/Data", "Audio");
            library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            return library;
        }

        public static void Rebuild(bool log)
        {
            var library = EnsureLibrary();
            var oldVolume = new Dictionary<string, float>();
            foreach (var e in library.entries)
                if (e != null && !string.IsNullOrEmpty(e.id)) oldVolume[e.id] = e.volume;

            // 编号 → 变体（按文件名排序）
            var groups = new SortedDictionary<string, SortedDictionary<string, AudioClip>>();
            var valid = new List<string>();
            foreach (var f in Folders) if (AssetDatabase.IsValidFolder(f)) valid.Add(f);
            if (valid.Count > 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", valid.ToArray()))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileNameWithoutExtension(path);
                    var m = Variant.Match(name);
                    string id = m.Success ? m.Groups[1].Value : name;
                    if (!groups.TryGetValue(id, out var clips)) groups[id] = clips = new SortedDictionary<string, AudioClip>();
                    clips[name] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }

            library.entries.Clear();
            foreach (var g in groups)
            {
                var entry = new AudioLibrary.Entry { id = g.Key, clips = new List<AudioClip>(g.Value.Values).ToArray() };
                if (oldVolume.TryGetValue(g.Key, out var v)) entry.volume = v;
                else if (DefaultVolume.TryGetValue(g.Key, out var d)) entry.volume = d;
                library.entries.Add(entry);
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log($"[Audio] 音频库已更新：{library.entries.Count} 个编号 → {LibraryPath}", library);
        }

        static bool InFolder(string path, string folder) =>
            path.Replace('\\', '/').StartsWith(folder + "/");

        static bool InAnyFolder(string path)
        {
            foreach (var f in Folders) if (InFolder(path, f)) return true;
            return false;
        }

        class Postprocessor : AssetPostprocessor
        {
            void OnPreprocessAudio()
            {
                if (!InAnyFolder(assetPath)) return;
                var importer = (AudioImporter)assetImporter;
                var s = importer.defaultSampleSettings;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                if (InFolder(assetPath, Root + "/SFX"))
                {
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.quality = 0.7f;
                }
                else
                {
                    s.loadType = AudioClipLoadType.Streaming;
                    s.quality = 0.6f;
                }
                importer.defaultSampleSettings = s;
            }

            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (pending) return;
                if (!Any(imported) && !Any(deleted) && !Any(moved) && !Any(movedFrom)) return;
                pending = true;
                EditorApplication.delayCall += () =>
                {
                    pending = false;
                    Rebuild(true);
                };
            }

            static bool Any(string[] paths)
            {
                foreach (var p in paths)
                    if (InAnyFolder(p) && !p.EndsWith(".meta")) return true;
                return false;
            }
        }
    }
}
