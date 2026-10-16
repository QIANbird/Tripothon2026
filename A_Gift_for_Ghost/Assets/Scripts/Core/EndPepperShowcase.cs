using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Ghost.Core
{
    // 结束界面中央的彩椒展示（docs/tasks/outro02-end-pause-screens.md 第 2 部分）。结束界面出现时显示，消失时收起。
    //   默认（屏幕 UI）：彩椒放在场景外的专用相机前，只拍 EndShowcase 层，渲到 RenderTexture，结束界面的 RawImage 显示。
    //                  Overlay UI 里画不了 3D 物体，所以绕一道 RT；相机平时关着，界面可见时才开。
    //   世界模式：彩椒本身就摆在结束界面面板前（由 VR 侧的组件挪过去并调 SetWorldMode(true)），不开相机、隐藏 RawImage。
    // 这里不判断设备，只看 SetWorldMode。
    public class EndPepperShowcase : MonoBehaviour
    {
        [Tooltip("结束界面的 CanvasGroup：透明度 > 0 时显示")]
        public CanvasGroup source;
        [Tooltip("彩椒展示物体（含 EndPepperSpin）")]
        public GameObject pepper;
        [Tooltip("屏幕 UI 模式下拍彩椒的相机")]
        public Camera viewCamera;
        [Tooltip("结束界面上显示 RT 的 RawImage")]
        public RawImage view;
        public int textureSize = 1024;
        [Tooltip("专用相机渲染时临时关掉的渲染器 Feature（屏幕压暗边会把 RT 四周也压一圈）")]
        public ScriptableRendererFeature[] hideFeatures;
        [Tooltip("世界模式下，结束界面透明度超过这个值才显示彩椒（不透明物体不能跟着淡入）")]
        public float worldShowAlpha = 0.5f;

        public bool WorldMode { get; private set; }

        RenderTexture target;
        bool[] savedActive;
        bool pepperOn, cameraOn;
        bool applied;

        void Awake()
        {
            if (pepper != null) pepper.SetActive(false);
            if (viewCamera != null) viewCamera.enabled = false;
            if (view != null) view.enabled = false;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += HandleBeginCamera;
            RenderPipelineManager.endCameraRendering += HandleEndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCamera;
            RenderPipelineManager.endCameraRendering -= HandleEndCamera;
        }

        void OnDestroy() => ReleaseTarget();

        public void SetWorldMode(bool world)
        {
            if (WorldMode == world && applied) return;
            WorldMode = world;
            applied = false;
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            float alpha = source != null ? source.alpha : 0f;
            bool screenOn = alpha > 0.001f;
            bool wantPepper = WorldMode ? alpha > worldShowAlpha : screenOn;
            bool wantCamera = !WorldMode && screenOn;
            if (applied && wantPepper == pepperOn && wantCamera == cameraOn) return;
            applied = true;
            pepperOn = wantPepper;
            cameraOn = wantCamera;

            if (pepper != null) pepper.SetActive(pepperOn);
            if (view != null) view.enabled = !WorldMode && screenOn;
            if (viewCamera == null) return;
            if (cameraOn)
            {
                if (target == null)
                {
                    target = new RenderTexture(textureSize, textureSize, 24) { name = "EndPepperView" };
                    target.Create();
                }
                viewCamera.targetTexture = target;
                if (view != null) view.texture = target;
            }
            viewCamera.enabled = cameraOn;
        }

        void HandleBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != viewCamera || hideFeatures == null) return;
            if (savedActive == null || savedActive.Length != hideFeatures.Length) savedActive = new bool[hideFeatures.Length];
            for (int i = 0; i < hideFeatures.Length; i++)
            {
                var f = hideFeatures[i];
                if (f == null) continue;
                savedActive[i] = f.isActive;
                f.SetActive(false);
            }
        }

        void HandleEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != viewCamera || hideFeatures == null || savedActive == null) return;
            for (int i = 0; i < hideFeatures.Length && i < savedActive.Length; i++)
                if (hideFeatures[i] != null) hideFeatures[i].SetActive(savedActive[i]);
        }

        void ReleaseTarget()
        {
            if (target == null) return;
            if (viewCamera != null && viewCamera.targetTexture == target) viewCamera.targetTexture = null;
            if (view != null && view.texture == target) view.texture = null;
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
