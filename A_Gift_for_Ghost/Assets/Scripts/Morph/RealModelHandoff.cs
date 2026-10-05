using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 写实形态的交接：节点飞到模型表面后，真模型从下往上溶解显现，同一高度的节点同时缩小消失；
    // 离开写实形态时反过来，模型先褪去、节点重新长出来，然后节点再开始变形。
    // 真模型取自 PlantNodeSet.sourceModel（运行时生成，不改场景和模型资产），按采样时记录的归一化参数对齐到节点。
    [RequireComponent(typeof(NodeMorpher))]
    public class RealModelHandoff : MonoBehaviour
    {
        [Tooltip("留空时自动查找 Ghost/RevealLit")]
        public Shader revealShader;
        [Tooltip("模型完全显现的时长（秒）")]
        public float revealDuration = 1.4f;
        [Tooltip("离开写实形态时模型褪去的时长（秒）")]
        public float hideDuration = 0.6f;
        [Tooltip("溶解方块的边长（米）")]
        public float noiseCellSize = 0.015f;

        // 运行时的模型实例，S4 的抓取交互从这里取部件（如 pepper_red_picked）
        public GameObject ModelInstance { get; private set; }
        public float Reveal { get; private set; }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int RevealBottomId = Shader.PropertyToID("_RevealBottom");
        static readonly int RevealHeightId = Shader.PropertyToID("_RevealHeight");
        static readonly int RevealBandId = Shader.PropertyToID("_RevealBand");
        static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");

        NodeMorpher morpher;
        Renderer[] renderers;
        readonly List<Material> materials = new List<Material>();
        bool renderersVisible = true;

        void Start()
        {
            morpher = GetComponent<NodeMorpher>();
            var set = morpher.nodeSet;
            if (set == null || set.sourceModel == null)
            {
                // 程序生成的假植物没有对应模型，写实形态就停在节点
                enabled = false;
                return;
            }
            if (revealShader == null) revealShader = Shader.Find("Ghost/RevealLit");
            if (revealShader == null)
            {
                Debug.LogError("[Morph] 找不到着色器 Ghost/RevealLit", this);
                enabled = false;
                return;
            }

            // 节点坐标 = (模型根空间坐标 - sourceOffset) * sourceScale，容器按同样的变换摆放模型
            var container = new GameObject("RealModel").transform;
            container.SetParent(transform, false);
            container.localPosition = -set.sourceOffset * set.sourceScale;
            container.localScale = Vector3.one * set.sourceScale;
            ModelInstance = Instantiate(set.sourceModel, container);
            ModelInstance.name = set.sourceModel.name;

            renderers = ModelInstance.GetComponentsInChildren<Renderer>(true);
            var replaced = new Dictionary<Material, Material>();
            foreach (var r in renderers)
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    var source = shared[i];
                    if (source == null) continue;
                    if (!replaced.TryGetValue(source, out var mat))
                    {
                        mat = CreateRevealMaterial(source);
                        replaced[source] = mat;
                        materials.Add(mat);
                    }
                    shared[i] = mat;
                }
                r.sharedMaterials = shared;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            Reveal = morpher.CurrentForm == MorphForm.Real && !morpher.IsMorphing ? 1f : 0f;
            Apply();
        }

        Material CreateRevealMaterial(Material source)
        {
            var mat = new Material(revealShader) { name = source.name + " (Reveal)" };
            if (source.HasProperty(BaseMapId))
            {
                mat.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
                mat.SetTextureScale(BaseMapId, source.GetTextureScale(BaseMapId));
                mat.SetTextureOffset(BaseMapId, source.GetTextureOffset(BaseMapId));
            }
            else if (source.mainTexture != null)
            {
                mat.SetTexture(BaseMapId, source.mainTexture);
            }
            if (source.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, source.GetColor(BaseColorId));
            // 沿用原材质的剔除设置（URP Lit 的 Render Face）
            if (source.HasProperty(CullId)) mat.SetFloat(CullId, source.GetFloat(CullId));
            return mat;
        }

        void Update()
        {
            bool show = morpher.CurrentForm == MorphForm.Real && !morpher.IsMorphing;
            float duration = show ? revealDuration : hideDuration;
            Reveal = Mathf.MoveTowards(Reveal, show ? 1f : 0f, Time.deltaTime / Mathf.Max(duration, 0.01f));
            Apply();
        }

        void Apply()
        {
            morpher.RealReveal = Reveal;

            bool visible = Reveal > 0f;
            if (visible != renderersVisible)
            {
                foreach (var r in renderers) r.enabled = visible;
                renderersVisible = visible;
            }
            if (!visible) return;

            float bottom = transform.TransformPoint(new Vector3(0f, morpher.RealMinY, 0f)).y;
            float height = (morpher.RealMaxY - morpher.RealMinY) * transform.lossyScale.y;
            foreach (var mat in materials)
            {
                mat.SetFloat(RevealId, Reveal);
                mat.SetFloat(RevealBottomId, bottom);
                mat.SetFloat(RevealHeightId, height);
                mat.SetFloat(RevealBandId, morpher.revealBand);
                mat.SetFloat(NoiseScaleId, noiseCellSize * transform.lossyScale.y);
            }
        }

        void OnDestroy()
        {
            foreach (var mat in materials)
                if (mat != null) Destroy(mat);
        }
    }
}
