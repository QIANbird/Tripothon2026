using UnityEngine;

namespace Ghost.Morph
{
    // 各部位在写实/几何形态下的颜色。抽象形态（矩阵、回路、网络）统一用灰蓝色调，由渲染器决定
    public static class OrganPalette
    {
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
    }
}
