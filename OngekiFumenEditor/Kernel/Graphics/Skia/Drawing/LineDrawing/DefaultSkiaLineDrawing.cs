using OngekiFumenEditor.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing
{
    internal sealed class DefaultSkiaLineDrawing : CommonSkiaDrawingBase, ILineDrawing, ISimpleLineDrawing, IDisposable
    {
        private sealed class SkiaLineVBOHandle : IStaticVBODrawing.IVBOHandle
        {
            public LineVertex[] Points { get; private set; }
            public float LineWidth { get; private set; }

            public SkiaLineVBOHandle(IEnumerable<LineVertex> points, float lineWidth)
            {
                Points = points?.ToArray() ?? [];
                LineWidth = lineWidth;
            }

            public void Dispose()
            {
                Points = [];
                LineWidth = default;
            }
        }

        private SKCanvas canvas;
        private List<LineVertex> postedPoints = new();
        private IDrawingContext target;
        private readonly Dictionary<VertexDash, WeakReference<SKPathEffect>> dashPathEffectCache = new();

        public DefaultSkiaLineDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
        {

        }

        public void Begin(IDrawingContext target)
        {
            OnBegin(target);

            this.target = target;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            postedPoints.Clear();
        }

        public void End()
        {
            PostDraw();
            OnEnd();

            lineWidth = default;
            canvas = default;
            postedPoints.Clear();
        }

        private SKPaint curPaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        private readonly SKPath reusedPath = new();
        private float lineWidth;

        private void UpdatePaint(Vector4 color, VertexDash dash, float lineWidth)
        {
            var skColor = new SKColor(
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255),
                (byte)(color.W * 255));

            curPaint.Color = skColor;
            curPaint.StrokeWidth = lineWidth;
            curPaint.PathEffect = GetDashPathEffect(dash);
        }

        private SKPathEffect GetDashPathEffect(VertexDash dash)
        {
            if (dash.DashSize <= 0 || dash.GapSize <= 0)
                return null;

            if (!(dashPathEffectCache.TryGetValue(dash, out var pathEffectRef) && pathEffectRef.TryGetTarget(out var pathEffect)))
                dashPathEffectCache[dash] = new(pathEffect = SKPathEffect.CreateDash([dash.DashSize, dash.GapSize], 0));

            return pathEffect;
        }

        private void PostDraw()
        {
            //var path = new SKPath();
            var itor = postedPoints.GetEnumerator();
            using var points = ObjectPool.GetPooledList<SKPoint>();

            if (itor.MoveNext())
            {
                var prev = itor.Current;
                (Vector4, VertexDash)? prevParam = default;
                points.Add(prev.Point.ToSkiaSharpPoint());

                while (itor.MoveNext())
                {
                    var cur = itor.Current;
                    var curParam = (cur.Color, cur.Dash);

                    if (curParam == prevParam || prevParam is null)
                    {
                        //just add to path if same color and dash
                        points.Add(cur.Point.ToSkiaSharpPoint());
                    }
                    else
                    {
                        //draw current path
                        UpdatePaint(prev.Color, prev.Dash, lineWidth);
                        DrawPath(points, curPaint);

                        //new path
                        points.Clear();
                        points.Add(prev.Point.ToSkiaSharpPoint());
                        points.Add(cur.Point.ToSkiaSharpPoint());
                    }

                    prevParam = curParam;
                    prev = cur;
                }

                if (points.Count > 0)
                {
                    UpdatePaint(prev.Color, prev.Dash, lineWidth);
                    DrawPath(points, curPaint);
                }
            }

        }

        private void DrawPath(IList<SKPoint> points, SKPaint paint)
        {
            var path = reusedPath;
            path.Reset();
            path.MoveTo(points[0]);
            for (int i = 0; i < points.Count - 1; i++)
            {
                var cur = points[i];
                var next = points[i + 1];
                if (cur == next)
                    continue;
                path.LineTo(next);
            }
            canvas.DrawPath(path, paint);
            target.RenderContext.PerfomenceMonitor.CountDrawCall();
        }

        private void DrawPath(SKPath path, SKPaint paint)
        {
            var actualPath = /*path.PointCount > 100 ? path.Simplify() : */path;

            canvas.DrawPath(actualPath, paint);
            target.RenderContext.PerfomenceMonitor.CountDrawCall();
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
            return new SkiaLineVBOHandle(points, lineWidth);
        }

        public void DrawVBO(IDrawingContext target, IStaticVBODrawing.IVBOHandle vbo)
        {
            if (vbo is not SkiaLineVBOHandle handle || handle.Points.Length == 0)
                return;

            Draw(target, handle.Points, handle.LineWidth);
        }

        public void Dispose()
        {
            curPaint.PathEffect = null;
            curPaint.Dispose();
            reusedPath.Dispose();

            foreach (var effectRef in dashPathEffectCache.Values)
                if (effectRef.TryGetTarget(out var effect))
                    effect.Dispose();

            dashPathEffectCache.Clear();
        }
    }
}
