
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IPolygonDrawing : IDrawing
    {
        public record PolygonVertex(Vector2 Point, Vector2 Color);
        void Draw(IFumenPreviewer target, IEnumerable<PolygonVertex> vertices, bool isFill);
    }
}
