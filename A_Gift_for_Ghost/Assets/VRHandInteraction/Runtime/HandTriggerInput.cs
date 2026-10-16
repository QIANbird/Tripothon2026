using UnityEngine;
using UnityEngine.InputSystem;

namespace Ghost.VRHandInteraction
{
    /// <summary>Only this adapter knows about Input Actions. External injection also supports tests/other rigs.</summary>
    public sealed class HandTriggerInput : MonoBehaviour
    {
        public InputActionReference triggerAction;
        [Range(0, 1)] public float pressThreshold = 0.5f;
        [Tooltip("Enable only for simulation or an upstream input adapter.")]
        public bool useInjectedValue;
        [Range(0, 1)] [SerializeField] float injectedValue;
        InputAction ownedAction;
        public float Value => !isActiveAndEnabled ? 0 : Mathf.Clamp01(useInjectedValue ? injectedValue :
            triggerAction != null && triggerAction.action.enabled ? triggerAction.action.ReadValue<float>() : 0);
        public bool Pressed => Value >= pressThreshold;
        public void SetInjectedValue(float value) => injectedValue = Mathf.Clamp01(value);
        void OnEnable()
        {
            if (triggerAction != null && !triggerAction.action.enabled)
            {
                ownedAction = triggerAction.action;
                ownedAction.Enable();
            }
        }
        void OnDisable()
        {
            ownedAction?.Disable();
            ownedAction = null;
            injectedValue = 0;
        }
    }
}
