using UnityEngine;

namespace Ghost.Pick
{
    // 挂在肘部下的 ForearmVisual 上（本地坐标系 = 肘部骨骼：原点在肘，+Z 沿前臂），
    // 把 ArmVisualSettings 应用到手模型、掌心（Palm）和果实挂点（FruitHolder）的本地姿态上。
    // base* 由场景构建（MainSceneMenu）写入：自动对齐后的姿态，肘端在原点、手指朝 +Z。
    // 掌心坐标系：z = 指尖方向，y = 掌心朝外，x = 大拇指一侧。FruitHolder 在掌心外 palmToFruit 处。
    // 这些物体都不是 IK 骨骼（Rig 只驱动 Shoulder / Elbow / Wrist），这里每帧写不会和 IK 冲突。
    // 顺序：先叠加设置里的旋转（以肘部为轴心）和平移，再绕前臂轴滚转 roll（HandReach 在举到嘴边时设置）。
    [ExecuteAlways]
    public class ArmVisualPlacement : MonoBehaviour
    {
        public ArmVisualSettings settings;
        public Transform model;
        public Transform palm;
        public Transform fruitHolder;

        [HideInInspector] public Vector3 modelBasePosition;
        [HideInInspector] public Quaternion modelBaseRotation = Quaternion.identity;
        [HideInInspector] public Vector3 modelBaseScale = Vector3.one;
        [HideInInspector] public Vector3 palmBasePosition;
        [HideInInspector] public Quaternion palmBaseRotation = Quaternion.identity;

        // 绕前臂轴的额外滚转（度），运行时由 HandReach 设置
        [System.NonSerialized] public float roll;

        // 不含 roll 的掌心朝外方向（本物体本地空间），HandReach 用它算嘴边要滚多少
        public Vector3 PalmNormalNoRoll
        {
            get
            {
                Vector3 euler = settings != null ? settings.rotationEuler : Vector3.zero;
                return Quaternion.Euler(euler) * (palmBaseRotation * Vector3.up);
            }
        }

        void Update() => Apply();

        public void Apply()
        {
            Vector3 euler = settings != null ? settings.rotationEuler : Vector3.zero;
            Vector3 offset = settings != null ? settings.positionOffset : Vector3.zero;
            float scale = settings != null ? Mathf.Max(0.01f, settings.scale) : 1f;
            Quaternion r = Quaternion.AngleAxis(roll, Vector3.forward);
            Quaternion q = r * Quaternion.Euler(euler);
            Vector3 rolledOffset = r * offset;

            if (model != null)
                Set(model, q * (modelBasePosition * scale) + rolledOffset, q * modelBaseRotation, modelBaseScale * scale);
            if (palm != null)
            {
                Vector3 adjust = settings != null ? settings.palmAdjust : Vector3.zero;
                Vector3 p = palmBasePosition * scale + palmBaseRotation * adjust;
                Set(palm, q * p + rolledOffset, q * palmBaseRotation, Vector3.one);
            }
            if (fruitHolder != null)
            {
                float d = settings != null ? settings.palmToFruit : 0.08f;
                Set(fruitHolder, new Vector3(0f, d, 0f), Quaternion.identity, Vector3.one);
            }
        }

        // 值没变就不写，编辑模式下不弄脏场景
        static void Set(Transform t, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (t.localPosition != position) t.localPosition = position;
            if (t.localRotation != rotation) t.localRotation = rotation;
            if (t.localScale != scale) t.localScale = scale;
        }
    }
}
