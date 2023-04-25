
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface ILineDrawing : IDrawing
    {
        public struct VertexDash
        {
            public int DashSize { get; set; }
            public int GapSize { get; set; }

            public static VertexDash Solider { get; } = new VertexDash()
            {
                GapSize = 0,
                DashSize = 100
            };
        }

        public record LineVertex(Vector2 Point, Vector4 Color, VertexDash Dash);
        void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth);
    }
}
