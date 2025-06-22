using OngekiFumenEditor.Kernel.Graphics.Skia;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls;
using SkiaSharp;
using System;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    public class DefaultSkiaRenderContext : IRenderContext
    {
        private DefaultSkiaDrawingManagerImpl manager;
        private bool isStart;
        private DateTime prevRenderTime;
        private readonly SkiaRenderControlBase renderControl;

        public event Action<TimeSpan> OnRender;

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

        public void StartRendering()
        {
            if (isStart)
                return;
            isStart = true;

            prevRenderTime = DateTime.UtcNow;
            renderControl.PaintSurface += RenderControl_PaintSurface;
        }

        private void RenderControl_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            var curRenderTime = DateTime.UtcNow;
            var ts = curRenderTime - prevRenderTime;
            ////

            var lineDrawing = manager.LineDrawing;

            ////
            OnRender?.Invoke(ts);
            prevRenderTime = curRenderTime;
        }

        public void StopRendering()
        {
            if (!isStart)
                return;
            isStart = false;

            renderControl.PaintSurface -= RenderControl_PaintSurface;
        }
    }
}
