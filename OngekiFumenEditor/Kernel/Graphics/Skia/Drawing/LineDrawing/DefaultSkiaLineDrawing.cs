using OngekiFumenEditor.Utils;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.CircleDrawing
{
    internal class DefaultSkiaLineDrawing : CommonSkiaDrawingBase, ILineDrawing
    {
        private SKCanvas canvas;

        public DefaultSkiaLineDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target)
        {
            OnBegin(target);

            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            prevPaintParam = default;
        }

        public void End()
        {
            OnEnd();

            canvas = default;
            prevPaintParam = default;
            prevPaint?.Dispose();
        }

        private (Vector4 color, VertexDash dash) prevPaintParam = default;
        private SKPaint prevPaint = default;

        private SKPaint GetPaint(Vector4 color, VertexDash dash, float lineWidth)
        {
            var param = (color, dash);
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
                Style = SKPaintStyle.Stroke,
                StrokeWidth = lineWidth,
                PathEffect = dash == VertexDash.Solider ? null : SKPathEffect.CreateDash([10, 5], 0)
            };

            prevPaint = paint;
            prevPaintParam = param;

            return paint;
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            Begin(target);

            var path = new SKPath();
            var itor = points.GetEnumerator();

            if (itor.MoveNext())
            {
                var prev = itor.Current;
                (Vector4, VertexDash)? prevParam = default;
                path.MoveTo(prev.Point.ToSkiaSharpPoint());

                while (itor.MoveNext())
                {
                    var cur = itor.Current;
                    var curParam = (cur.Color, cur.Dash);

                    if (curParam == prevParam || prevParam is null)
                    {
                        //just add to path if same color and dash
                        path.LineTo(cur.Point.ToSkiaSharpPoint());
                    }
                    else
                    {
                        //draw current path
                        var paint = GetPaint(prev.Color, prev.Dash, lineWidth);
                        canvas.DrawPath(path, paint);
                        target.PerfomenceMonitor.CountDrawCall(this);

                        //new path
                        path?.Dispose();
                        path = new SKPath();
                        path.MoveTo(prev.Point.ToSkiaSharpPoint());
                        path.LineTo(cur.Point.ToSkiaSharpPoint());
                    }

                    prevParam = curParam;
                    prev = cur;
                }

                if (path != null && path.PointCount > 0)
                {
                    var paint = GetPaint(prev.Color, prev.Dash, lineWidth);
                    canvas.DrawPath(path, paint);
                    target.PerfomenceMonitor.CountDrawCall(this);
                }
                path?.Dispose();
            }

            End();
        }
    }
}
