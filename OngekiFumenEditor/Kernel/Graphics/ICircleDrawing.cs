
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface ICircleDrawing : IDrawing
    {
        void Begin(IDrawingContext target);
        void Post(Vector2 point,Vector4 color,bool isSolid, float radius);
        void End();
    }
}
