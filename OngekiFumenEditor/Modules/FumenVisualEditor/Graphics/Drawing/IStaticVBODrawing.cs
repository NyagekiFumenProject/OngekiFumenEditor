using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    public interface IStaticVBODrawing : IDrawing
    {
        public interface IVBOHandle : IDisposable
        {

        }
        void DrawVBO(IFumenEditorDrawingContext target, IVBOHandle vbo);
    }
}
