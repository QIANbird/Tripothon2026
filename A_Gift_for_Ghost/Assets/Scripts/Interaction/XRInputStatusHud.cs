using System.Collections.Generic;
using Ghost.Stages;
using UnityEngine;

namespace Ghost.Interaction
{
    // Runtime diagnostic overlay for Quest Link / desktop fallback testing.
    // It does not drive gameplay; it only reports whether Unity can see the right XR controller.
    [DefaultExecutionOrder(95)]
    public class XRInputStatusHud : MonoBehaviour
    {
        static readonly List<UnityEngine.XR.InputDevice> Devices = new List<UnityEngine.XR.InputDevice>(4);

        public StageContext ctx;
        public bool showHud = true;
        public Vector2 screenOffset = new Vector2(12f, 12f);

        PointerInput Pointer => ctx != null ? ctx.pointer : null;

        void Awake()
        {
            if (ctx == null) ctx = FindAnyObjectByType<StageContext>(FindObjectsInactive.Include);
        }

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) showHud = !showHud;
        }

        void OnGUI()
        {
            if (!showHud) return;

            var pointer = Pointer != null ? Pointer : FindAnyObjectByType<PointerInput>(FindObjectsInactive.Include);
            ReadRightHand(out bool valid, out bool hasPose, out float trigger, out float grip);

            float width = 330f;
            float height = 146f;
            var rect = new Rect(screenOffset.x, screenOffset.y, width, height);
            GUI.Box(rect, "Quest / VR Input");

            float y = rect.y + 24f;
            DrawLine(rect.x + 10f, ref y, "Right controller: " + (valid ? "seen" : "not seen"));
            DrawLine(rect.x + 10f, ref y, "Pose: " + (hasPose ? "ready" : "missing") + "    XR ray: " + (pointer != null && pointer.XRRayActive ? "active" : "mouse fallback"));
            DrawLine(rect.x + 10f, ref y, "Trigger: " + trigger.ToString("0.00") + "    Grip: " + grip.ToString("0.00"));
            DrawLine(rect.x + 10f, ref y, "Select: " + (pointer != null && pointer.IsPressed ? "held" : "up") + "    Grasp: " + (pointer != null && pointer.GraspHeld ? "held" : "up"));
            DrawLine(rect.x + 10f, ref y, "Hover node: " + (pointer != null ? pointer.HoveredNode.ToString() : "-"));
            DrawLine(rect.x + 10f, ref y, "F8 hide/show. H + 1-6 preview hand modes.");
        }

        static void DrawLine(float x, ref float y, string text)
        {
            GUI.Label(new Rect(x, y, 310f, 18f), text);
            y += 19f;
        }

        static void ReadRightHand(out bool valid, out bool hasPose, out float trigger, out float grip)
        {
            valid = false;
            hasPose = false;
            trigger = 0f;
            grip = 0f;

            Devices.Clear();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, Devices);
            for (int i = 0; i < Devices.Count; i++)
            {
                var device = Devices[i];
                if (!device.isValid) continue;

                valid = true;
                bool hasPosition = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out _);
                bool hasRotation = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out _);
                hasPose = hasPosition && hasRotation;
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out trigger);
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out grip);
                return;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallRuntimeBootstrap()
        {
            var go = new GameObject("XRInputStatusHudBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<RuntimeBootstrap>();
        }

        class RuntimeBootstrap : MonoBehaviour
        {
            System.Collections.IEnumerator Start()
            {
                for (int i = 0; i < 120; i++)
                {
                    var stageContext = FindAnyObjectByType<StageContext>(FindObjectsInactive.Include);
                    if (stageContext != null)
                    {
                        if (FindAnyObjectByType<XRInputStatusHud>(FindObjectsInactive.Include) == null)
                        {
                            var hud = stageContext.gameObject.AddComponent<XRInputStatusHud>();
                            hud.ctx = stageContext;
                        }
                        yield break;
                    }
                    yield return null;
                }
            }
        }
    }
}
