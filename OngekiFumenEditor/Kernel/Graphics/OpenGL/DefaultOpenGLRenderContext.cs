using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    internal class DefaultOpenGLRenderContext : IRenderContext
    {
        private readonly DefaultOpenGLRenderManager manager;
        private readonly GLWpfControl glView;
        private bool isStart = false;

        public DefaultOpenGLRenderContext(DefaultOpenGLRenderManager manager, GLWpfControl glView)
        {
            this.manager = manager;
            this.glView = glView;
        }

        public bool IsInitialized { get; internal set; }

        public event Action<TimeSpan> OnRender;

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

        public void StartRendering()
        {
            if (isStart)
                return;
            isStart = true;

            glView.Render += OnRender;
        }

        public void StopRendering()
        {
            if (!isStart)
                return;
            isStart = false;

            glView.Render -= OnRender;
        }
    }
}
