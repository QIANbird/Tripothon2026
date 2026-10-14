using System.Collections;
using Ghost.Core;
using Ghost.Morph;
using Ghost.Stages;
using UnityEngine;

namespace Ghost.Interaction
{
    // Runtime-only prototype for the VR interaction "body" layer.
    // It does not replace gameplay logic: it reads PointerInput and the active Stage, then draws a ray/tool/hand proxy.
    // Quest Link path: PointerInput can be driven by the right XR controller; PC fallback: mouse ray + G as Grasp.
    [DefaultExecutionOrder(80)]
    public class InteractionAvatarVisual : MonoBehaviour
    {
        public enum Mode
        {
            Hidden,
            RayOnly,
            WaterConduit,
            Tweezer,
            ProtoFinger,
            GhostHand,
            StableHand,
        }

        [Header("Refs")]
        public StageContext ctx;

        [Header("Ray")]
        public float defaultDistance = 1.5f;
        public float rayWidth = 0.004f;
        public Color rayColor = new Color(0.42f, 0.8f, 1f, 0.65f);
        public Color waterRayColor = new Color(0.35f, 0.9f, 1f, 0.9f);

        [Header("Visual")]
        public float visualScale = 1f;
        public Color waterColor = new Color(0.35f, 0.9f, 1f, 0.85f);
        public Color tweezerColor = new Color(1f, 0.72f, 0.32f, 0.82f);
        public Color protoFingerColor = new Color(0.55f, 0.9f, 1f, 0.72f);
        public Color ghostHandColor = new Color(0.72f, 0.95f, 1f, 0.42f);
        public Color stableHandColor = new Color(0.9f, 0.98f, 1f, 0.55f);
        public Color failColor = new Color(1f, 0.28f, 0.34f, 0.9f);

        [Header("Motion")]
        public float tweezerTremor = 0.006f;
        public float protoFingerTremor = 0.0035f;
        public float ghostHandTremor = 0.0018f;
        public float failKickDuration = 0.18f;

        [Header("Desktop Simulation")]
        [Tooltip("Unity 内无 Quest 时，用键盘直接预览 VR 手部状态。H 开关强制预览，1-6 选外观，G 夹合，F 失败抖动")]
        public bool enableDesktopSimulation = true;

        LineRenderer rayLine;
        Transform visualRoot;
        Transform nozzle;
        Transform palm;
        Transform leftArm;
        Transform rightArm;
        Transform leftTip;
        Transform rightTip;
        Transform centerTip;

        Material rayMaterial;
        Material waterMaterial;
        Material tweezerMaterial;
        Material protoMaterial;
        Material ghostMaterial;
        Material stableMaterial;
        Material failMaterial;

        Mode currentMode = Mode.Hidden;
        Mode forcedMode = Mode.Hidden;
        float failKickUntil;
        int lastHoveredNode = -1;
        bool forcePreview;

        PointerInput Pointer => ctx != null ? ctx.pointer : null;

        void Awake()
        {
            if (ctx == null) ctx = FindAnyObjectByType<StageContext>(FindObjectsInactive.Include);
            BuildVisuals();
            HideAll();
        }

        void OnEnable()
        {
            if (Pointer != null) Pointer.HoverChanged += HandleHoverChanged;
        }

        void OnDisable()
        {
            if (Pointer != null) Pointer.HoverChanged -= HandleHoverChanged;
        }

        void LateUpdate()
        {
            UpdateDesktopSimulationKeys();

            if (ctx == null || Pointer == null)
            {
                HideAll();
                return;
            }

            if ((ctx.flow == null || !ctx.flow.HasStarted) && !forcePreview)
            {
                HideAll();
                return;
            }

            Stage activeStage = ctx.flow != null ? ctx.flow.CurrentStage : null;
            Mode mode = forcePreview && forcedMode != Mode.Hidden ? forcedMode : ModeFor(activeStage);
            if (mode == Mode.Hidden)
            {
                HideAll();
                return;
            }

            UpdatePose(mode);
            UpdateShape(mode);
        }

        void HandleHoverChanged(int id)
        {
            if (lastHoveredNode >= 0 && id != lastHoveredNode && Pointer != null && Pointer.GraspHeld)
                failKickUntil = Time.time + failKickDuration;
            lastHoveredNode = id;
        }

        Mode ModeFor(Stage stage)
        {
            if (stage == null) return Mode.Hidden;
            switch (stage.stageName)
            {
                case "Intro":
                case "Tutorial":
                case "S1":
                    return Mode.RayOnly;
                case "S2":
                    return Mode.WaterConduit;
                case "S3":
                    return PrecisionModeRequested() ? Mode.ProtoFinger : Mode.Tweezer;
                case "S4":
                    return Mode.GhostHand;
                case "Pick":
                    return Mode.StableHand;
                default:
                    return Mode.RayOnly;
            }
        }

        bool PrecisionModeRequested()
        {
            // Prototype rule: once the player holds Grasp in S3, show the higher-precision transitional hand.
            // A later UI choice can set this explicitly; this keeps the branch testable today.
            return SimulatedGraspHeld || (Pointer != null && Pointer.GraspHeld);
        }

        bool SimulatedGraspHeld => enableDesktopSimulation && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.gKey.isPressed;
        bool SimulatedPinchPressed => enableDesktopSimulation && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame;

        void UpdatePose(Mode mode)
        {
            Ray ray = Pointer.CurrentRay;
            float distance = float.IsInfinity(Pointer.HoverDistance) || float.IsNaN(Pointer.HoverDistance) ? defaultDistance : Pointer.HoverDistance;
            distance = Mathf.Clamp(distance, 0.08f, Mathf.Max(0.1f, defaultDistance * 2.5f));
            Vector3 hit = ray.origin + ray.direction * distance;

            float tremor = TremorFor(mode);
            if (tremor > 0f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, ray.direction).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                Vector3 up = Vector3.Cross(ray.direction, right).normalized;
                float n1 = Mathf.PerlinNoise(Time.time * 17.1f, 0.17f) - 0.5f;
                float n2 = Mathf.PerlinNoise(0.63f, Time.time * 19.7f) - 0.5f;
                hit += (right * n1 + up * n2) * tremor;
            }

            visualRoot.position = hit;
            visualRoot.rotation = Quaternion.LookRotation(ray.direction, Vector3.up);
            visualRoot.localScale = Vector3.one * visualScale;

            rayLine.enabled = true;
            rayLine.startWidth = rayLine.endWidth = mode == Mode.WaterConduit && Pointer.IsPressed ? rayWidth * 1.7f : rayWidth;
            rayLine.sharedMaterial = mode == Mode.WaterConduit ? waterMaterial : rayMaterial;
            rayLine.SetPosition(0, ray.origin);
            rayLine.SetPosition(1, hit);
        }

        void UpdateShape(Mode mode)
        {
            if (mode != currentMode)
            {
                currentMode = mode;
                ApplyModeVisibility(mode);
            }

            bool failed = Time.time < failKickUntil;
            bool pinching = SimulatedGraspHeld || Pointer.GraspHeld || Pointer.IsPressed;
            float close = SimulatedPinchPressed || Pointer.SelectPressedThisFrame ? 1f : pinching ? 0.62f : 0f;

            SetMaterial(nozzle, failed ? failMaterial : waterMaterial);
            SetMaterial(leftArm, failed ? failMaterial : MaterialFor(mode));
            SetMaterial(rightArm, failed ? failMaterial : MaterialFor(mode));
            SetMaterial(leftTip, failed ? failMaterial : MaterialFor(mode));
            SetMaterial(rightTip, failed ? failMaterial : MaterialFor(mode));
            SetMaterial(centerTip, failed ? failMaterial : MaterialFor(mode));
            SetMaterial(palm, MaterialFor(mode));

            if (mode == Mode.WaterConduit)
            {
                float pulse = Pointer.IsPressed || SimulatedGraspHeld ? 1f + 0.12f * Mathf.Sin(Time.time * 18f) : 1f;
                nozzle.localScale = new Vector3(0.035f, 0.035f, 0.055f) * pulse;
                centerTip.localScale = Vector3.one * 0.018f * (Pointer.IsPressed || SimulatedGraspHeld ? 1.35f : 1f);
                return;
            }

            float spread = Mathf.Lerp(0.036f, 0.006f, close);
            float length = mode == Mode.Tweezer ? 0.12f : 0.09f;
            float width = mode == Mode.Tweezer ? 0.008f : 0.014f;
            leftArm.localPosition = new Vector3(-spread, 0f, -length * 0.45f);
            rightArm.localPosition = new Vector3(spread, 0f, -length * 0.45f);
            leftArm.localScale = new Vector3(width, width, length);
            rightArm.localScale = new Vector3(width, width, length);
            leftArm.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-16f, -3f, close), 0f);
            rightArm.localRotation = Quaternion.Euler(0f, Mathf.Lerp(16f, 3f, close), 0f);
            leftTip.localPosition = new Vector3(-spread * 0.25f, 0f, 0.004f);
            rightTip.localPosition = new Vector3(spread * 0.25f, 0f, 0.004f);
            float tip = mode == Mode.Tweezer ? 0.012f : 0.018f;
            leftTip.localScale = Vector3.one * tip;
            rightTip.localScale = Vector3.one * tip;

            if (palm != null)
            {
                palm.localPosition = new Vector3(0f, -0.01f, -0.095f);
                palm.localScale = mode == Mode.Tweezer ? Vector3.zero : new Vector3(0.07f, 0.035f, 0.045f);
            }
        }

        float TremorFor(Mode mode)
        {
            float amount;
            switch (mode)
            {
                case Mode.Tweezer: amount = tweezerTremor; break;
                case Mode.ProtoFinger: amount = protoFingerTremor; break;
                case Mode.GhostHand: amount = ghostHandTremor; break;
                default: amount = 0f; break;
            }
            if (Pointer != null && Pointer.HoveredNode >= 0 && IsBug(Pointer.HoveredNode)) amount *= 0.35f;
            if (Time.time < failKickUntil) amount *= 3.5f;
            return amount;
        }

        bool IsBug(int id)
        {
            var node = ctx != null ? ctx.Node(id) : null;
            return node != null && node.organ == Organ.Bug;
        }

        void UpdateDesktopSimulationKeys()
        {
            if (!enableDesktopSimulation) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.hKey.wasPressedThisFrame) forcePreview = !forcePreview;
            if (keyboard.digit1Key.wasPressedThisFrame) ForcePreview(Mode.RayOnly);
            if (keyboard.digit2Key.wasPressedThisFrame) ForcePreview(Mode.WaterConduit);
            if (keyboard.digit3Key.wasPressedThisFrame) ForcePreview(Mode.Tweezer);
            if (keyboard.digit4Key.wasPressedThisFrame) ForcePreview(Mode.ProtoFinger);
            if (keyboard.digit5Key.wasPressedThisFrame) ForcePreview(Mode.GhostHand);
            if (keyboard.digit6Key.wasPressedThisFrame) ForcePreview(Mode.StableHand);
            if (keyboard.fKey.wasPressedThisFrame) failKickUntil = Time.time + failKickDuration;
        }

        void ForcePreview(Mode mode)
        {
            forcePreview = true;
            forcedMode = mode;
        }

        void BuildVisuals()
        {
            rayMaterial = NewMaterial(rayColor);
            waterMaterial = NewMaterial(waterColor);
            tweezerMaterial = NewMaterial(tweezerColor);
            protoMaterial = NewMaterial(protoFingerColor);
            ghostMaterial = NewMaterial(ghostHandColor);
            stableMaterial = NewMaterial(stableHandColor);
            failMaterial = NewMaterial(failColor);

            var lineGo = new GameObject("InteractionAvatarRay");
            lineGo.transform.SetParent(transform, false);
            rayLine = lineGo.AddComponent<LineRenderer>();
            rayLine.useWorldSpace = true;
            rayLine.positionCount = 2;
            rayLine.numCapVertices = 4;
            rayLine.sharedMaterial = rayMaterial;
            rayLine.startWidth = rayLine.endWidth = rayWidth;

            visualRoot = new GameObject("InteractionAvatarVisualRoot").transform;
            visualRoot.SetParent(transform, false);
            nozzle = Primitive("WaterNozzle", PrimitiveType.Cube, waterMaterial, visualRoot);
            palm = Primitive("Palm", PrimitiveType.Sphere, ghostMaterial, visualRoot);
            leftArm = Primitive("LeftFinger", PrimitiveType.Cube, tweezerMaterial, visualRoot);
            rightArm = Primitive("RightFinger", PrimitiveType.Cube, tweezerMaterial, visualRoot);
            leftTip = Primitive("LeftTip", PrimitiveType.Sphere, tweezerMaterial, visualRoot);
            rightTip = Primitive("RightTip", PrimitiveType.Sphere, tweezerMaterial, visualRoot);
            centerTip = Primitive("WaterTip", PrimitiveType.Sphere, waterMaterial, visualRoot);
        }

        Transform Primitive(string name, PrimitiveType type, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            SetMaterial(go.transform, material);
            return go.transform;
        }

        void ApplyModeVisibility(Mode mode)
        {
            bool water = mode == Mode.WaterConduit;
            bool hand = mode == Mode.Tweezer || mode == Mode.ProtoFinger || mode == Mode.GhostHand || mode == Mode.StableHand;
            SetActive(nozzle, water);
            SetActive(centerTip, water);
            SetActive(leftArm, hand);
            SetActive(rightArm, hand);
            SetActive(leftTip, hand);
            SetActive(rightTip, hand);
            SetActive(palm, mode == Mode.ProtoFinger || mode == Mode.GhostHand || mode == Mode.StableHand);
        }

        void HideAll()
        {
            currentMode = Mode.Hidden;
            if (rayLine != null) rayLine.enabled = false;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }

        void SetActive(Transform target, bool active)
        {
            if (target == null) return;
            target.gameObject.SetActive(active);
            if (active && visualRoot != null && !visualRoot.gameObject.activeSelf) visualRoot.gameObject.SetActive(true);
        }

        Material MaterialFor(Mode mode)
        {
            switch (mode)
            {
                case Mode.Tweezer: return tweezerMaterial;
                case Mode.ProtoFinger: return protoMaterial;
                case Mode.GhostHand: return ghostMaterial;
                case Mode.StableHand: return stableMaterial;
                default: return rayMaterial;
            }
        }

        static void SetMaterial(Transform target, Material material)
        {
            if (target == null || material == null) return;
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != material) renderer.sharedMaterial = material;
        }

        static Material NewMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallRuntimeBootstrap()
        {
            var go = new GameObject("InteractionAvatarVisualBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<RuntimeBootstrap>();
        }

        class RuntimeBootstrap : MonoBehaviour
        {
            IEnumerator Start()
            {
                for (int i = 0; i < 120; i++)
                {
                    var stageContext = FindAnyObjectByType<StageContext>(FindObjectsInactive.Include);
                    if (stageContext != null)
                    {
                        if (FindAnyObjectByType<InteractionAvatarVisual>(FindObjectsInactive.Include) == null)
                        {
                            var visual = stageContext.gameObject.AddComponent<InteractionAvatarVisual>();
                            visual.ctx = stageContext;
                        }
                        yield break;
                    }
                    yield return null;
                }
            }
        }
    }
}
