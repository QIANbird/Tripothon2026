using Ghost.Core;
using UnityEngine;

namespace Ghost.Interaction.XR
{
    // VR 下结束界面的彩椒：不走 RenderTexture，把彩椒本身挂到结束界面面板上（anchor = PC 上显示 RT 的那块 RawImage），
    // 在它中心前方 forward 米、大小 = anchor 高度 × fill，跟面板一起 LazyFollow。PC 上还原到原处，由 EndPepperShowcase 用 RT 显示。
    // 由 XRWorldCanvas.worldModeChanged 调 SetWorld（XRUIBuilder 构建时接好）。
    // 排序：World Space 的 UI 和彩椒（Overlay 队列）都在透明物体阶段画，Unity 先按 sortingOrder 再按队列排，
    // 彩椒的 sortingOrder 必须高于结束界面和它背后的黑幕（XRScreenBackdrop），否则先画彩椒、再被头前 0.1 m 的黑幕盖掉。
    public class XREndPepperPlacement : MonoBehaviour
    {
        public EndPepperShowcase showcase;
        [Tooltip("PC 上显示彩椒 RT 的 RawImage，VR 下彩椒摆在它前面")]
        public RectTransform anchor;
        [Tooltip("彩椒在面板前方多远（米）")]
        public float forward = 0.1f;
        [Tooltip("彩椒最长边占 anchor 高度的比例")]
        public float fill = 0.7f;
        [Tooltip("彩椒的 sortingOrder 比所在 Canvas 高多少")]
        public int sortingOffset = 10;

        Transform pepper;
        Transform savedParent;
        Vector3 savedPosition, savedScale;
        Quaternion savedRotation;
        int savedLayer;
        Renderer[] renderers;
        int[] savedOrders;
        bool placed;

        public void SetWorld(bool world)
        {
            if (showcase == null || showcase.pepper == null || anchor == null) return;
            pepper = showcase.pepper.transform;
            if (world && !placed)
            {
                savedParent = pepper.parent;
                savedPosition = pepper.localPosition;
                savedRotation = pepper.localRotation;
                savedScale = pepper.localScale;
                savedLayer = showcase.pepper.layer;

                // anchor 的本地单位是 Canvas 像素；Canvas 正面朝 -forward，往玩家方向 = 本地 -z
                float metersPerPixel = Mathf.Max(anchor.lossyScale.y, 1e-6f);
                pepper.SetParent(anchor, false);
                pepper.localPosition = new Vector3(anchor.rect.center.x, anchor.rect.center.y, -forward / metersPerPixel);
                pepper.localRotation = Quaternion.identity;
                pepper.localScale = Vector3.one * (anchor.rect.height * fill);
                SetLayer(pepper, 0); // 主相机看得到的 Default 层
                var canvas = anchor.GetComponentInParent<Canvas>(true);
                int order = (canvas != null ? canvas.sortingOrder : 0) + sortingOffset;
                renderers = pepper.GetComponentsInChildren<Renderer>(true);
                savedOrders = new int[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    savedOrders[i] = renderers[i].sortingOrder;
                    renderers[i].sortingOrder = order;
                }
                placed = true;
            }
            else if (!world && placed)
            {
                pepper.SetParent(savedParent, false);
                pepper.localPosition = savedPosition;
                pepper.localRotation = savedRotation;
                pepper.localScale = savedScale;
                SetLayer(pepper, savedLayer);
                for (int i = 0; renderers != null && i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].sortingOrder = savedOrders[i];
                placed = false;
            }
            showcase.SetWorldMode(world);
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }
    }
}
