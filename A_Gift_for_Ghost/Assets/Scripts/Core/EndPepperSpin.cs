using UnityEngine;

namespace Ghost.Core
{
    // 结束界面的彩椒：绕本地斜轴（默认 (1,1,0)，与竖直方向成 45°）匀速转，同时轻微上下浮动。
    // 挂在展示物体的子物体上，本地位置只用来浮动（父物体负责摆放和缩放）。网格按"彩椒最长边 = 1"归一化，
    // 振幅也按这个单位：0.08 ≈ 真实大小（约 12 cm）下的 1 cm，展示时整体放大，浮动按比例跟着放大。
    // 用 unscaled time：暂停（timeScale = 0）不影响结束界面。
    public class EndPepperSpin : MonoBehaviour
    {
        public Vector3 axis = new Vector3(1f, 1f, 0f);
        [Tooltip("转速（度 / 秒）")]
        public float speed = 20f;
        [Tooltip("上下浮动振幅（彩椒最长边 = 1）")]
        public float bobAmplitude = 0.08f;
        [Tooltip("浮动周期（秒）")]
        public float bobPeriod = 3f;

        float angle;
        float time;

        void OnEnable()
        {
            angle = 0f;
            time = 0f;
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            angle = Mathf.Repeat(angle + speed * dt, 360f);
            time += dt;
            Apply();
        }

        void Apply()
        {
            Vector3 a = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
            transform.localRotation = Quaternion.AngleAxis(angle, a);
            float bob = bobPeriod > 0f ? Mathf.Sin(time * 2f * Mathf.PI / bobPeriod) * bobAmplitude : 0f;
            transform.localPosition = Vector3.up * bob;
        }
    }
}
