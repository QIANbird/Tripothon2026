using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Ghost.VRHandInteraction
{
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class TimedDirectInteractor : XRDirectInteractor
    {
        public HandPoseFollower poseFollower;
        public HandTriggerInput triggerInput;
        [Min(0)] public float minimumHoldDuration = 0.3f;
        [Min(0.001f)] public float grabRadius = 0.08f;
        public new InteractionLayerMask interactionLayerMask = 1;
        [Tooltip("Physics layers eligible for contact; independent of XRI interaction layers.")]
        public LayerMask contactPhysicsLayers = 1;
        readonly HoldAuthorization hold = new HoldAuthorization();
        readonly List<IXRInteractable> targets = new List<IXRInteractable>();
        SphereCollider sphere;
        bool Ready => isActiveAndEnabled && allowSelect && poseFollower != null && poseFollower.IsTracking && triggerInput != null && triggerInput.Pressed;
        public bool IsAuthorized => hold.Authorized;
        public override bool isSelectActive => Ready && (hasSelection || hold.Authorized);

        protected override void Awake()
        {
            sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = grabRadius;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            interactionLayers = interactionLayerMask;
            keepSelectedTargetValid = true;
            selectActionTrigger = InputTriggerType.State;
            base.Awake();
        }

        public override void PreprocessInteractor(XRInteractionUpdateOrder.UpdatePhase phase)
        {
            poseFollower?.SyncPose();
            sphere.radius = Mathf.Max(0.001f, grabRadius);
            interactionLayers = interactionLayerMask;
            base.PreprocessInteractor(phase);
            if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            if (!Ready) { hold.Reset(); return; }
            if (hasSelection) return;
            targets.Clear();
            GetValidTargets(targets);
            IXRSelectInteractable candidate = null;
            // Keep the current candidate while its actual collider remains in range.
            if (hold.Candidate is IXRSelectInteractable previous && Eligible(previous) && targets.Contains(previous))
                candidate = previous;
            else
            {
                float nearest = float.PositiveInfinity;
                foreach (var target in targets)
                    if (target is IXRSelectInteractable selectable && Eligible(selectable))
                    {
                        float distance = (target.transform.position - transform.position).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; candidate = selectable; }
                    }
            }
            hold.Tick(candidate, true, Time.unscaledTimeAsDouble, minimumHoldDuration);
        }

        bool Eligible(IXRSelectInteractable target)
        {
            if (target == null || (target is Object unityObject && unityObject == null) || target.transform == null || !target.transform.gameObject.activeInHierarchy || target.isSelected)
                return false;
            if ((target.interactionLayers.value & interactionLayerMask.value) == 0 || !base.CanSelect(target)) return false;
            // XRI trigger contacts can lag behind a fast pose change by a physics tick.
            // Recheck current geometry before starting/continuing authorization (also handles multi-collider objects).
            foreach (var collider in target.colliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                    (contactPhysicsLayers.value & (1 << collider.gameObject.layer)) != 0 &&
                    !Physics.GetIgnoreLayerCollision(gameObject.layer, collider.gameObject.layer) &&
                    !Physics.GetIgnoreCollision(sphere, collider) &&
                    Physics.ComputePenetration(sphere, transform.position, transform.rotation,
                        collider, collider.transform.position, collider.transform.rotation, out _, out _)) return true;
            return false;
        }

        public override bool CanSelect(IXRSelectInteractable target) =>
            Ready && base.CanSelect(target) && (IsSelecting(target) ||
                (hold.Authorized && ReferenceEquals(hold.Candidate, target) && Eligible(target)));

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            hold.Reset();
            base.OnSelectExited(args);
        }
        protected override void OnDisable()
        {
            hold.Reset();
            base.OnDisable(); // XRI unregister cancels selection and restores the object's Rigidbody.
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, grabRadius * Mathf.Abs(transform.lossyScale.x));
        }
    }
}
