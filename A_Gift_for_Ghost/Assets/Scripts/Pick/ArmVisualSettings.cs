using UnityEngine;

namespace Ghost.Pick
{
    // 手臂模型（前臂 + 手）的摆放微调。场景是代码生成的（MainSceneMenu.BuildArm），直接在场景里改会被覆盖，
    // 所以可调参数放在这个资产里（Assets/Data/Pick/ArmVisualSettings.asset，由 Build Main Scene 生成）：
    // 编辑模式和 Play 模式下改数值都立刻生效（ArmVisualPlacement / HandReach 每帧读取）。
    // 默认值是 10-09 手动调好的右手姿态：大拇指朝左、手背朝右上
    public class ArmVisualSettings : ScriptableObject
    {
        [Header("手模型")]
        [Tooltip("旋转（度），以肘部为轴心。z = 绕前臂轴滚转（改大拇指朝向），x = 上下翻，y = 左右摆")]
        public Vector3 rotationEuler = new Vector3(-17.11f, 17.8f, 202.9f);
        [Tooltip("平移（米），在肘部坐标系里：z 沿前臂朝手指方向，x / y 垂直于前臂")]
        public Vector3 positionOffset = new Vector3(-0.04f, -0.01f, 0.14f);
        [Tooltip("整体缩放倍数（1 = 自动对齐后的原始大小）")]
        public float scale = 1f;

        [Header("手掌和果实")]
        [Tooltip("果实中心离掌心多远（米），沿掌心朝外的方向。果实半径约 0.1 m")]
        public float palmToFruit = 0.08f;
        [Tooltip("掌心位置微调（米），在手掌坐标系里：x 大拇指一侧，y 掌心朝外，z 指尖方向")]
        public Vector3 palmAdjust = Vector3.zero;

        [Header("姿态：果实中心相对眼睛的位置（米：x 右、y 上、z 前）")]
        public Vector3 idlePalm = new Vector3(0.19f, -0.24f, 0.45f);
        [Tooltip("嘴边：果实在画面下方、离嘴很近")]
        public Vector3 mouthPalm = new Vector3(0.02f, -0.13f, 0.22f);
        [Tooltip("举到嘴边时自动把掌心转到朝上（略朝向嘴）。这里是在自动角度上再加的微调（度）")]
        public float mouthRoll = 0f;
        [Tooltip("嘴边掌心朝向：0 = 正上方，1 = 朝向眼睛")]
        [Range(0f, 1f)] public float mouthFaceTowardEye = 0.3f;
    }
}
