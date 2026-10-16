using UnityEngine;

namespace Ghost.VRHandInteraction
{
    /// <summary>Animator-only adapter. No selection or physics side effects.</summary>
    public sealed class HandGraspVisual : MonoBehaviour
    {
        public HandTriggerInput input;
        public Animator animator;
        public float handAnimationSpeed = 8f;
        public string[] fingerParameters = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        public Transform[] fallbackFingers;
        float amount;
        public float Amount => amount;
        void Update()
        {
            amount = Mathf.MoveTowards(amount, input != null ? input.Value : 0, Mathf.Max(0, handAnimationSpeed) * Time.deltaTime);
            if (animator != null)
                foreach (var parameter in fingerParameters) animator.SetFloat(parameter, amount);
            if (fallbackFingers != null)
                foreach (var finger in fallbackFingers)
                    if (finger != null) finger.localRotation = Quaternion.Euler(amount * 85, 0, 0);
        }
    }
}
