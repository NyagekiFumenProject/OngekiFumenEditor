using System;
using System.Numerics;
using System.Windows.Media;

namespace OngekiFumenEditor.Utils
{
    public static class ColorExtensionMethod
    {
        public static float ColorDistance(this Color a, Color b)
        {
            byte ra = a.R, rb = b.R, ga = a.G, gb = b.G, ba = a.B, bb = b.B;
            var rm = (ra + rb) / 2.0f;
            var R = (ra - rb);
            var G = (ga - gb);
            var B = (ba - bb);
            return MathF.Sqrt((2 + rm / 256.0f) * MathF.Pow(R, 2) + 4 * MathF.Pow(G, 2) + (2 + (255 - rm) / 256.0f) * MathF.Pow(B, 2));
        }

        public static Vector4 ToVector4(this System.Drawing.Color color)
        {
            return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        public static System.Drawing.Color AsARGBToColor(this int argb) => System.Drawing.Color.FromArgb(argb);
        public static System.Windows.Media.Color ToMediaColor(this System.Drawing.Color color) => System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        public static System.Drawing.Color ToDrawingColor(this System.Windows.Media.Color color) => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}