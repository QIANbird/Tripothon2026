using System;
using System.Collections;
using System.Collections.Generic;
using Ghost.Core;
using UnityEngine;

namespace Ghost.Audio
{
    // 音效 / 音乐 / 环境音的播放入口。全部是 2D 声音（spatialBlend = 0），和输入设备、平台无关。
    //   音效：GameAudio.Play("SFX_AGENT_POPUP")；循环音效用 StartLoop / StopLoop。
    //   音乐、环境音：进关时按 stageAudio 表自动切换（交叉淡入淡出），也可以手动 PlayMusic / PlayAmbience。
    // 表里写的编号在 AudioLibrary 里找不到（音频还没交付）时，保持当前在播的不变，只打印一次提示。
    // 场景由 MainSceneMenu 生成（AudioSceneBuilder），不要手动摆放。
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        // 表里写这个值表示进这一关时停掉
        public const string None = "none";

        [Serializable]
        public class StageAudio
        {
            [Tooltip("Stage.stageName，例如 Intro、S1")]
            public string stageName;
            [Tooltip("进关时切到的音乐编号；空 = 不变，none = 停掉")]
            public string music;
            [Tooltip("进关时切到的环境音编号；空 = 不变，none = 停掉")]
            public string ambience;
        }

        public AudioLibrary library;
        public GameFlow flow;
        [Tooltip("所有音效（交互、消息提示、转场）的总音量；不影响配音、音乐和环境音")]
        [Range(0f, 1f)] public float sfxVolume = 0.25f;
        [Range(0f, 1f)] public float musicVolume = 0.6f;
        [Range(0f, 1f)] public float ambienceVolume = 0.5f;
        [Tooltip("音乐 / 环境音切换时的淡入淡出时长（秒）")]
        public float fadeTime = 1.5f;
        public List<StageAudio> stageAudio = new List<StageAudio>();

        AudioSource sfx, pitched;
        Channel music, ambience;
        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        readonly HashSet<string> warned = new HashSet<string>();

        void Awake()
        {
            Instance = this;
            sfx = NewSource("SFX");
            music = new Channel(this, "Music");
            ambience = new Channel(this, "Ambience");
        }

        void OnEnable()
        {
            if (flow != null) flow.StageEntered += HandleStageEntered;
        }

        void OnDisable()
        {
            if (flow != null) flow.StageEntered -= HandleStageEntered;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- 音效 ----

        // 播一次音效；场景里没有 GameAudio 时什么都不做
        public static void Play(string id)
        {
            if (Instance != null) Instance.PlayOneShot(id);
        }

        public void PlayOneShot(string id)
        {
            var entry = Find(id);
            var clip = AudioLibrary.PickClip(entry);
            if (clip == null) return;
            sfx.PlayOneShot(clip, entry.volume * sfxVolume);
        }

        // 播指定的变体（按文件名排序，0 = _01）。变体不存在时不播（不退回别的变体）
        public void PlayVariant(string id, int index)
        {
            var entry = Find(id);
            if (entry == null || entry.clips == null || index < 0 || index >= entry.clips.Length || entry.clips[index] == null)
            {
                if (entry != null && warned.Add(id + "#" + index))
                    Debug.Log($"[Audio] {id} 没有第 {index + 1} 个变体，跳过", this);
                return;
            }
            sfx.PlayOneShot(entry.clips[index], entry.volume * sfxVolume);
        }

        // 变调播一次（同一音效连续触发时逐个升高音高）。用单独的 AudioSource，不影响其他音效
        public void PlayPitched(string id, float pitch)
        {
            var entry = Find(id);
            var clip = AudioLibrary.PickClip(entry);
            if (clip == null) return;
            if (pitched == null) pitched = NewSource("SFX Pitched");
            pitched.pitch = pitch;
            pitched.PlayOneShot(clip, entry.volume * sfxVolume);
        }

        public bool IsLooping(string id) => loops.TryGetValue(id, out var s) && s != null && s.isPlaying;

        public void StopAllLoops()
        {
            foreach (var s in loops.Values) if (s != null) s.Stop();
        }

        // 开始循环播放一个音效（拖拽声、打字声）；已经在播时不重新开始
        public void StartLoop(string id)
        {
            if (loops.TryGetValue(id, out var playing) && playing.isPlaying) return;
            var entry = Find(id);
            var clip = AudioLibrary.PickClip(entry);
            if (clip == null) return;
            if (playing == null)
            {
                playing = NewSource("Loop " + id);
                loops[id] = playing;
            }
            playing.clip = clip;
            playing.loop = true;
            playing.volume = entry.volume * sfxVolume;
            playing.Play();
        }

        public void StopLoop(string id)
        {
            if (loops.TryGetValue(id, out var source) && source != null) source.Stop();
        }

        // ---- 音乐 / 环境音 ----

        public string CurrentMusic => music.Id;
        public string CurrentAmbience => ambience.Id;

        public void PlayMusic(string id) => Switch(music, id, musicVolume);
        public void StopMusic() => music.FadeTo(null, null, 0f, fadeTime);
        public void PlayAmbience(string id) => Switch(ambience, id, ambienceVolume);
        public void StopAmbience() => ambience.FadeTo(null, null, 0f, fadeTime);

        void HandleStageEntered(Stage stage)
        {
            if (stage == null) return;
            foreach (var row in stageAudio)
            {
                if (row == null || row.stageName != stage.stageName) continue;
                Apply(music, row.music, musicVolume);
                Apply(ambience, row.ambience, ambienceVolume);
                return;
            }
        }

        void Apply(Channel channel, string id, float volume)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id == None) channel.FadeTo(null, null, 0f, fadeTime);
            else Switch(channel, id, volume);
        }

        void Switch(Channel channel, string id, float volume)
        {
            if (channel.Id == id) return; // 同一首继续播，不从头开始
            var entry = Find(id);
            var clip = AudioLibrary.PickClip(entry);
            if (clip == null) return; // 还没交付：保持当前在播的
            channel.FadeTo(id, clip, entry.volume * volume, fadeTime);
        }

        // ---- 内部 ----

        AudioLibrary.Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var entry = library != null ? library.Get(id) : null;
            if ((entry == null || AudioLibrary.PickClip(entry) == null) && warned.Add(id))
                Debug.Log($"[Audio] 还没有 {id} 的音频，跳过", this);
            return entry;
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        // 一条可以交叉淡入淡出的长音频通道（两个 AudioSource 轮流用）
        class Channel
        {
            readonly GameAudio owner;
            readonly AudioSource[] sources = new AudioSource[2];
            int active;
            Coroutine fade;

            public string Id { get; private set; }

            public Channel(GameAudio owner, string name)
            {
                this.owner = owner;
                for (int i = 0; i < 2; i++)
                {
                    sources[i] = owner.NewSource($"{name} {i}");
                    sources[i].loop = true;
                }
            }

            // clip 为 null 时只淡出当前的
            public void FadeTo(string id, AudioClip clip, float volume, float time)
            {
                Id = id;
                var from = sources[active];
                AudioSource to = null;
                if (clip != null)
                {
                    active = 1 - active;
                    to = sources[active];
                    to.Stop();
                    to.clip = clip;
                    to.volume = 0f;
                    to.Play();
                }
                if (fade != null) owner.StopCoroutine(fade);
                fade = owner.StartCoroutine(Crossfade(from, to, volume, time));
            }

            static IEnumerator Crossfade(AudioSource from, AudioSource to, float target, float time)
            {
                float fromStart = from.volume;
                for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
                {
                    float k = t / time;
                    from.volume = Mathf.Lerp(fromStart, 0f, k);
                    if (to != null) to.volume = Mathf.Lerp(0f, target, k);
                    yield return null;
                }
                from.volume = 0f;
                from.Stop();
                if (to != null) to.volume = target;
            }
        }
    }
}
