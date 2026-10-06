using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Agent
{
    // Agent 界面共用的配色和 UGUI 小工具。风格对应 PROJECT_SUMMARY 第 3 节 "Encoded States"：
    // 浅灰底、黑 / 灰 / 蓝灰、细线、方块标记。只有界面构建代码用它，组件本身的颜色都能在 Inspector 里改。
    public static class AgentUIStyle
    {
        public static readonly Color Ink = new Color(0.12f, 0.13f, 0.15f);
        public static readonly Color Gray = new Color(0.42f, 0.44f, 0.47f);
        public static readonly Color LightGray = new Color(0.76f, 0.77f, 0.79f);
        public static readonly Color BlueGray = new Color(0.33f, 0.43f, 0.55f);
        public static readonly Color Failed = new Color(0.62f, 0.30f, 0.28f);
        public static readonly Color PanelFill = new Color(0.96f, 0.96f, 0.97f, 0.94f);
        public static readonly Color PanelBorder = new Color(0.12f, 0.13f, 0.15f, 0.85f);

        // World Space Canvas 的像素 → 米：1000 px = 1 m
        public const float CanvasScale = 0.001f;

        static Font cachedFont;

        // 和 StagePanel 一样用内置动态字体：缺字时系统字体补字，中文能显示，不需要 TMP 字体资产
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        // 新建 World Space Canvas，正对 camera 放在 position。pivot 决定 sizeDelta 改变时哪条边不动
        public static Canvas CreateWorldCanvas(string name, Transform parent, Camera camera, Vector3 position,
            Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

            var rect = (RectTransform)go.transform;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            go.transform.localScale = Vector3.one * CanvasScale;
            go.transform.position = position;
            if (camera != null) go.transform.rotation = FacingRotation(position, camera.transform);
            return canvas;
        }

        // Canvas 朝向：和相机画面平行（forward = 相机朝向，up 取世界竖直），平面屏幕上不会出现梯形变形和倾斜。
        // 固定相机下只在构建时算一次；VR 版如需改为朝向头部，在这里统一改
        public static Quaternion FacingRotation(Vector3 position, Transform viewer)
        {
            Vector3 forward = viewer.forward;
            if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f) forward = position - viewer.position;
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        // 左上角锚定的子 RectTransform：position 是相对父物体左上角的像素偏移（y 向下为负）
        public static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // 铺满父物体（可内缩 inset 像素）
        public static RectTransform CreateStretch(string name, Transform parent, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text AddText(RectTransform rect, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, Font font = null)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font != null ? font : DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        // 带细边框的面板底：外层是边框色，内层内缩 border 像素填充底色。返回内层（放内容用）
        public static RectTransform AddFramedBackground(RectTransform panel, Color fill, Color border, float borderWidth)
        {
            AddImage(CreateStretch("Border", panel), border);
            var inner = CreateStretch("Fill", panel, borderWidth);
            AddImage(inner, fill);
            return inner;
        }
    }
}
