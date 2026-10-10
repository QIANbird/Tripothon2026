using Ghost.Core;
using UnityEngine;

namespace Ghost.Audio.EditorTools
{
    // MainSceneMenu 调用：在场景里建 GameAudio，挂上音频库和每关的音乐 / 环境音表。
    // 表按 05_audio_list.xlsx 的"音乐与环境音"页；还没交付的编号运行时会跳过、保持上一首。
    public static class AudioSceneBuilder
    {
        public static GameAudio Build(GameFlow flow)
        {
            var go = new GameObject("GameAudio");
            var audio = go.AddComponent<GameAudio>();
            audio.flow = flow;
            audio.library = AudioLibraryBuilder.EnsureLibrary();
            AudioLibraryBuilder.Rebuild(false);

            void Row(string stage, string music, string ambience) =>
                audio.stageAudio.Add(new GameAudio.StageAudio { stageName = stage, music = music, ambience = ambience });

            // Intro 开场：BGM_DIGITAL 开头自带开场动画的音效，人声随后进来；TUT–S1 继续同一首
            Row("Intro", "BGM_DIGITAL", "");
            Row("S2", "BGM_CIRCUIT", "");
            Row("S3", "BGM_COLOR", "");
            Row("S4", "BGM_AWAKEN", "");
            Row("Transition", "", "AMB_BALCONY");
            Row("Pick", "BGM_PICK", "AMB_BALCONY");
            return audio;
        }
    }
}
