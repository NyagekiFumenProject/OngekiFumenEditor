using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Text
{
    public interface IStringMeasure
    {
        Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle);
    }
}
