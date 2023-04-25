using OngekiFumenEditor.Base.EditorObjects.Svg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface ISvgDrawing : IDrawing
    {
        void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position);
    }
}
