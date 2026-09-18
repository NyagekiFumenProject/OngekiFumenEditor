using System.Collections.Generic;
using System.Numerics;
using OngekiFumenEditor.Kernel.Graphics;

namespace OngekiFumenEditor.Kernel.Graphics.Text
{
    public interface IStringDrawing : IDrawing
    {
        IEnumerable<IFontHandle> SupportFonts { get; }

        void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize);
    }
}
