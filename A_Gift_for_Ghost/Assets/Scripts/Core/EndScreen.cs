using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Ghost.Core
{
    // 结束界面：GameFlow.FlowFinished 时淡入，先播结尾视频（有的话），播完露出"重新开始"，点击重新加载当前场景，回到开始界面。
    // 重载场景而不是 JumpTo(0)：各系统的运行时状态（写实度、PlantFit Hold、玩家相机、摘下的果实……）一次清干净。
    // 光标不在这里处理：最后一关之前的 Pick 阶段 Exit 时 PcCursorLock 已经恢复光标。
    public class EndScreen : FlowScreen
    {
        [Header("结尾视频（可选，不填就直接显示结束界面）")]
        public VideoPlayer video;
        [Tooltip("盖在标题和按钮上面的视频层，播放期间挡住按钮")]
        public GameObject videoLayer;
        public RawImage videoImage;
        public AspectRatioFitter videoFitter;

        bool reloading;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            flow.FlowFinished += HandleFinished;
            SetVisible(false, immediate: true);

            if (video != null)
            {
                video.playOnAwake = false;
                video.isLooping = false;
                // APIOnly：直接拿 VideoPlayer 的贴图给 RawImage，宽高比按视频本身算，不需要额外的 RenderTexture 资源
                video.renderMode = VideoRenderMode.APIOnly;
                video.prepareCompleted += HandlePrepared;
                video.loopPointReached += HandleVideoDone;
                video.errorReceived += HandleVideoError;
            }
            if (videoLayer != null) videoLayer.SetActive(false);
        }

        protected override void OnDestroy()
        {
            if (flow != null) flow.FlowFinished -= HandleFinished;
            if (video != null)
            {
                video.prepareCompleted -= HandlePrepared;
                video.loopPointReached -= HandleVideoDone;
                video.errorReceived -= HandleVideoError;
            }
            base.OnDestroy();
        }

        void HandleFinished()
        {
            SetVisible(true, immediate: false);
            if (video == null || videoLayer == null || video.clip == null) return;
            videoLayer.SetActive(true);
            video.Prepare(); // 准备期间视频层是黑的，和结束界面黑底一致
        }

        void HandlePrepared(VideoPlayer vp)
        {
            if (videoImage != null) videoImage.texture = vp.texture;
            if (videoFitter != null && vp.height > 0) videoFitter.aspectRatio = (float)vp.width / vp.height;
            vp.Play();
        }

        void HandleVideoDone(VideoPlayer vp)
        {
            vp.Stop();
            if (videoLayer != null) videoLayer.SetActive(false);
        }

        void HandleVideoError(VideoPlayer vp, string message)
        {
            Debug.LogError($"[Flow] 结尾视频播放失败，直接显示结束界面：{message}", this);
            HandleVideoDone(vp);
        }

        protected override void OnButton()
        {
            if (reloading) return;
            reloading = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
