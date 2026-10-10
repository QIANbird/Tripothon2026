using Ghost.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Ghost.Stages
{
    // Outro：全屏播放结局视频（Assets/Video/Outro.mp4），播完自动 Complete()，GameFlow 随后显示结束界面。
    // 画面：VideoPlayer 渲到运行时创建的 RenderTexture，再由 HUD Canvas 上的 RawImage 显示（黑底、按视频比例留边）。
    // 没有视频时只打警告并停在这一关，仍可用调试键 N 继续。
    // TODO(VR)：Quest 上把这块 Canvas 换成世界空间屏幕（docs/VR_GUIDELINES.md）。
    public class OutroVideoStage : Stage
    {
        public StageContext ctx;
        [Tooltip("结局视频；为空时不播放")]
        public VideoClip clip;
        public VideoPlayer player;
        [Tooltip("全屏视频层（Canvas 根物体），平时停用")]
        public GameObject screen;
        public RawImage image;
        [Tooltip("可选：让画面按视频比例适配")]
        public AspectRatioFitter fitter;

        public override string PanelText => "";

        RenderTexture target;

        void Awake()
        {
            if (screen != null) screen.SetActive(false);
            if (player != null) player.loopPointReached += HandleFinished;
        }

        void OnDestroy()
        {
            if (player != null) player.loopPointReached -= HandleFinished;
            ReleaseTarget();
        }

        public override void Enter()
        {
            base.Enter();
            if (ctx != null) ctx.ResetShared();
            if (clip == null || player == null)
            {
                Debug.LogWarning("[Stage] OutroVideoStage 没有视频或 VideoPlayer，按 N 继续", this);
                return;
            }

            ReleaseTarget();
            int w = clip.width > 0 ? (int)clip.width : 1920;
            int h = clip.height > 0 ? (int)clip.height : 1080;
            target = new RenderTexture(w, h, 0) { name = "OutroVideo" };
            player.clip = clip;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            if (image != null) image.texture = target;
            if (fitter != null) fitter.aspectRatio = (float)w / h;
            if (screen != null) screen.SetActive(true);
            player.Play();
        }

        public override void Exit()
        {
            if (player != null) player.Stop();
            if (screen != null) screen.SetActive(false);
            if (image != null) image.texture = null;
            ReleaseTarget();
            if (ctx != null) ctx.ResetShared();
            base.Exit();
        }

        void HandleFinished(VideoPlayer source)
        {
            // Complete() 自己会忽略不在进行中的情况
            Complete();
        }

        void ReleaseTarget()
        {
            if (target == null) return;
            if (player != null && player.targetTexture == target) player.targetTexture = null;
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
