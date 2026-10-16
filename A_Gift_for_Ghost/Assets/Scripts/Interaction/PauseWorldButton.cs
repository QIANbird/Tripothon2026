using Ghost.Core;
using UnityEngine;

namespace Ghost.Interaction
{
    // 矩阵左上方的"Pause/Help"按钮：World Space Canvas（没有底板）+ BoxCollider，世界坐标固定、转向玩家。
    // 鼠标射线（PointerInput）和手柄射线（XRControllerInput 扳机短按）点到都走 IInteractable.OnTap → PauseMenu.Open()，
    // 和按 Esc / 左手柄 menu 打开的是同一个暂停界面。
    // 只在 ControlsGuideSettings.pauseButtonStages 里的阶段、且暂停界面可以打开时显示。暂停中也显示，再点一次 = 关闭暂停回到游戏
    // （VR：XRControllerInput 暂停 UI 模式的射线也打它；Canvas 的 sortingOrder 比暂停界面和头前幕布高，画在幕布上面）。
    // 位置 / 大小每帧从 ControlsGuideSettings 读，编辑模式下也执行（previewInEditMode 不为 None 时显示）。
    [ExecuteAlways]
    public class PauseWorldButton : MonoBehaviour, IInteractable
    {
        public PauseMenu menu;
        public GameFlow flow;
        public ControlsGuideSettings settings;
        public Transform viewCamera;
        public CanvasGroup group;
        public BoxCollider box;
        [Tooltip("悬停时放大的部分（圆）")]
        public RectTransform hoverTarget;
        public float hoverScale = 1.1f;

        bool hovered;
        float alpha;

        public void OnTap()
        {
            if (!Visible) return;
            if (menu == null) return;
            if (PauseMenu.IsPaused) menu.Close();
            else menu.Open();
        }

        public void OnHoverEnter() => hovered = true;
        public void OnHoverExit() => hovered = false;

        bool Visible => group != null && group.alpha > 0.5f;

        bool ShouldShow()
        {
            if (!Application.isPlaying) return settings.previewInEditMode != ControlsGuideSettings.Preview.None;
            if (menu == null || flow == null || (!PauseMenu.IsPaused && !menu.CanOpen)) return false;
            var stage = flow.CurrentStage;
            return stage != null && settings.pauseButtonStages != null
                && System.Array.IndexOf(settings.pauseButtonStages, stage.stageName) >= 0;
        }

        void LateUpdate()
        {
            if (settings == null || group == null) return;

            bool show = ShouldShow();
            if (Application.isPlaying)
            {
                float step = settings.fadeSeconds > 0f ? Time.unscaledDeltaTime / settings.fadeSeconds : 1f;
                alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, step);
            }
            else alpha = show ? 1f : 0f;
            if (group.alpha != alpha) group.alpha = alpha;
            bool clickable = show && alpha > 0.5f;
            if (box != null && box.enabled != clickable) box.enabled = clickable;
            if (!show) hovered = false;

            SetPosition(transform, settings.pauseButtonPosition);
            SetScale(transform, Vector3.one * Mathf.Max(1e-5f, settings.pauseButtonMetersPerPixel));
            Transform cam = viewCamera != null ? viewCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam != null)
            {
                // Canvas 正面朝 -Z：让 +Z 背对相机
                Vector3 away = transform.position - cam.position;
                if (settings.yawOnly) away.y = 0f;
                if (away.sqrMagnitude > 1e-6f) SetRotation(transform, Quaternion.LookRotation(away.normalized, Vector3.up));
            }

            if (hoverTarget != null)
            {
                float s = hovered ? hoverScale : 1f;
                float k = Application.isPlaying ? 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f) : 1f;
                SetScale(hoverTarget, Vector3.Lerp(hoverTarget.localScale, Vector3.one * s, k));
            }
        }

        // 编辑模式下每帧赋同样的值会把场景标成已修改，只在变了时才写
        static void SetPosition(Transform t, Vector3 v) { if ((t.position - v).sqrMagnitude > 1e-10f) t.position = v; }
        static void SetScale(Transform t, Vector3 v) { if ((t.localScale - v).sqrMagnitude > 1e-12f) t.localScale = v; }
        static void SetRotation(Transform t, Quaternion q) { if (Quaternion.Angle(t.rotation, q) > 0.01f) t.rotation = q; }
    }
}
