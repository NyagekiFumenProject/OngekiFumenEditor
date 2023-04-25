using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IStaticVBODrawing : IDrawing
    {
        public interface IVBOHandle : IDisposable
        {

        }
        void DrawVBO(IDrawingContext target, IVBOHandle vbo);
    }
}
