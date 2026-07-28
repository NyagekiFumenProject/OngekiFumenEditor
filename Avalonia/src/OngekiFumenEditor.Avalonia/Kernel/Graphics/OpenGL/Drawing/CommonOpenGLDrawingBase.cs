using OngekiFumenEditor.Avalonia.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.OpenGL.Drawing
{
    public class CommonOpenGLDrawingBase : CommonDrawingBase
    {
        protected DefaultOpenGLRenderManagerImpl manager;

        public CommonOpenGLDrawingBase(DefaultOpenGLRenderManagerImpl manager)
        {
            this.manager = manager;
        }
    }
}


