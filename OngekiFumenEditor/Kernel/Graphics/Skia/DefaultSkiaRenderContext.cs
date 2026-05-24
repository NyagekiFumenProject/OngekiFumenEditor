using OngekiFumenEditor.Kernel.Graphics.Skia;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    public sealed class DefaultSkiaRenderContext : ISkiaRenderContext
    {
        private DefaultSkiaDrawingManagerImpl manager;
        private bool isStart;
        private long prevRenderTimestamp;
        private readonly SkiaRenderControlBase renderControl;

        public event Action<IRenderContext, TimeSpan> OnRender;

        public int LimitFPS { get; set; } = -1;

        public IPerfomenceMonitor PerfomenceMonitor { get; set; } = DummyPerformenceMonitor.Instance;

        public SKCanvas Canvas => renderControl.CurrentRenderSurface?.Canvas;

        public DefaultSkiaRenderContext(DefaultSkiaDrawingManagerImpl manager, SkiaRenderControlBase renderControl)
        {
            this.manager = manager;
            this.renderControl = renderControl;
        }

        public void AfterRender(IDrawingContext context)
        {
            Canvas.Restore();
        }

        public void BeforeRender(IDrawingContext context)
        {
            Canvas.Save();
        }

        public void CleanRender(IDrawingContext context, Vector4 cleanColor)
        {
            Canvas.Clear(new SKColorF(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W));
        }

        public void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true)
        {
            manager.PostDrawCommandList(this, drawCommandList, autoDispose);
        }

        public void StartRendering()
        {
            if (isStart)
                return;
            isStart = true;

            prevRenderTimestamp = 0;
            renderControl.PaintSurface += RenderControl_PaintSurface;
        }

        private void RenderControl_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            if (!TryUpdateRenderTime(out var ts))
                return;

            OnRender?.Invoke(this, ts);

            PostAndPresentDrawCommandList();
        }

        public void StopRendering()
        {
            if (!isStart)
                return;
            isStart = false;

            renderControl.PaintSurface -= RenderControl_PaintSurface;
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

        private void PostAndPresentDrawCommandList()
        {
            if (!manager.SwapDrawCommandList(this))
                return;

            Canvas.Save();
            manager.PresentDrawCommandList(this);
            Canvas.Restore();
        }
    }
}
