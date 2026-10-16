using System;
using Ghost.Interaction.XR;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Interaction
{
    // 射线 / 鼠标指着的节点外面画一圈实体描边（类似按钮悬停），方便看清当前命中的是哪个节点。
    // 只读发起方状态（InteractionRouter.HoveredNode），PC 和 VR 共用；VR 选中的节点（放大 + 突起）也一直描边。
    // 描边按 NodeMorpher 本帧姿态画（含姿态覆盖），所以跟着放大、变形、旋转走。只影响显示。
    // 描边目标就是 HoveredNode（阶段的命中修正在 NodePicker.Resolver 里，和点击一致）；阶段可以自己画描边（S4：画在虫子模型上）。
    [DefaultExecutionOrder(200)] // 在发起方、NodeMorpher、BugModelInstances 的 LateUpdate 之后画，贴本帧模型姿态
    public class NodeHoverOutline : MonoBehaviour
    {
        public InteractionSource source;
        public NodeMorpher morpher;
        [Tooltip("Ghost/MorphOutline 材质（描边颜色在材质上，默认 #E5FF71）")]
        public Material material;
        [Tooltip("描边厚度（米，世界空间，每边）")]
        public float thickness = 0.004f;
        [Tooltip("模型描边（沿法线外扩）的厚度（米，世界空间），S4 虫子模型用")]
        public float modelThickness = 0.002f;

        // 阶段自己画描边：返回 true 表示已经画过，不再画节点描边
        public Func<int, bool> CustomDraw { get; set; }

        // 本帧描边的悬停目标（-1 = 没有），阶段可以读
        public int OutlinedNode { get; private set; } = -1;

        Material modelMaterial;
        static readonly int ExtrudeId = Shader.PropertyToID("_Extrude");

        // 沿法线外扩的描边材质（material 的运行时副本），给模型描边用
        public Material ModelMaterial
        {
            get
            {
                if (material == null) return null;
                if (modelMaterial == null) modelMaterial = new Material(material) { name = material.name + " (Model)" };
                modelMaterial.SetFloat(ExtrudeId, modelThickness);
                return modelMaterial;
            }
        }

        public void ClearOverrides()
        {
            CustomDraw = null;
        }

        void LateUpdate()
        {
            OutlinedNode = -1;
            if (source == null || morpher == null || material == null || !morpher.IsReady) return;

            int hovered = source.HoveredNode;
            OutlinedNode = hovered;
            Draw(hovered);

            int selected = SelectedNode();
            if (selected != hovered) Draw(selected);
        }

        void Draw(int id)
        {
            if (id < 0) return;
            if (CustomDraw != null && CustomDraw(id)) return;
            morpher.DrawOutline(id, material, thickness);
        }

        int SelectedNode()
        {
            var xr = source is InteractionRouter router ? router.Active as XRControllerInput : source as XRControllerInput;
            return xr != null && xr.InputEnabled ? xr.SelectedNode : -1;
        }

        void OnDestroy()
        {
            if (modelMaterial != null) Destroy(modelMaterial);
        }
    }
}
