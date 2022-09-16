using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IDrawing
    {
        void PushOverrideModelMatrix(Matrix4 modelMatrix);
        Matrix4 GetOverrideModelMatrix();
        bool PopOverrideModelMatrix(out Matrix4 modelMatrix);
    }
}
