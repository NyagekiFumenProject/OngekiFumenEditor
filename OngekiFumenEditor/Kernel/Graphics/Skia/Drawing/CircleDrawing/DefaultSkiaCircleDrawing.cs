
using OngekiFumenEditor.Utils;
using SkiaSharp;
using System.Numerics;
using System.Windows.Controls;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.CircleDrawing
{
    internal class DefaultSkiaCircleDrawing : CommonSkiaDrawingBase, ICircleDrawing
    {
        private SKCanvas canvas;
        private IDrawingContext target;

        public DefaultSkiaCircleDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target)
        {
            OnBegin(target);

            this.target = target;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            prevPaintParam = default;
        }

        public void End()
        {
            OnEnd();

            target = default;
            canvas = default;
            prevPaintParam = default;
            prevPaint?.Dispose();
        }

        private (Vector4 color, bool isSolid) prevPaintParam = default;
        private SKPaint prevPaint = default;

        private SKPaint GetPaint(Vector4 color, bool isSolid)
        {
            var param = (color, isSolid);
            if (param == prevPaintParam && prevPaint != null)
                return prevPaint;

            prevPaint?.Dispose();

            var skColor = new SKColor(
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255),
                (byte)(color.W * 255));

            var paint = new SKPaint
            {
                Color = skColor,
                IsAntialias = true,
                Style = isSolid ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
                StrokeWidth = 2f
            };

            prevPaint = paint;
            prevPaintParam = param;

            return paint;
        }

        public void Post(Vector2 point, Vector4 color, bool isSolid, float radius)
        {
            var paint = GetPaint(color, isSolid);
            canvas.DrawCircle(point.X, point.Y, radius, paint);
            target.PerfomenceMonitor.CountDrawCall(this);
        }
    }
}
