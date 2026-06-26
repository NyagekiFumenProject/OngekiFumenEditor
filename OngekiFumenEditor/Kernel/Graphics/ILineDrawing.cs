using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface ILineDrawing : IDrawing
    {
        public readonly record struct VertexDash(int DashSize, int GapSize)
        {
            public static VertexDash Solider { get; } = new VertexDash(100, 0);
        }

        public readonly record struct LineVertex(Vector2 Point, Vector4 Color, VertexDash Dash);
        void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth);
    }
}
