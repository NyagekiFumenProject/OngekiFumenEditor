using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    internal class DefaultOpenGLRenderContext : IRenderContext
    {
        private readonly DefaultOpenGLDrawingManager manager;

        public DefaultOpenGLRenderContext(DefaultOpenGLDrawingManager manager)
        {
            this.manager = manager;
        }

        public void AfterRender(IDrawingContext context)
        {

        }

        public void BeforeRender(IDrawingContext context)
        {
            var renderViewWidth = (int)((context.CurrentDrawingTargetContext?.ViewWidth ?? 0) * manager.CurrentDPI.DpiScaleX);
            var renderViewHeight = (int)((context.CurrentDrawingTargetContext?.ViewHeight ?? 0) * manager.CurrentDPI.DpiScaleY);

            GL.Viewport(0, 0, renderViewWidth, renderViewHeight);
        }

        public void CleanRender(IDrawingContext context, Vector4 cleanColor)
        {
            GL.ClearColor(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
    }
}
