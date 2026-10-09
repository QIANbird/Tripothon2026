using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Ghost.Pick
{
    // 写实模型里可以摘的果实（Pick 阶段）。运行时由 PickStage 挂到 RealModelHandoff.ModelInstance 里的果实网格上。
    // 负责：Collider + XRI Interactable（被射线悬停时高亮，被选中时发 PickRequested）、从植株上脱离挂到手上、离开阶段时还原。
    // 自己不做射线检测，不读输入：交互发起方是 XRRayInteractor（PC：相机中心射线；VR 以后换成手柄 Interactor）。
    public class PickableFruit : MonoBehaviour
    {
        [Tooltip("高亮时乘到 _BaseColor 上的颜色")]
        public Color highlightTint = new Color(1.6f, 1.45f, 1.15f, 1f);
        [Tooltip("碰撞球半径 = 网格包围盒最大半边长 × 这个倍数（略大一点，好瞄）")]
        public float colliderPadding = 1.1f;

        public XRSimpleInteractable Interactable { get; private set; }
        // 网格中心的世界坐标（摘的时候手伸向这里）
        public Vector3 WorldCenter => transform.TransformPoint(meshCenter);
        // 已经从植株上摘下（挂在手上）
        public bool IsDetached { get; private set; }
        public bool IsPickable { get; private set; }
        public bool IsHighlighted { get; private set; }

        // 交互发起方选中了果实（PC：对准后按左键）
        public event Action<PickableFruit> PickRequested;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int RevealBottomId = Shader.PropertyToID("_RevealBottom");
        static readonly int RevealHeightId = Shader.PropertyToID("_RevealHeight");

        Renderer rend;
        Material homeMaterial, heldMaterial;
        SphereCollider sphere;
        MaterialPropertyBlock block;
        Vector3 meshCenter;
        Transform homeParent;
        Vector3 homePosition, homeScale, heldScale;
        Quaternion homeRotation;
        bool initialized;

        // 找到果实网格后调用一次。记下在植株上的姿态（Restore 用），加 Collider 和 Interactable（默认不可摘）
        public void Init(XRInteractionManager manager)
        {
            if (initialized) return;
            initialized = true;
            rend = GetComponent<Renderer>();
            var filter = GetComponent<MeshFilter>();
            Bounds bounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.2f);
            meshCenter = bounds.center;

            homeParent = transform.parent;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;

            sphere = gameObject.AddComponent<SphereCollider>();
            sphere.center = bounds.center;
            sphere.radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * colliderPadding;
            sphere.enabled = false;

            // 先停用再填 colliders：Interactable 在 OnEnable 时按 colliders 注册到 Interaction Manager
            Interactable = gameObject.AddComponent<XRSimpleInteractable>();
            Interactable.enabled = false;
            Interactable.interactionManager = manager;
            Interactable.colliders.Clear();
            Interactable.colliders.Add(sphere);
            Interactable.firstHoverEntered.AddListener(_ => SetHighlight(true));
            Interactable.lastHoverExited.AddListener(_ => SetHighlight(false));
            Interactable.firstSelectEntered.AddListener(_ => PickRequested?.Invoke(this));
        }

        // 打开 / 关闭可摘（Collider + Interactable）。已摘下时始终关闭
        public void SetPickable(bool on)
        {
            if (!initialized) return;
            IsPickable = on && !IsDetached;
            sphere.enabled = IsPickable;
            Interactable.enabled = IsPickable;
            if (!IsPickable) SetHighlight(false);
        }

        public void SetHighlight(bool on)
        {
            if (rend == null || IsHighlighted == on) return;
            IsHighlighted = on;
            if (!on)
            {
                rend.SetPropertyBlock(null);
                return;
            }
            var mat = rend.sharedMaterial;
            Color baseColor = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            if (block == null) block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseColor * highlightTint);
            rend.SetPropertyBlock(block);
        }

        // 从植株上脱离，挂到 holder 下（保持世界姿态，之后由手把它移到 HeldLocalPosition）
        public void Detach(Transform holder)
        {
            SetPickable(false);
            IsDetached = true;
            transform.SetParent(holder, true);
            heldScale = transform.localScale;
            UseHeldMaterial();
        }

        // 植株共用的 RevealLit 材质按植株高度溶解（RealModelHandoff 每帧写参数），果实举到嘴边会高出植株被裁掉。
        // 摘下后换成自己的一份拷贝，把溶解范围放大到不会裁掉；Restore 时换回共用材质
        void UseHeldMaterial()
        {
            if (rend == null || rend.sharedMaterial == null) return;
            homeMaterial = rend.sharedMaterial;
            if (heldMaterial == null) heldMaterial = new Material(homeMaterial) { name = homeMaterial.name + " (Held)" };
            else heldMaterial.CopyPropertiesFromMaterial(homeMaterial);
            heldMaterial.SetFloat(RevealId, 1f);
            heldMaterial.SetFloat(RevealBottomId, -1000f);
            heldMaterial.SetFloat(RevealHeightId, 100000f);
            rend.sharedMaterial = heldMaterial;
        }

        // 挂在手上时，网格中心对齐到 holder 原点的本地位置（随缩放变化）
        public Vector3 HeldLocalPosition => -(transform.localRotation * Vector3.Scale(transform.localScale, meshCenter));

        // 挂在手上时的缩放（1 = 摘下时的大小）。咬一口时用
        public void SetScaleFactor(float factor)
        {
            if (!IsDetached) return;
            transform.localScale = heldScale * Mathf.Max(0f, factor);
        }

        // 回到植株上原来的位置和大小，重新显示，清除高亮，不可摘
        public void Restore()
        {
            if (!initialized) return;
            if (homeParent != null) transform.SetParent(homeParent, false);
            transform.localPosition = homePosition;
            transform.localRotation = homeRotation;
            transform.localScale = homeScale;
            if (rend != null && homeMaterial != null) rend.sharedMaterial = homeMaterial;
            IsDetached = false;
            gameObject.SetActive(true);
            SetHighlight(false);
            SetPickable(false);
        }

        void OnDestroy()
        {
            if (heldMaterial != null) Destroy(heldMaterial);
        }
    }
}
