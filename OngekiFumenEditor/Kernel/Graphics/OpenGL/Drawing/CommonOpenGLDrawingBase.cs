using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl
{
    public class CommonOpenGLDrawingBase : CommonDrawingBase
    {
        protected DefaultOpenGLDrawingManager manager;

        public CommonOpenGLDrawingBase(DefaultOpenGLDrawingManager manager)
        {
            this.manager = manager;
        }
    }
}
