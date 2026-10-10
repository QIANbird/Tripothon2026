using Ghost.Core;
using UnityEngine;

namespace Ghost.Audio.EditorTools
{
    // MainSceneMenu 调用：在场景里建 GameAudio，挂上音频库和每关的音乐 / 环境音表。
    // 必须在 flow.stages 填好之后调用。还没交付的编号运行时会跳过、保持上一首。
    public static class AudioSceneBuilder
    {
        public static GameAudio Build(GameFlow flow)
        {
            var go = new GameObject("GameAudio");
            var audio = go.AddComponent<GameAudio>();
            audio.flow = flow;
            audio.library = AudioLibraryBuilder.EnsureLibrary();
            // 音效压到 1/4，不盖过人声和 BGM（10-10 试听后两次减半）
            audio.sfxVolume = 0.25f;
            AudioLibraryBuilder.Rebuild(false);

            go.AddComponent<GameAudioCues>();

            // 一首 BGM_DIGITAL 从 Intro 贯穿到 Outro 之前（开头自带开场动画的音效，人声随后进来），进 Outro 时淡出。
            // 每关都写同一首：同一首在播时不会从头开始；从 Outro 跳回前面的关也会重新响起
            foreach (var stage in flow.stages)
            {
                if (stage == null) continue;
                bool outro = stage.stageName == "Outro";
                string ambience = stage.stageName == "Transition" || stage.stageName == "Pick" ? "AMB_BALCONY"
                    : outro ? GameAudio.None : "";
                audio.stageAudio.Add(new GameAudio.StageAudio
                {
                    stageName = stage.stageName,
                    music = outro ? GameAudio.None : "BGM_DIGITAL",
                    ambience = ambience,
                });
            }
            return audio;
        }
    }
}
