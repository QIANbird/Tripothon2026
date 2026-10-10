using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Audio
{
    // 音效 / 音乐 / 环境音的索引：编号（05_audio_list.xlsx 里的 SFX_* / BGM_* / AMB_*）→ 音频。
    // 由 AudioLibraryBuilder 扫描 Assets/Audio/{SFX,BGM,AMB} 自动生成；
    // 文件名 = 编号，变体在后面加 _01、_02（SFX_MORPH_01 → SFX_MORPH 的第 1 个变体），播放时随机选一个。
    // 重新生成时保留每条已调过的音量。
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 1f)] public float volume = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }

        // 随机取一个变体；没有可用音频时返回 null
        public static AudioClip PickClip(Entry entry)
        {
            if (entry == null || entry.clips == null || entry.clips.Length == 0) return null;
            return entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
        }
    }
}
