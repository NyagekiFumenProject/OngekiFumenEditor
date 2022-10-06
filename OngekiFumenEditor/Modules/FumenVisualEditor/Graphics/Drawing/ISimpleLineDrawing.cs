
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    public interface ISimpleLineDrawing : ILineDrawing, IStaticVBODrawing
    {
        void Begin(IFumenEditorDrawingContext target, float lineWidth);
        void PostPoint(Vector2 Point, Vector4 Color, VertexDash dash);
        void End();

        IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth);
    }
}