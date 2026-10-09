using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghost.Morph
{
    // 节点外围的发光圈：在节点四周画一圈从内向外渐隐的方框，和节点本身的颜色状态（闪烁、着色）互不干扰。
    // 两种发光可以叠加：
    //   SetGlow —— 常驻发光，可选按频率呼吸（S2 的脉冲源）；
    //   Flash   —— 瞬间亮起，再按半衰期指数衰减（S2 沿线路依次亮起的脉冲，前一个还没灭后一个就亮，看得出连续）。
    // 所有发光圈合在一个动态网格里一次绘制，使用和连线相同的顶点色透明材质（Ghost/MorphLine）。只影响显示。
    [RequireComponent(typeof(NodeMorpher))]
    [DefaultExecutionOrder(100)] // 在 NodeMorpher.Update 之后读取本帧姿态
    public class NodeHaloRenderer : MonoBehaviour
    {
        [Tooltip("需要 Ghost/MorphLine 材质（顶点色 + 透明）。留空时借用同物体上 NodeLinkRenderer 的材质")]
        public Material material;
        [Tooltip("发光圈内边缘离节点边缘的距离，按节点边长的比例")]
        public float innerGap = 0.08f;
        [Tooltip("发光圈的宽度，按节点边长的比例")]
        public float glowWidth = 0.55f;
        [Tooltip("呼吸发光的最低亮度（相对最高亮度）")]
        [Range(0f, 1f)] public float breathMin = 0.25f;

        NodeMorpher morpher;
        Mesh mesh;
        Color[] glowColors;
        float[] glowIntensity;
        float[] glowFrequency;
        float[] glowStart;
        Color[] flashColors;
        float[] flashStart;
        float[] flashHalfLife;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        // 亮度低于这个值的瞬时发光视为结束
        const float FlashCutoff = 0.02f;

        // 在 Awake 里初始化：运行时 AddComponent 后可以马上调用接口
        void Awake()
        {
            morpher = GetComponent<NodeMorpher>();
            if (material == null)
            {
                var links = GetComponent<NodeLinkRenderer>();
                if (links != null) material = links.material;
            }
            if (material == null || morpher.nodeSet == null)
            {
                Debug.LogError("[Morph] NodeHaloRenderer 缺少 material 或 NodeMorpher 没有 nodeSet", this);
                enabled = false;
                return;
            }
            int count = morpher.nodeSet.Count;
            glowColors = new Color[count];
            glowIntensity = new float[count];
            glowFrequency = new float[count];
            glowStart = new float[count];
            flashColors = new Color[count];
            flashStart = new float[count];
            flashHalfLife = new float[count];
            mesh = new Mesh { name = "NodeHalos" };
            mesh.MarkDynamic();
        }

        bool IsValid(int id) => glowIntensity != null && id >= 0 && id < glowIntensity.Length;

        // 常驻发光。frequency > 0 时在 breathMin 和 intensity 之间呼吸；intensity <= 0 等于关掉
        public void SetGlow(int id, Color color, float intensity = 1f, float frequency = 0f)
        {
            if (!IsValid(id)) return;
            // 频率不变时不重置相位，避免反复调用时呼吸跳变
            if (glowIntensity[id] <= 0f || !Mathf.Approximately(glowFrequency[id], frequency)) glowStart[id] = Time.time;
            glowColors[id] = color;
            glowIntensity[id] = Mathf.Max(0f, intensity);
            glowFrequency[id] = frequency;
        }

        // 瞬时发光：立刻亮到 1，之后每过 halfLife 秒亮度减半
        public void Flash(int id, Color color, float halfLife)
        {
            if (!IsValid(id)) return;
            flashColors[id] = color;
            flashStart[id] = Time.time;
            flashHalfLife[id] = Mathf.Max(0.01f, halfLife);
        }

        public void Clear(int id)
        {
            if (!IsValid(id)) return;
            glowIntensity[id] = 0f;
            flashHalfLife[id] = 0f;
        }

        public void ClearAll()
        {
            if (glowIntensity == null) return;
            for (int i = 0; i < glowIntensity.Length; i++)
            {
                glowIntensity[i] = 0f;
                flashHalfLife[i] = 0f;
            }
        }

        void LateUpdate()
        {
            var poses = morpher.CurrentPoses;
            if (poses == null || morpher.RealReveal >= 1f) return;

            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            float now = Time.time;
            for (int i = 0; i < glowIntensity.Length && i < poses.Length; i++)
            {
                Color color;
                float k = Intensity(i, now, out color);
                k *= morpher.GetNodeVisibility(i);
                if (k <= 0.001f) continue;
                AddRing(poses[i], color, k);
            }
            if (vertices.Count == 0) return;

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };
            Graphics.RenderMesh(rp, mesh, 0, transform.localToWorldMatrix);
        }

        // 常驻和瞬时取较亮的一个
        float Intensity(int i, float now, out Color color)
        {
            color = glowColors[i];
            float glow = glowIntensity[i];
            if (glow > 0f && glowFrequency[i] > 0f)
            {
                float t = (now - glowStart[i]) * glowFrequency[i];
                float wave = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f); // 从最亮开始
                glow *= Mathf.Lerp(breathMin, 1f, wave);
            }

            if (flashHalfLife[i] > 0f)
            {
                float flash = Mathf.Pow(0.5f, (now - flashStart[i]) / flashHalfLife[i]);
                if (flash < FlashCutoff) flashHalfLife[i] = 0f;
                else if (flash > glow)
                {
                    color = flashColors[i];
                    glow = flash;
                }
            }
            return glow;
        }

        // 节点所在平面上的一圈方框：内边缘不透明，外边缘透明，看起来像外发光
        void AddRing(NodePose pose, Color color, float intensity)
        {
            Vector3 right = pose.rotation * Vector3.right;
            Vector3 up = pose.rotation * Vector3.up;
            float size = Mathf.Max(Mathf.Abs(pose.scale.x), Mathf.Abs(pose.scale.y));
            float inner = size * (0.5f + innerGap);
            float outer = inner + size * glowWidth;

            Color inC = color;
            inC.a = Mathf.Clamp01(color.a * intensity);
            Color outC = color;
            outC.a = 0f;

            int v = vertices.Count;
            // 0–3 内圈，4–7 外圈，顺序：右上、左上、左下、右下
            Vector2[] corners = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
            foreach (var c in corners) vertices.Add(pose.position + right * (c.x * inner) + up * (c.y * inner));
            foreach (var c in corners) vertices.Add(pose.position + right * (c.x * outer) + up * (c.y * outer));
            for (int j = 0; j < 4; j++) colors.Add(inC);
            for (int j = 0; j < 4; j++) colors.Add(outC);
            for (int j = 0; j < 4; j++)
            {
                int a = v + j, b = v + (j + 1) % 4, oa = a + 4, ob = b + 4;
                triangles.Add(a); triangles.Add(oa); triangles.Add(ob);
                triangles.Add(a); triangles.Add(ob); triangles.Add(b);
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
