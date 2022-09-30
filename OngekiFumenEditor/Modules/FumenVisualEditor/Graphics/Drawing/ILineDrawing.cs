
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    public interface ILineDrawing : IDrawing
    {
        public record LineVertex(Vector2 Point, Vector4 Color);
        void Draw(IFumenEditorDrawingContext target, IEnumerable<LineVertex> points, float lineWidth);
    }
}
