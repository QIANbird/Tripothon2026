using UnityEngine;

namespace Ghost.Core
{
    // 键位提示图的摆放参数。场景是代码生成的（MainSceneMenu），直接在场景里改会被 Build Main Scene 覆盖，
    // 所以可调参数放在这个资产里（Assets/Data/UI/ControlsGuideSettings.asset，由 Build Main Scene 生成）：
    // 编辑模式和 Play 模式下改数值都立刻生效（ControlsGuide 每帧读取），Play 里改的值退出后也保留。
    public class ControlsGuideSettings : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("在这些阶段显示（GameFlow 里的阶段名）")]
            public string[] stages;
            public Texture2D image;
            [Tooltip("图片中心的世界坐标（米）。固定机位在 (0, 1.6, -1.9) 朝 +Z")]
            public Vector3 position;
            [Tooltip("图片高度（米），宽度按图片比例")]
            public float height = 0.3f;
        }

        // 新值只往后加：资产里存的是枚举的整数值
        public enum Preview { None, S1S2, S3S4, Pick, S2 }

        // 字段名沿用 s1s2（改名会丢掉资产里调好的值），10-16 起只用于 S1，S2 用下面的 s2
        public Entry s1s2 = new Entry { stages = new[] { "S1" }, position = new Vector3(1f, 1.25f, -1.5f), height = 0.16f };
        public Entry s2 = new Entry { stages = new[] { "S2" }, position = new Vector3(1f, 1.25f, -1.5f), height = 0.16f };
        public Entry s3s4 = new Entry { stages = new[] { "S3", "S4" }, position = new Vector3(1f, 1.25f, -1.5f), height = 0.16f };
        public Entry pick = new Entry { stages = new[] { "Pick" }, position = new Vector3(-0.8f, 1.1f, 0.6f), height = 0.4f };

        [Header("Pause/Help 按钮（射线点击打开暂停界面）")]
        [Tooltip("按钮中心的世界坐标（米）")]
        public Vector3 pauseButtonPosition = new Vector3(0.93f, 1.4f, -1.5f);
        [Tooltip("大小：每个 Canvas 像素多少米。按钮约 380×120 像素，0.0006 ≈ 23 cm 宽")]
        public float pauseButtonMetersPerPixel = 0.0006f;
        [Tooltip("在这些阶段显示")]
        public string[] pauseButtonStages = { "S1", "S2", "S3", "S4" };

        [Header("教学弹窗（TUT_003 / TUT_012 播完后在演示方块右侧滑出，见 docs/tasks/tut-guide-popups.md）")]
        [Tooltip("Select / Confirm（BeginEasy 滑入，简单问题解决后滑出）")]
        public Texture2D tutorialSubmitImage;
        [Tooltip("Select / Details（BeginInspect 滑入，第一次查看详情后滑出）")]
        public Texture2D tutorialDetailsImage;
        [Tooltip("弹窗中心相对演示方块中心的世界偏移（米）：x 向右、y 向上、负 z 朝玩家")]
        public Vector3 tutorialPopupOffset = new Vector3(0.22f, 0f, -0.05f);
        [Tooltip("底板高度（米），宽度按图片比例")]
        public float tutorialPopupHeight = 0.16f;
        [Tooltip("图片四周留白，占底板高度的比例")]
        [Range(0f, 0.45f)] public float tutorialPopupPadding = 0.07f;
        [Tooltip("底板颜色和透明度")]
        public Color tutorialPopupColor = new Color(0.851f, 0.851f, 0.851f, 0.85f);
        [Tooltip("圆角半径，占底板高度的比例")]
        [Range(0f, 0.5f)] public float tutorialPopupCornerRadius = 0.08f;
        [Tooltip("滑入 / 滑出的水平距离（米），从目标位置再往右")]
        public float tutorialPopupSlideDistance = 0.1f;
        [Tooltip("滑入 / 滑出时长（秒）")]
        public float tutorialPopupSlideSeconds = 0.35f;

        [Header("显示")]
        [Tooltip("只绕竖直轴转向玩家（图片保持竖直）；关掉 = 完全正对相机")]
        public bool yawOnly = true;
        [Tooltip("PC（没有头显）上也显示。图片画的是 Quest 手柄")]
        public bool showOnPC = true;
        [Tooltip("淡入淡出时长（秒）")]
        public float fadeSeconds = 0.4f;
        [Tooltip("透明度")]
        [Range(0f, 1f)] public float opacity = 1f;

        [Header("编辑模式预览（不进 Play 时显示哪张，方便在 Scene 视图里摆位置；不为 None 时 Pause/Help 按钮也显示）")]
        public Preview previewInEditMode = Preview.None;

        public Entry Find(string stageName)
        {
            foreach (var e in All())
                if (e != null && e.stages != null && System.Array.IndexOf(e.stages, stageName) >= 0) return e;
            return null;
        }

        public Entry PreviewEntry => previewInEditMode switch
        {
            Preview.S1S2 => s1s2,
            Preview.S2 => s2,
            Preview.S3S4 => s3s4,
            Preview.Pick => pick,
            _ => null,
        };

        // s2 排在 s1s2 前面：旧资产里 s1s2 的阶段还带着 S2 时（Build Main Scene 会去掉），S2 也先匹配到 s2
        Entry[] All() => new[] { s2, s1s2, s3s4, pick };
    }
}
