using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IStringDrawing
    {
        public enum StringStyle
        {
            Normal = 0,
            Bold = 1,
            Italic = 2,
            Overline = 4,
            Strike = 8,
            Underline = 16,
        }

        public interface FontHandle
        {
            string Name { get; }
        }

        IEnumerable<FontHandle> SupportFonts { get; }

        void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, StringStyle style, IFumenPreviewer target, FontHandle handle, out Vector2? measureTextSize);
    }
}
