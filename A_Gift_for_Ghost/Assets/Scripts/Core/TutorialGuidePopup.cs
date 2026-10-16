using UnityEngine;
using UnityEngine.XR;

namespace Ghost.Core
{
    // 教学操作提示弹窗（docs/tasks/tut-guide-popups.md）：灰色半透明圆角底板（Ghost/GuidePanel）+ 手柄示意图（Ghost/GuideUnlit），
    // 两块面片，世界坐标固定在演示方块右侧、只绕竖直轴转向相机。没有 Collider，不挡射线。
    // TutorialStage 在阶段切换点调 Show / Hide / HideImmediate；位置、大小、动画参数每帧从 ControlsGuideSettings 的"教学弹窗"一栏读。
    // 滑入：从目标位置再往右 slideDistance、透明度 0 开始，ease-out 到目标；滑出反过来（ease-in），结束后隐藏。
    public class TutorialGuidePopup : MonoBehaviour
    {
        public enum Kind { Submit, Details }

        public ControlsGuideSettings settings;
        [Tooltip("玩家相机")]
        public Transform viewCamera;
        public MeshRenderer panel;
        public MeshRenderer image;

        Kind kind;
        Transform anchorSpace;
        Vector3 anchorLocal;
        bool active;
        float progress;  // 0 = 隐藏位置，1 = 目标位置
        int direction;   // +1 滑入，-1 滑出

        MaterialPropertyBlock block;
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");

        public bool IsShowing => active && direction > 0;

        // 锚点 = space 空间里的一点（演示方块中心，植株本地空间），每帧换算到世界，跟着 PlantFit 走
        public void Show(Kind which, Transform space, Vector3 localPoint)
        {
            // 同一时刻只有一张：换图时直接从头滑入
            if (!active || kind != which) progress = 0f;
            kind = which;
            anchorSpace = space;
            anchorLocal = localPoint;
            active = true;
            direction = 1;
        }

        public void Hide()
        {
            if (active) direction = -1;
        }

        public void HideImmediate()
        {
            active = false;
            progress = 0f;
            direction = 0;
            SetVisible(false);
        }

        void Awake() => SetVisible(false);

        void LateUpdate()
        {
            if (!active || settings == null) return;

            float seconds = settings.tutorialPopupSlideSeconds;
            progress = Mathf.Clamp01(progress + direction * (seconds > 0f ? Time.deltaTime / seconds : 1f));
            if (direction < 0 && progress <= 0f)
            {
                HideImmediate();
                return;
            }

            var tex = kind == Kind.Submit ? settings.tutorialSubmitImage : settings.tutorialDetailsImage;
            if (tex == null || (!settings.showOnPC && !XRSettings.isDeviceActive))
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);

            // 滑入（ease-out）和滑出（ease-in，倒着走）是同一条曲线
            float e = 1f - (1f - progress) * (1f - progress);

            Transform cam = viewCamera != null ? viewCamera : (Camera.main != null ? Camera.main.transform : null);
            Vector3 anchor = anchorSpace != null ? anchorSpace.TransformPoint(anchorLocal) : anchorLocal;
            Vector3 target = anchor + settings.tutorialPopupOffset;
            Vector3 right = Vector3.right;
            if (cam != null)
            {
                right = cam.right;
                right.y = 0f;
                right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            }
            transform.position = Vector3.Lerp(target + right * settings.tutorialPopupSlideDistance, target, e);

            if (cam != null)
            {
                // Quad 的正面朝 -Z：让 +Z 背对相机
                Vector3 away = transform.position - cam.position;
                if (settings.yawOnly) away.y = 0f;
                if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }

            // 尺寸：底板高 H，四周留 padding × H，图片按比例放在中间
            float h = Mathf.Max(0.01f, settings.tutorialPopupHeight);
            float pad = h * Mathf.Clamp(settings.tutorialPopupPadding, 0f, 0.45f);
            float imageH = h - 2f * pad;
            float imageAspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
            float imageW = imageH * imageAspect;
            float panelW = imageW + 2f * pad;
            panel.transform.localScale = new Vector3(panelW, h, 1f);
            image.transform.localScale = new Vector3(imageW, imageH, 1f);
            // 图片略靠近相机（-Z），和底板之间不互相穿插
            image.transform.localPosition = new Vector3(0f, 0f, -0.002f);

            float alpha = e;
            block ??= new MaterialPropertyBlock();
            panel.GetPropertyBlock(block);
            block.SetColor(ColorId, settings.tutorialPopupColor);
            block.SetFloat(AlphaId, alpha);
            block.SetFloat(AspectId, panelW / h);
            block.SetFloat(RadiusId, settings.tutorialPopupCornerRadius);
            panel.SetPropertyBlock(block);

            image.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, tex);
            block.SetFloat(AlphaId, alpha);
            image.SetPropertyBlock(block);
        }

        void SetVisible(bool visible)
        {
            if (panel != null && panel.enabled != visible) panel.enabled = visible;
            if (image != null && image.enabled != visible) image.enabled = visible;
        }
    }
}
