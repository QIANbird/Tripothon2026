using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Narrative
{
    // 按顺序播放 DialogueSequence：有音频就放音频，时长取音频长度；没有音频按字数估算。
    // 字幕显示由 SubtitlePanel 订阅这里的事件完成，播放器本身不管界面。
    //
    // 队列规则：
    //   Play(seq, onComplete)    —— 打断当前播放、清空队列，立刻播放 seq（被打断的那段不回调 onComplete）。
    //   Enqueue(seq, onComplete) —— 排在队列末尾；空闲时立刻开始。
    //   Skip()                   —— 跳过当前这句，进入下一句（最后一句被跳过时，整段正常结束并回调）。
    //   Stop()                   —— 停止并清空队列，不回调任何 onComplete，不发 SequenceFinished。
    public class DialoguePlayer : MonoBehaviour
    {
        [Tooltip("播放配音的 AudioSource；为空时自动在本物体上添加一个 2D AudioSource")]
        public AudioSource audioSource;
        [Tooltip("没有音频时的估算语速（字 / 秒）")]
        public float charsPerSecond = 5f;
        [Tooltip("估算时长的下限（秒）")]
        public float minDuration = 1.5f;
        [Tooltip("句与句之间的默认间隔（秒），台词的 pauseAfter < 0 时使用")]
        public float defaultPause = 0.4f;

        // 一句开始 / 结束（Skip 也算结束）时发出
        public event Action<DialogueLine> LineStarted;
        public event Action<DialogueLine> LineFinished;
        // 一整段自然播完（或最后一句被 Skip）时发出；Stop 不发
        public event Action<DialogueSequence> SequenceFinished;
        // 停止（Stop 或被 Play 打断）时发出，字幕面板用来隐藏
        public event Action Stopped;

        struct Request
        {
            public DialogueSequence sequence;
            public Action onComplete;
        }

        readonly Queue<Request> queue = new Queue<Request>();
        Coroutine routine;
        bool skipRequested;
        int generation; // Stop() 时加一；回调里调 Play/Stop 时，旧协程据此退出

        public bool IsPlaying => routine != null;
        public DialogueSequence CurrentSequence { get; private set; }
        public DialogueLine CurrentLine { get; private set; }

        void Awake()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 语音通过"脑机接口"直接听到，不做空间化
            }
        }

        void OnDisable()
        {
            Stop();
        }

        public void Play(DialogueSequence sequence, Action onComplete = null)
        {
            Stop();
            Enqueue(sequence, onComplete);
        }

        public void Enqueue(DialogueSequence sequence, Action onComplete = null)
        {
            if (sequence == null)
            {
                Debug.LogWarning("[Dialogue] 序列为空，直接回调", this);
                onComplete?.Invoke();
                return;
            }
            queue.Enqueue(new Request { sequence = sequence, onComplete = onComplete });
            if (routine == null && isActiveAndEnabled) routine = StartCoroutine(Run(generation));
        }

        public void Skip()
        {
            if (routine != null) skipRequested = true;
        }

        public void Stop()
        {
            bool wasPlaying = routine != null;
            generation++;
            queue.Clear();
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (audioSource != null) audioSource.Stop();
            CurrentSequence = null;
            CurrentLine = null;
            skipRequested = false;
            if (wasPlaying) Stopped?.Invoke();
        }

        // 某句台词实际要显示多久（不含 pauseAfter）
        public float DurationOf(DialogueLine line)
        {
            if (line.durationOverride > 0f) return line.durationOverride;
            if (line.clip != null) return line.clip.length;
            int chars = string.IsNullOrEmpty(line.text) ? 0 : line.text.Length;
            return Mathf.Max(minDuration, chars / Mathf.Max(0.1f, charsPerSecond));
        }

        IEnumerator Run(int gen)
        {
            while (queue.Count > 0)
            {
                var request = queue.Dequeue();
                var sequence = request.sequence;
                CurrentSequence = sequence;

                if (sequence.startDelay > 0f) yield return new WaitForSeconds(sequence.startDelay);

                foreach (var line in sequence.lines)
                {
                    if (line == null) continue;
                    CurrentLine = line;
                    skipRequested = false;
                    if (line.clip != null && audioSource != null)
                    {
                        audioSource.clip = line.clip;
                        audioSource.Play();
                    }
                    LineStarted?.Invoke(line);
                    if (gen != generation) yield break;

                    float duration = DurationOf(line);
                    for (float t = 0f; t < duration && !skipRequested; t += Time.deltaTime)
                        yield return null;

                    if (audioSource != null && audioSource.clip == line.clip) audioSource.Stop();
                    LineFinished?.Invoke(line);
                    if (gen != generation) yield break;

                    float pause = line.pauseAfter >= 0f ? line.pauseAfter : defaultPause;
                    // 被跳过的句子不再等间隔
                    if (!skipRequested && pause > 0f) yield return new WaitForSeconds(pause);
                    skipRequested = false;
                }

                CurrentLine = null;
                CurrentSequence = null;
                // 最后一段：先标记空闲，回调里就可以安全地 Play / Enqueue 新序列
                bool last = queue.Count == 0;
                if (last) routine = null;
                SequenceFinished?.Invoke(sequence);
                if (gen != generation) yield break;
                request.onComplete?.Invoke();
                if (last || gen != generation) yield break;
            }
            routine = null;
        }
    }
}
