using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ghost.Agent.EditorTools
{
    // Agent 聊天气泡用的几张白色小图，缺少时用代码画出来（不覆盖已有文件，策划可以直接替换同名 PNG）：
    //   bubble_round.png  圆角矩形，九宫格（气泡、Caution 卡、按钮）
    //   circle.png        圆（头像底）
    //   bubble_tail.png   气泡指向头像的小三角
    //   caution_header.png 上圆下直的标签条（Caution 卡顶部）
    //   icon_agent.png    头像图标【占位】，策划稍后换正式图标
    // 颜色都在 Image.color 上染，图本身是白色。
    [InitializeOnLoad]
    public static class AgentChatArt
    {
        const int RoundSize = 64, RoundRadius = 24;

        static AgentChatArt()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Ensure();
            };
        }

        [MenuItem("Ghost/Agent/Generate Chat Bubble Art")]
        public static void Ensure()
        {
            EnsureFolder();
            Write(AgentUIStyle.ChatRoundSprite, RoundSize, RoundRadius, (x, y) => InRoundRect(x, y, RoundSize, RoundRadius));
            Write(AgentUIStyle.ChatCircleSprite, 128, 0, (x, y) => Sq(x - 64f) + Sq(y - 64f) <= 63f * 63f, mips: true);
            // 三角：尖朝左上，右边贴着气泡
            Write(AgentUIStyle.ChatTailSprite, 32, 0, (x, y) => InTriangle(x, y, new Vector2(0f, 30f), new Vector2(32f, 30f), new Vector2(32f, 4f)));
            // 上圆下直：只圆上面两个角
            Write(AgentUIStyle.ChatHeaderSprite, RoundSize, new Vector4(RoundRadius, 0f, RoundRadius, RoundRadius),
                (x, y) => InTopRoundRect(x, y, RoundSize, RoundRadius));
            Write(AgentUIStyle.ChatIconSprite, 128, 0, PlaceholderIcon, mips: true);
        }

        static void EnsureFolder()
        {
            string[] parts = AgentUIStyle.ChatArtFolder.Split('/');
            string path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }

        // 4×4 超采样抗锯齿，白色 + alpha
        static void Write(string file, int size, Vector4 border, Func<float, float, bool> inside, bool mips = false)
        {
            string assetPath = AgentUIStyle.ChatArtFolder + "/" + file;
            if (File.Exists(FullPath(assetPath))) return;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) hits++;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(FullPath(assetPath), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = mips;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            Debug.Log($"[Agent] 已生成聊天气泡素材 {assetPath}");
        }

        static void Write(string file, int size, int border, Func<float, float, bool> inside, bool mips = false) =>
            Write(file, size, new Vector4(border, border, border, border), inside, mips);

        static string FullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        static float Sq(float v) => v * v;

        static bool InRoundRect(float x, float y, int size, int r)
        {
            float half = size * 0.5f;
            float qx = Mathf.Max(Mathf.Abs(x - half) - (half - r), 0f);
            float qy = Mathf.Max(Mathf.Abs(y - half) - (half - r), 0f);
            return qx * qx + qy * qy <= r * r;
        }

        // 只圆上面两个角（Unity 纹理 y 向上，上边 = y 大）
        static bool InTopRoundRect(float x, float y, int size, int r)
        {
            if (y < r) return x >= 0f && x <= size && y >= 0f;
            return InRoundRect(x, y, size, r);
        }

        static bool InTriangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(x, y);
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        // 【占位】六边形环 + 中间一道竖条
        static bool PlaceholderIcon(float x, float y)
        {
            float px = (x - 64f) / 64f, py = (y - 64f) / 64f;
            bool outer = InHexagon(px, py, 0.82f);
            bool inner = InHexagon(px, py, 0.56f);
            bool bar = Mathf.Abs(px) <= 0.09f && Mathf.Abs(py) <= 0.5f;
            return (outer && !inner) || bar;
        }

        // 尖顶朝上的正六边形，外接圆半径 r
        static bool InHexagon(float x, float y, float r)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            return ax <= r * 0.8660254f && ay <= r - ax * 0.57735027f;
        }
    }
}
