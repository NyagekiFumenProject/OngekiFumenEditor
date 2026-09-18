using SkiaSharp;

namespace OngekiFumenEditor.Utils
{
    public static class Vector4ExtensionMethod
    {
        public static SKColor ToSKColor(this System.Numerics.Vector4 p)
            => new((byte)(255 * p.X), (byte)(255 * p.Y), (byte)(255 * p.Z), (byte)(255 * p.W));

        public static SKColorF ToSKColorF(this System.Numerics.Vector4 p)
            => new(p.X, p.Y, p.Z, p.W);
    }
}
