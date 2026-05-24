using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    internal sealed class DefaultOpenGLRenderContext : IRenderContext
    {
        private readonly DefaultOpenGLRenderManagerImpl manager;
        private readonly GLWpfControl glView;
        private bool isStart = false;
        private long prevRenderTimestamp;

        public DefaultOpenGLRenderContext(DefaultOpenGLRenderManagerImpl manager, GLWpfControl glView)
        {
            this.manager = manager;
            this.glView = glView;
        }

        public bool IsInitialized { get; internal set; }

        public string Name { get; set; }

        public event Action<IRenderContext, TimeSpan> OnRender;

        public int LimitFPS { get; set; } = -1;

        public IPerfomenceMonitor PerfomenceMonitor { get; set; } = DummyPerformenceMonitor.Instance;

        public void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true)
        {
            manager.PostDrawCommandList(this, drawCommandList, autoDispose);
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

        public void StartRendering()
        {
            if (isStart)
                return;
            isStart = true;

            prevRenderTimestamp = 0;
            glView.Render += GlView_Render;
        }

        public void StopRendering()
        {
            if (!isStart)
                return;
            isStart = false;

            glView.Render -= GlView_Render;
        }

        private void GlView_Render(TimeSpan ts)
        {
            if (!TryUpdateRenderTime(out var actualTs))
                return;

            OnRender?.Invoke(this, actualTs);

            SwapAndPresentDrawCommandList();
        }

        private void SwapAndPresentDrawCommandList()
        {
            if (!manager.SwapDrawCommandList(this))
                return;

            manager.PresentDrawCommandList(this);
        }

        private bool TryUpdateRenderTime(out TimeSpan ts)
        {
            var curRenderTimestamp = Stopwatch.GetTimestamp();

            if (prevRenderTimestamp == 0)
            {
                prevRenderTimestamp = curRenderTimestamp;
                ts = TimeSpan.Zero;
                return true;
            }

            ts = Stopwatch.GetElapsedTime(prevRenderTimestamp, curRenderTimestamp);
            var limitFPS = LimitFPS;
            if (limitFPS > 0 && ts.TotalSeconds < 1.0 / limitFPS)
                return false;

            prevRenderTimestamp = curRenderTimestamp;
            return true;
        }
    }
}
