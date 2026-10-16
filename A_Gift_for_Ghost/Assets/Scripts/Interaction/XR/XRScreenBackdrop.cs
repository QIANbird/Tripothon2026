using UnityEngine;
using UnityEngine.XR;

namespace Ghost.Interaction.XR
{
    // VR 下开始 / 结束界面（以及暂停界面的半透明幕布）背后的黑幕：PC 上界面本身就是全屏黑底，VR 里界面只是头前一块面板，四周会露出关卡。
    // 这块黑幕挂在相机前（XRWorldCanvas HeadLocked，和黑屏一样），透明度跟着界面的 CanvasGroup 走：
    // 界面淡入它就淡入，界面淡出它也一起淡出。排序比界面低一档，界面画在它上面。PC 上一直透明。
    public class XRScreenBackdrop : MonoBehaviour
    {
        [Tooltip("开始 / 结束界面的 CanvasGroup")]
        public CanvasGroup source;
        [Tooltip("黑幕自己的 CanvasGroup")]
        public CanvasGroup group;

        void LateUpdate()
        {
            if (group == null) return;
            group.alpha = XRSettings.isDeviceActive && source != null ? source.alpha : 0f;
        }
    }
}
