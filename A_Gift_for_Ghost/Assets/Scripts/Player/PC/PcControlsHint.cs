using Ghost.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Player.PC
{
    // PC 专用：Pick 阶段左上角的操作提示（浅色文字，没有底框）。文字来自台词表"操作提示"行，写的是键盘键位，
    // 所以只放在 PC 端；VR 版另写一个显示手柄提示的组件，订阅同一组事件。
    public class PcControlsHint : MonoBehaviour
    {
        public PickStage stage;
        public CanvasGroup group;
        public Text label;
        [Tooltip("淡入淡出时长（秒）")]
        public float fadeSeconds = 0.6f;

        float target;

        void OnEnable()
        {
            if (stage == null || group == null || label == null)
            {
                Debug.LogError("[Pick] PcControlsHint 缺少 stage / group / label", this);
                enabled = false;
                return;
            }
            stage.ControlsHintShown += Show;
            stage.ControlsHintHidden += Hide;
            target = 0f;
            group.alpha = 0f;
        }

        void OnDisable()
        {
            if (stage == null) return;
            stage.ControlsHintShown -= Show;
            stage.ControlsHintHidden -= Hide;
        }

        void Update()
        {
            float step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, step);
        }

        void Show(string text)
        {
            label.text = text;
            target = 1f;
        }

        void Hide()
        {
            target = 0f;
            group.alpha = 0f;
        }
    }
}
