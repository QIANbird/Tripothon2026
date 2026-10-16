using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.VRHandInteraction
{
    /// <summary>Optional standalone test driver. No camera dependency and no legacy Input API.</summary>
    [DefaultExecutionOrder(-250)]
    public sealed class EditorHandSimulator : MonoBehaviour
    {
        public Transform poseSource;
        public HandTriggerInput input;
        public InputActionReference move, rotate, trigger;
        public float moveSpeed = 0.35f;
        public float rotationSpeed = 90f;
        void OnEnable()
        {
            move?.action.Enable(); rotate?.action.Enable(); trigger?.action.Enable();
            if (input != null) input.useInjectedValue = true;
        }
        void Update()
        {
            if (poseSource != null)
            {
                poseSource.position += (move != null ? move.action.ReadValue<Vector3>() : Vector3.zero) * (moveSpeed * Time.deltaTime);
                poseSource.Rotate((rotate != null ? rotate.action.ReadValue<Vector3>() : Vector3.zero) * (rotationSpeed * Time.deltaTime), Space.Self);
            }
            input?.SetInjectedValue(trigger != null ? trigger.action.ReadValue<float>() : 0);
        }
        void OnDisable()
        {
            input?.SetInjectedValue(0);
            if (input != null) input.useInjectedValue = false;
            move?.action.Disable(); rotate?.action.Disable(); trigger?.action.Disable();
        }
    }
}
