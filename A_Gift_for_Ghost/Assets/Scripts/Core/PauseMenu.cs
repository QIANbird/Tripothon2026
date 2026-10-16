using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Ghost.Core
{
    // 暂停界面（docs/tasks/outro02-end-pause-screens.md 第 3 部分）：半透明幕布 + 中央缓慢旋转的 #E5FF71 菱形 + 底部"返回开始界面"。
    // 共享逻辑，不读设备：Toggle 由 Interaction/PauseInput（Gameplay 表的 Pause：Esc / 左手柄 menu）调用。
    //   只在游戏进行中可开：flow 已开始、未结束，开始 / 结束界面都不可见。Outro 视频期间也可开（暂停视频）。
    //   打开：timeScale = 0、AudioListener.pause、暂停正在播的视频、停用 blockedMaps（Player 表：走路 / 转头 / 摘 / 吃；Debug 表：跳关）；
    //         关闭时原样恢复。Gameplay 表保持启用（VR 要用扳机 / A 点"返回开始界面"、再按 menu 关闭），
    //         PointerInput / XRControllerInput / DialogueAdvanceInput 自己看 IsPaused 不派发节点交互、不推进对白。
    //   动画全部用 unscaled time，暂停时界面照常淡入、菱形照常转。
    public class PauseMenu : MonoBehaviour
    {
        public GameFlow flow;
        public CanvasGroup group;
        [Tooltip("\"返回开始界面\"按钮")]
        public Button button;
        [Tooltip("中央的菱形（绕 Z 轴转）")]
        public RectTransform diamond;
        [Tooltip("PC 的全屏幕布。VR 下关掉，改用头前的 XRScreenBackdrop")]
        public Graphic dim;
        [Tooltip("按钮下方的手柄提示（A 确认 / 菜单键返回游戏），只在 VR 显示")]
        public GameObject xrHints;
        [Tooltip("这些界面（开始 / 结束）可见时不能暂停")]
        public CanvasGroup[] blockingScreens;
        [Tooltip("暂停时一起暂停的视频（Outro）")]
        public VideoPlayer[] videos;
        [Tooltip("InputSystem_Actions.inputactions；暂停期间停用 blockedMaps")]
        public InputActionAsset actions;
        public string[] blockedMaps = { "Player", "Debug" };
        [Tooltip("淡入淡出时长（秒，unscaled）")]
        public float fade = 0.25f;
        [Tooltip("菱形旋转速度（度/秒）")]
        public float spinSpeed = 30f;
        [Tooltip("菱形呼吸缩放的幅度（0 = 不缩放）和周期（秒）")]
        public float breatheAmount = 0.04f;
        public float breathePeriod = 3f;

        [Tooltip("每次打开时调用（VR：面板重新对准视线）")]
        public UnityEvent opened = new UnityEvent();

        public static bool IsPaused { get; private set; }
        public static event Action<bool> PausedChanged;

        float target;
        float savedTimeScale = 1f;
        bool[] videoWasPlaying;
        bool[] mapWasEnabled;
        float spinTime;
        bool reloading;

        // 关掉"进入 Play 不重载域"时，静态状态不会自动清
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            PausedChanged = null;
        }

        void Awake()
        {
            if (flow == null || group == null || button == null)
            {
                Debug.LogError("[Pause] PauseMenu 缺少 flow / group / button", this);
                enabled = false;
                return;
            }
            IsPaused = false;
            button.onClick.AddListener(ReturnToStart);
            SetVisible(false, immediate: true);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(ReturnToStart);
            // 暂停中被销毁（重载场景）：时间和音频交给 FlowScreen.ReloadToStart 恢复，这里只清标记
            IsPaused = false;
        }

        public bool CanOpen
        {
            get
            {
                if (!enabled || flow == null || !flow.HasStarted || flow.IsFinished) return false;
                if (blockingScreens != null)
                    foreach (var g in blockingScreens)
                        if (g != null && (g.alpha > 0.01f || g.blocksRaycasts)) return false;
                return true;
            }
        }

        public void Toggle()
        {
            if (IsPaused) Close();
            else Open();
        }

        public void Open()
        {
            if (IsPaused || reloading || !CanOpen) return;
            IsPaused = true;

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            int n = videos != null ? videos.Length : 0;
            videoWasPlaying = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var v = videos[i];
                if (v == null || !v.isPlaying) continue;
                videoWasPlaying[i] = true;
                v.Pause();
            }

            int m = blockedMaps != null ? blockedMaps.Length : 0;
            mapWasEnabled = new bool[m];
            for (int i = 0; i < m; i++)
            {
                var map = actions != null ? actions.FindActionMap(blockedMaps[i], throwIfNotFound: false) : null;
                if (map == null || !map.enabled) continue;
                mapWasEnabled[i] = true;
                map.Disable();
            }

            spinTime = 0f;
            SetVisible(true, immediate: false);
            Debug.Log("[Pause] 暂停", this);
            PausedChanged?.Invoke(true);
            opened?.Invoke();
        }

        public void Close()
        {
            if (!IsPaused) return;
            Restore();
            SetVisible(false, immediate: false);
            Debug.Log("[Pause] 继续", this);
            PausedChanged?.Invoke(false);
        }

        // 暂停中点"返回开始界面"：先恢复时间 / 音频，再重载场景（和结束界面同一个做法）
        public void ReturnToStart()
        {
            if (!IsPaused || reloading) return;
            reloading = true;
            Restore();
            FlowScreen.ReloadToStart();
        }

        // VR：幕布改用头前的 XRScreenBackdrop，面板上不画全屏幕布（XRWorldCanvas.worldModeChanged 调用）
        public void SetWorldLayout(bool world)
        {
            if (dim != null) dim.enabled = !world;
            if (xrHints != null) xrHints.SetActive(world);
        }

        void Restore()
        {
            IsPaused = false;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;

            if (videoWasPlaying != null)
                for (int i = 0; i < videoWasPlaying.Length && i < videos.Length; i++)
                    if (videoWasPlaying[i] && videos[i] != null) videos[i].Play();
            videoWasPlaying = null;

            if (mapWasEnabled != null)
                for (int i = 0; i < mapWasEnabled.Length && i < blockedMaps.Length; i++)
                {
                    if (!mapWasEnabled[i]) continue;
                    var map = actions != null ? actions.FindActionMap(blockedMaps[i], throwIfNotFound: false) : null;
                    if (map != null) map.Enable();
                }
            mapWasEnabled = null;
        }

        void SetVisible(bool visible, bool immediate)
        {
            target = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (immediate) group.alpha = target;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!Mathf.Approximately(group.alpha, target))
            {
                float speed = fade > 0f ? 1f / fade : float.MaxValue;
                group.alpha = Mathf.MoveTowards(group.alpha, target, speed * dt);
            }
            if (diamond != null && group.alpha > 0f)
            {
                spinTime += dt;
                diamond.localRotation = Quaternion.Euler(0f, 0f, 45f + spinTime * spinSpeed);
                float s = breathePeriod > 0f ? 1f + Mathf.Sin(spinTime * 2f * Mathf.PI / breathePeriod) * breatheAmount : 1f;
                diamond.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
