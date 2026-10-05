using UnityEngine;

namespace Ghost.Morph
{
    // 节点颜色。抽象形态（矩阵、回路、网络）只用黑、灰、蓝灰；越接近写实，越多地混入部位本来的颜色
    public static class OrganPalette
    {
        // 抽象形态的色阶，按节点 id 散列取色，让方块之间有明暗变化（参考 ENCODED STATES 概念图）
        static readonly Color[] AbstractTones =
        {
            new Color(0.10f, 0.10f, 0.11f),
            new Color(0.30f, 0.31f, 0.33f),
            new Color(0.30f, 0.31f, 0.33f),
            new Color(0.52f, 0.53f, 0.55f),
            new Color(0.40f, 0.47f, 0.56f),
            new Color(0.40f, 0.47f, 0.56f),
            new Color(0.66f, 0.67f, 0.69f),
        };

        public static Color RealColor(Organ organ)
        {
            switch (organ)
            {
                case Organ.Soil: return new Color(0.20f, 0.15f, 0.12f);
                case Organ.Root: return new Color(0.55f, 0.45f, 0.33f);
                case Organ.Stem: return new Color(0.28f, 0.42f, 0.18f);
                case Organ.Leaf: return new Color(0.24f, 0.52f, 0.22f);
                case Organ.Bud: return new Color(0.62f, 0.78f, 0.35f);
                case Organ.Fruit: return new Color(0.82f, 0.16f, 0.10f);
                case Organ.Bug: return new Color(0.10f, 0.08f, 0.12f);
                default: return Color.magenta;
            }
        }

        public static Color AbstractColor(PlantNode node)
        {
            uint hash = (uint)node.id * 2654435761u;
            return AbstractTones[(hash >> 16) % (uint)AbstractTones.Length];
        }

        // 各形态混入多少写实颜色：0 = 纯灰阶，1 = 写实
        public static float Realness(MorphForm form)
        {
            switch (form)
            {
                case MorphForm.Geometric: return 0.4f;
                case MorphForm.Real: return 1f;
                default: return 0f;
            }
        }

        // 连线参数：x = 直角折线程度（0 直线，1 直角），y = 不透明度。
        // 矩阵没有连线；回路是直角走线；网络是直线；几何植株淡出；写实没有连线
        public static Vector2 LinkParams(MorphForm form)
        {
            switch (form)
            {
                case MorphForm.Circuit: return new Vector2(1f, 1f);
                case MorphForm.Network: return new Vector2(0f, 0.8f);
                case MorphForm.Geometric: return new Vector2(0f, 0.15f);
                default: return Vector2.zero;
            }
        }

        public static readonly Color LinkColor = new Color(0.22f, 0.27f, 0.34f);

        public static Color FormColor(PlantNode node, MorphForm form)
        {
            Color real = node.realColor.a > 0f ? node.realColor : RealColor(node.organ);
            return Color.Lerp(AbstractColor(node), real, Realness(form));
        }
    }
}
