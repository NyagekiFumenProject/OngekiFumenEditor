using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface ILineDrawing
    {
        public record LineVertex(Vector2 Point, Vector2 Color);
        void Draw(IFumenPreviewer target, IEnumerable<LineVertex> points, float lineWidth);
    }
}
