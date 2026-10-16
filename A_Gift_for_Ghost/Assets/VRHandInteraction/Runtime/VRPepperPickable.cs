using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Ghost.VRHandInteraction
{
    public enum PepperPickState { Attached, Held, Released }

    [DisallowMultipleComponent]
    public sealed class VRPepperPickable : MonoBehaviour
    {
        public PepperPickState State { get; private set; } = PepperPickState.Attached;
        public bool IsAttached => State == PepperPickState.Attached;
        public bool IsHeld => State == PepperPickState.Held;
        public bool IsReleased => State == PepperPickState.Released;
        public XRGrabInteractable Interactable { get; private set; }
        public Rigidbody Body { get; private set; }
        public event Action<GameObject> OnGrabStarted;
        public event Action<GameObject> OnFruitPicked;
        public event Action<GameObject> OnReleased;
        public bool IsHarvestable { get; private set; }
        public void SetHarvestable(bool value) { IsHarvestable = value; if (Interactable != null) Interactable.enabled = value && State == PepperPickState.Attached; }
        Transform homeParent; Vector3 homePosition, homeScale; Quaternion homeRotation;
        bool initialized, pickedEventSent;
        bool originalKinematic, originalGravity;

        public void Configure(XRInteractionManager manager, InteractionLayerMask layers = default)
        {
            if (initialized) return;
            initialized = true;
            homeParent = transform.parent; homePosition = transform.localPosition;
            homeRotation = transform.localRotation; homeScale = transform.localScale;
            var collider = GetComponent<Collider>();
            if (collider == null) { var box = gameObject.AddComponent<BoxCollider>(); box.size = Vector3.one * 0.7f; collider = box; }
            Body = GetComponent<Rigidbody>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
            originalKinematic = Body.isKinematic; originalGravity = Body.useGravity;
            Body.isKinematic = true; Body.useGravity = false;
            Interactable = GetComponent<XRGrabInteractable>();
            if (Interactable == null) { Interactable = gameObject.AddComponent<XRGrabInteractable>(); Interactable.enabled = false; }
            Interactable.interactionManager = manager;
            if (layers.value != 0) Interactable.interactionLayers = layers;
            Interactable.selectMode = InteractableSelectMode.Single;
            Interactable.movementType = XRBaseInteractable.MovementType.Kinematic;
            Interactable.useDynamicAttach = true;
            Interactable.throwOnDetach = false;
            Interactable.enabled = IsHarvestable;
            Interactable.selectEntered.AddListener(HandleSelectEntered);
            Interactable.selectExited.AddListener(HandleSelectExited);
        }

        void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (!IsAttached || !IsHarvestable) return;
            State = PepperPickState.Held;
            transform.SetParent(null, true);
            OnGrabStarted?.Invoke(gameObject);
            if (!pickedEventSent) { pickedEventSent = true; OnFruitPicked?.Invoke(gameObject); }
        }
        void HandleSelectExited(SelectExitEventArgs args)
        {
            if (!IsHeld) return;
            State = PepperPickState.Released;
            Body.isKinematic = originalKinematic; Body.useGravity = originalGravity;
            OnReleased?.Invoke(gameObject);
        }
        public void ForceRelease()
        {
            if (Interactable != null && Interactable.isSelected)
                Interactable.interactionManager.SelectExit(Interactable.firstInteractorSelecting, Interactable);
            else if (IsHeld) HandleSelectExited(null);
        }
        void OnDestroy()
        {
            if (Interactable != null) { Interactable.selectEntered.RemoveListener(HandleSelectEntered); Interactable.selectExited.RemoveListener(HandleSelectExited); }
        }
    }
}
