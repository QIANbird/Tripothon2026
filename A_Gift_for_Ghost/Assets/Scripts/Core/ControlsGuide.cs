using UnityEngine;
using UnityEngine.XR;

namespace Ghost.Core
{
    // 键位提示图：一块世界坐标固定的面片，始终转向玩家（相机）。进 S1–S4 / Pick 时按 ControlsGuideSettings 换图、
    // 摆位置，其他阶段淡出。位置 / 大小每帧从 settings 读，编辑模式下也执行，方便在 Scene 视图里调。
    // 只订阅 GameFlow 的阶段事件，不改玩法逻辑；面片没有 Collider，不挡节点拾取和 UI 射线。
    [ExecuteAlways]
    public class ControlsGuide : MonoBehaviour
    {
        public GameFlow flow;
        public ControlsGuideSettings settings;
        [Tooltip("玩家相机（Pick 阶段跟着玩家走的也是它）")]
        public Transform viewCamera;
        public MeshRenderer quad;

        ControlsGuideSettings.Entry current;
        ControlsGuideSettings.Entry shown;
        float alpha;
        MaterialPropertyBlock block;
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        void OnEnable()
        {
            if (flow != null && Application.isPlaying)
            {
                flow.StageEntered += OnStageEntered;
                flow.StageExited += OnStageExited;
                if (flow.CurrentStage != null && flow.CurrentStage.IsActive) OnStageEntered(flow.CurrentStage);
            }
            alpha = 0f;
        }

        void OnDisable()
        {
            if (flow == null) return;
            flow.StageEntered -= OnStageEntered;
            flow.StageExited -= OnStageExited;
        }

        void OnStageEntered(Stage stage)
        {
            current = settings != null && stage != null ? settings.Find(stage.stageName) : null;
        }

        void OnStageExited(Stage stage)
        {
            current = null;
        }

        void LateUpdate()
        {
            if (settings == null || quad == null) return;

            ControlsGuideSettings.Entry target;
            if (Application.isPlaying)
            {
                target = current;
                if (!settings.showOnPC && !XRSettings.isDeviceActive) target = null;
                if (PauseMenu.IsPaused) target = null;
                // 换图：先淡出再换
                float step = settings.fadeSeconds > 0f ? Time.unscaledDeltaTime / settings.fadeSeconds : 1f;
                if (target != shown)
                {
                    alpha = Mathf.MoveTowards(alpha, 0f, step);
                    if (alpha <= 0f) shown = target;
                }
                else alpha = Mathf.MoveTowards(alpha, shown != null ? 1f : 0f, step);
            }
            else
            {
                shown = settings.PreviewEntry;
                alpha = shown != null ? 1f : 0f;
            }

            bool visible = shown != null && shown.image != null && alpha > 0f;
            quad.enabled = visible;
            if (!visible) return;

            var t = quad.transform;
            SetPosition(t, shown.position);
            float aspect = shown.image.height > 0 ? (float)shown.image.width / shown.image.height : 1f;
            SetScale(t, new Vector3(shown.height * aspect, shown.height, 1f));

            Transform cam = viewCamera != null ? viewCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam != null)
            {
                // Quad 的正面朝 -Z：让 +Z 背对相机
                Vector3 away = t.position - cam.position;
                if (settings.yawOnly) away.y = 0f;
                if (away.sqrMagnitude > 1e-6f) SetRotation(t, Quaternion.LookRotation(away.normalized, Vector3.up));
            }

            block ??= new MaterialPropertyBlock();
            quad.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, shown.image);
            block.SetFloat(AlphaId, alpha * settings.opacity);
            quad.SetPropertyBlock(block);
        }

        // 编辑模式下每帧赋同样的值会把场景标成已修改，只在变了时才写
        static void SetPosition(Transform t, Vector3 v) { if ((t.position - v).sqrMagnitude > 1e-10f) t.position = v; }
        static void SetScale(Transform t, Vector3 v) { if ((t.localScale - v).sqrMagnitude > 1e-12f) t.localScale = v; }
        static void SetRotation(Transform t, Quaternion q) { if (Quaternion.Angle(t.rotation, q) > 0.01f) t.rotation = q; }
    }
}
