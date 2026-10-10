using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Core
{
    // 开始 / 结束界面的共用部分：全屏 CanvasGroup 淡入淡出 + 一个按钮。
    // 【技术债】比赛期是 Screen Space HUD，按钮走 UGUI EventSystem；VR 版用 XR UI 射线点同一个 Button。
    public abstract class FlowScreen : MonoBehaviour
    {
        public GameFlow flow;
        public CanvasGroup group;
        public Button button;
        [Tooltip("淡入淡出时长（秒）")]
        public float fade = 0.8f;

        float target;

        protected virtual void Awake()
        {
            if (flow == null || group == null || button == null)
            {
                Debug.LogError($"[Flow] {GetType().Name} 缺少 flow / group / button", this);
                enabled = false;
                return;
            }
            button.onClick.AddListener(OnButton);
        }

        protected virtual void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnButton);
        }

        protected abstract void OnButton();

        protected void SetVisible(bool visible, bool immediate)
        {
            target = visible ? 1f : 0f;
            // 淡出过程中就不再挡点击，淡入时立刻可点
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (immediate) group.alpha = target;
        }

        protected virtual void Update()
        {
            if (Mathf.Approximately(group.alpha, target)) return;
            float speed = fade > 0f ? 1f / fade : float.MaxValue;
            group.alpha = Mathf.MoveTowards(group.alpha, target, speed * Time.unscaledDeltaTime);
        }
    }
}
