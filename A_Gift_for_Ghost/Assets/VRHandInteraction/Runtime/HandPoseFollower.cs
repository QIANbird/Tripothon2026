using UnityEngine;

namespace Ghost.VRHandInteraction
{
    [DefaultExecutionOrder(-200)]
    public sealed class HandPoseFollower : MonoBehaviour
    {
        public Transform controllerPoseSource;
        public Transform handVisualTransform;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public bool trackingEnabled = true;
        [Header("Physical hand boundary")]
        [Tooltip("Prevents the hand from moving through non-trigger colliders in these Physics layers.")]
        public bool preventPenetration = true;
        [Min(0.001f)] public float collisionRadius = 0.045f;
        public LayerMask collisionLayers = 1;
        [Min(1)] public int collisionIterations = 2;
        [SerializeField] bool trackingValid = true;
        public bool IsTracking => isActiveAndEnabled && trackingEnabled && trackingValid && controllerPoseSource != null && controllerPoseSource.gameObject.activeInHierarchy;
        public void SetTrackingValid(bool valid) => trackingValid = valid;
        public void SetPoseSource(Transform source) => controllerPoseSource = source;

        public static Pose MapPose(Transform source, Vector3 position, Vector3 rotation) =>
            new Pose(source.position + source.rotation * position, source.rotation * Quaternion.Euler(rotation));

        public void SyncPose()
        {
            if (!IsTracking) return;
            var pose = MapPose(controllerPoseSource, positionOffset, rotationOffset);
            var position = ResolveCollision(transform.position, pose.position);
            transform.SetPositionAndRotation(position, pose.rotation);
        }


        Vector3 ResolveCollision(Vector3 current, Vector3 desired)
        {
            if (!preventPenetration || collisionLayers.value == 0 || collisionRadius <= 0 ||
                (desired - current).sqrMagnitude < 1e-10f) return desired;
            var position = current;
            for (int i = 0; i < collisionIterations; i++)
            {
                var delta = desired - position;
                var distance = delta.magnitude;
                if (distance < 1e-5f) break;
                if (Physics.SphereCast(position, collisionRadius, delta / distance, out var hit,
                        distance, collisionLayers, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point - delta.normalized * collisionRadius;
                    break;
                }
                position = desired;
            }
            return position;
        }

        void Update() => SyncPose();
        void OnEnable() => Application.onBeforeRender += RefreshVisual;
        void OnDisable() => Application.onBeforeRender -= RefreshVisual;
        void RefreshVisual()
        {
            // Keep render-only refresh separate from the physics/interactor root.
            if (!IsTracking || handVisualTransform == null || handVisualTransform == transform) return;
            var pose = MapPose(controllerPoseSource, positionOffset, rotationOffset);
            handVisualTransform.SetPositionAndRotation(pose.position, pose.rotation);
        }
        void LateUpdate()
        {
            if (handVisualTransform != null && handVisualTransform != transform)
                handVisualTransform.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }
}
