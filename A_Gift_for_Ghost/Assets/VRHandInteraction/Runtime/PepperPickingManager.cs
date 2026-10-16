using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Ghost.VRHandInteraction
{
    public sealed class PepperPickingManager : MonoBehaviour
    {
        public Transform treeRoot;
        public XRInteractionManager interactionManager;
        public List<Transform> explicitPeppers = new List<Transform>();
        public InteractionLayerMask interactionLayerMask = 1;
        public bool autoScan = true;
        public bool makeScannedObjectsPickable = true;
        public TimedDirectInteractor handInteractor;
        [Tooltip("Message shown when Trigger is held over an unripe pepper.")]
        public string unripeMessage = "你确定要采摘未成熟的果实吗？再看看";
        public string CurrentMessage { get; private set; }
        public IReadOnlyList<VRPepperPickable> Peppers => peppers;
        public int PickedPepperCount { get; private set; }
        public int TotalPepperCount => peppers.Count;
        public VRPepperPickable CurrentHeldPepper { get; private set; }
        public event Action<GameObject> OnGrabStarted, OnFruitPicked, OnReleased;
        readonly List<VRPepperPickable> peppers = new List<VRPepperPickable>();
        readonly HashSet<Transform> registered = new HashSet<Transform>();
        public void RegisterAll()
        {
            if (interactionManager == null) interactionManager = FindFirstObjectByType<XRInteractionManager>();
            var list = explicitPeppers.Count > 0 ? explicitPeppers : FindCandidates();
            foreach (var t in list) Register(t);
        }
        List<Transform> FindCandidates()
        {
            var result = new List<Transform>(); if (treeRoot == null) return result;
            foreach (var t in treeRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == treeRoot || registered.Contains(t)) continue;
                string n = t.name.ToLowerInvariant();
                if (n == "pepper_red_picked" || (n.Contains("pepper") && (t.GetComponent<MeshRenderer>() != null || t.GetComponent<SkinnedMeshRenderer>() != null))) result.Add(t);
            }
            return result;
        }
        public VRPepperPickable Register(Transform target)
        {
            if (target == null || registered.Contains(target)) return target != null ? target.GetComponent<VRPepperPickable>() : null;
            var p = target.GetComponent<VRPepperPickable>() ?? target.gameObject.AddComponent<VRPepperPickable>();
            p.Configure(interactionManager, interactionLayerMask);
            p.SetHarvestable(target.name.Equals("pepper_red_picked", StringComparison.OrdinalIgnoreCase) || target.name.EndsWith("_picked", StringComparison.OrdinalIgnoreCase));
            p.OnGrabStarted += Grabbed; p.OnFruitPicked += Picked; p.OnReleased += Released;
            registered.Add(target); peppers.Add(p); return p;
        }
        void Grabbed(GameObject go) { CurrentHeldPepper = go.GetComponent<VRPepperPickable>(); OnGrabStarted?.Invoke(go); }
        void Picked(GameObject go) { PickedPepperCount++; OnFruitPicked?.Invoke(go); }
        void Released(GameObject go) { if (CurrentHeldPepper != null && CurrentHeldPepper.gameObject == go) CurrentHeldPepper = null; OnReleased?.Invoke(go); }
        void Start()
        {
            // Runtime lists are intentionally not serialized; rebuild them from the scene references.
            if (peppers.Count == 0) RegisterAll();
        }
        void Update()
        {
            CurrentMessage = null;
            if (handInteractor == null || handInteractor.triggerInput == null || !handInteractor.triggerInput.Pressed) return;
            foreach (var p in peppers)
            {
                if (p == null || p.IsHarvestable || !p.IsAttached) continue;
                var c = p.GetComponent<Collider>();
                if (c != null && Vector3.Distance(handInteractor.transform.position, c.ClosestPoint(handInteractor.transform.position)) <= handInteractor.grabRadius)
                { CurrentMessage = unripeMessage; break; }
            }
        }
        void OnDestroy() { foreach (var p in peppers) { p.OnGrabStarted -= Grabbed; p.OnFruitPicked -= Picked; p.OnReleased -= Released; } }
    }
}
