using UnityEngine;

namespace Ghost.Narrative
{
    // 黑屏：一块挂在相机前方的 World Space 黑色面板（CanvasGroup 控制透明度）。
    // 只遮挡画面，不改相机参数、不控制镜头。开场剧情用它做黑屏，以后结局等也可以复用。
    public class ScreenBlackout : MonoBehaviour
    {
        public CanvasGroup group;
        [Tooltip("默认淡入淡出时长（秒）")]
        public float defaultFade = 1f;

        float target;
        float speed;

        public bool IsBlack => group != null && group.alpha >= 0.999f;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            target = group != null ? group.alpha : 0f;
        }

        // 立刻变黑 / 恢复
        public void SetImmediate(bool black)
        {
            target = black ? 1f : 0f;
            if (group != null) group.alpha = target;
        }

        // 渐变到黑 / 透明；fade < 0 用默认时长
        public void FadeTo(bool black, float fade = -1f)
        {
            if (fade < 0f) fade = defaultFade;
            target = black ? 1f : 0f;
            if (fade <= 0f) { SetImmediate(black); return; }
            speed = 1f / fade;
        }

        void Update()
        {
            if (group == null || Mathf.Approximately(group.alpha, target)) return;
            group.alpha = Mathf.MoveTowards(group.alpha, target, speed * Time.deltaTime);
        }
    }
}
