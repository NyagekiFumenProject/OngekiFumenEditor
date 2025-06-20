using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Utils;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing
{
    internal class DefaultSkiaLineDrawing : CommonSkiaDrawingBase, ILineDrawing, ISimpleLineDrawing
    {
        private SKCanvas canvas;
        private List<LineVertex> postedPoints = new();
        private IDrawingContext target;
        private int drawcallCount = 0;

        public DefaultSkiaLineDrawing(DefaultSkiaDrawingManager manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target)
        {
            OnBegin(target);

            this.target = target;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            prevPaintParam = default;
            postedPoints.Clear();
            drawcallCount = 0;
        }

        public void End()
        {
            PostDraw();
            OnEnd();

            lineWidth = default;
            canvas = default;
            prevPaintParam = default;
            prevPaint?.Dispose();
            postedPoints.Clear();
        }

        private (Vector4 color, VertexDash dash) prevPaintParam = default;
        private SKPaint prevPaint = default;
        private float lineWidth;

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

        private void PostDraw()
        {
            var path = new SKPath();
            var itor = postedPoints.GetEnumerator();

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
                        DrawPath(path, paint);

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
                    DrawPath(path, paint);
                }
                path?.Dispose();
            }
        }

        private void DrawPath(SKPath path, SKPaint paint)
        {
            var actualPath = /*path.PointCount > 100 ? path.Simplify() : */path;

            canvas.DrawPath(actualPath, paint);
            target.PerfomenceMonitor.CountDrawCall(this);
            drawcallCount++;
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            Begin(target, lineWidth);

            foreach (var point in points)
                PostPoint(point.Point, point.Color, point.Dash);

            End();
        }

        public void Begin(IDrawingContext target, float lineWidth)
        {
            Begin(target);
            this.lineWidth = lineWidth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PostPoint(LineVertex vertex)
        {
            postedPoints.Add(vertex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PostPoint(Vector2 point, Vector4 color, VertexDash dash)
        {
            PostPoint(new LineVertex(point, color, dash));
        }

        public IStaticVBODrawing.IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth)
        {
            throw new System.NotImplementedException();
        }

        public void DrawVBO(IDrawingContext target, IStaticVBODrawing.IVBOHandle vbo)
        {
            throw new System.NotImplementedException();
        }
    }
}
