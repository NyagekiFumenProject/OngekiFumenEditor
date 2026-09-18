using SkiaSharp;

namespace OngekiFumenEditor.Utils
{
    public static class Vector2ExtensionMethod
    {
        public static SKPoint ToSkiaSharpPoint(this System.Numerics.Vector2 p)
            => new(p.X, p.Y);

        public static System.Numerics.Vector2 ToSystemNumericsVector2(this System.Numerics.Vector2 p)
            => p;

        public static System.Numerics.Vector2 ToSystemNumericsVector2(this System.Windows.Point p)
            => new((float)p.X, (float)p.Y);
    }
}
