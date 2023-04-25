
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IPolygonDrawing : IDrawing
    {
        int AvailablePostableVertexCount { get; }
        void Begin(IDrawingContext target);
        void PostPoint(Vector2 Point, Vector4 Color);
        void End();
    }
}
