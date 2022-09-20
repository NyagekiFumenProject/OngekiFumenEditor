
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface ISimpleLineDrawing : ILineDrawing
    {
        public interface IVBOHandle : IDisposable
        {

        }

        void Begin(IFumenPreviewer target, float lineWidth);
        void PostPoint(Vector2 Point, Vector4 Color);
        void End();

        void DrawVBO(IFumenPreviewer target, IVBOHandle vbo);
        IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth);
    }
}
