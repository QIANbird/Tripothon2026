namespace Ghost.Interaction
{
    // 普通场景物体（带 Collider）的可交互接口，比如 AI 询问框的 Yes 按钮。
    // 由交互发起方（PC：PointerInput 鼠标射线；以后 VR：手柄射线）调用，物体本身不做射线检测。
    // 实现脚本挂在带 Collider 的物体或它的父物体上。
    public interface IInteractable
    {
        // 点击（按下和松开都在这个物体上，且移动没超过阈值）
        void OnTap();

        // 指针移入 / 移出。只用于高亮，不能作为唯一的信息展示方式（AGENTS.md 第 5 节）
        void OnHoverEnter();
        void OnHoverExit();
    }
}
