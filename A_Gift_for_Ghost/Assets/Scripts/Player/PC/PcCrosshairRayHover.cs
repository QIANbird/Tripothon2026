using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Ghost.Player.PC
{
    // PC 专用：屏幕中心的 XRI 射线悬停到 Interactable（在伸手范围内的果实）时，准星变成"可摘"状态。
    // VR 版没有准星，由手柄射线的 Line Visual 表现，这个组件不用。
    public class PcCrosshairRayHover : MonoBehaviour
    {
        public XRRayInteractor ray;
        public PcCrosshair crosshair;

        void Update()
        {
            if (crosshair == null) return;
            bool hover = ray != null && ray.isActiveAndEnabled && ray.hasHover;
            if (crosshair.Highlighted != hover) crosshair.Highlighted = hover;
        }

        void OnDisable()
        {
            if (crosshair != null) crosshair.Highlighted = false;
        }
    }
}
