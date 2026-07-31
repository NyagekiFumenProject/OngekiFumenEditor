using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using SkiaSharp;
using System.Numerics;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.LineDrawing;

internal class DefaultSkiaLineDrawing : CommonSkiaDrawingBase, ILineDrawing, ISimpleLineDrawing
{
    private SKCanvas canvas;
    private readonly List<LineVertex> postedPoints = [];
    private IDrawingContext target;

    private (Vector4 color, VertexDash dash) prevPaintParam;
    private SKPaint prevPaint;
    private float lineWidth;

    public DefaultSkiaLineDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Begin(IDrawingContext target)
    {
        OnBegin(target);
        this.target = target;
        canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
        prevPaintParam = default;
        prevPaint?.Dispose();
        prevPaint = default;
        postedPoints.Clear();
    }

    public void End()
    {
        PostDraw();
        OnEnd();

        lineWidth = default;
        canvas = default;
        target = default;
        prevPaintParam = default;
        prevPaint?.Dispose();
        prevPaint = default;
        postedPoints.Clear();
    }

    public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
    {
        Begin(target, lineWidth);
        foreach (var point in points)
            PostPoint(point);
        End();
    }

    public void Begin(IDrawingContext target, float lineWidth)
    {
        Begin(target);
        this.lineWidth = lineWidth;
    }

    public void PostPoint(Vector2 point, Vector4 color, VertexDash dash)
    {
        postedPoints.Add(new LineVertex(point, color, dash));
    }

    public void PostPoint(LineVertex vertex)
    {
        postedPoints.Add(vertex);
    }

    private SKPaint GetPaint(Vector4 color, VertexDash dash, float width)
    {
        var param = (color, dash);
        if (param == prevPaintParam && prevPaint is not null)
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
            StrokeWidth = width,
            PathEffect = dash == VertexDash.Solider ? null : SKPathEffect.CreateDash([dash.DashSize, dash.GapSize], 0)
        };

        prevPaint = paint;
        prevPaintParam = param;
        return paint;
    }

    private void PostDraw()
    {
        var itor = postedPoints.GetEnumerator();
        var points = ObjectPool.Get<List<SKPoint>>();
        points.Clear();

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
                    points.Add(cur.Point.ToSkiaSharpPoint());
                }
                else
                {
                    var paint = GetPaint(prev.Color, prev.Dash, lineWidth);
                    DrawPath(points, paint);
                    points.Clear();
                    points.Add(prev.Point.ToSkiaSharpPoint());
                    points.Add(cur.Point.ToSkiaSharpPoint());
                }

                prevParam = curParam;
                prev = cur;
            }

            if (points.Count > 0)
            {
                var paint = GetPaint(prev.Color, prev.Dash, lineWidth);
                DrawPath(points, paint);
            }
        }

        ObjectPool.Return(points);
    }

    private void DrawPath(List<SKPoint> points, SKPaint paint)
    {
        if (points.Count <= 1)
            return;

        using var path = new SKPath();
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
        target.PerfomenceMonitor.CountDrawCall(this);
    }

    private sealed class SkiaLineVboHandle : IStaticVBODrawing.IVBOHandle
    {
        public LineVertex[] Points { get; private set; }

        public float LineWidth { get; }

        public SkiaLineVboHandle(IEnumerable<LineVertex> points, float lineWidth)
        {
            Points = points.ToArray();
            LineWidth = lineWidth;
        }

        public void Dispose()
        {
            Points = null;
        }
    }

    public IStaticVBODrawing.IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth)
    {
        ArgumentNullException.ThrowIfNull(points);
        return new SkiaLineVboHandle(points, lineWidth);
    }

    public void DrawVBO(IDrawingContext target, IStaticVBODrawing.IVBOHandle vbo)
    {
        if (vbo is not SkiaLineVboHandle handle)
            throw new ArgumentException("The VBO handle was not created by the Skia line drawing implementation.", nameof(vbo));
        if (handle.Points is null)
            throw new ObjectDisposedException(nameof(vbo));

        Draw(target, handle.Points, handle.LineWidth);
    }
}
