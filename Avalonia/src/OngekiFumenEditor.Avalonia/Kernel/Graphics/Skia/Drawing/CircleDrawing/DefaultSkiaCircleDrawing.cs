using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.CircleDrawing;

public class DefaultSkiaCircleDrawing : CommonSkiaDrawingBase, ICircleDrawing
{
    private SKCanvas canvas;
    private IDrawingContext target;
    private (Vector4 color, bool isSolid, float hollowLineWidth) previousPaintParameters;
    private SKPaint previousPaint;

    public DefaultSkiaCircleDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Begin(IDrawingContext target)
    {
        OnBegin(target);

        this.target = target;
        canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
        previousPaintParameters = default;
    }

    public void Post(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth)
    {
        var paint = GetPaint(color, isSolid, hollowLineWidth);
        canvas.DrawCircle(point.X, point.Y, radius, paint);
        target.PerfomenceMonitor.CountDrawCall(this);
    }

    public void End()
    {
        OnEnd();

        target = default;
        canvas = default;
        previousPaintParameters = default;
        previousPaint?.Dispose();
        previousPaint = default;
    }

    private SKPaint GetPaint(Vector4 color, bool isSolid, float hollowLineWidth)
    {
        var parameters = (color, isSolid, hollowLineWidth);
        if (parameters == previousPaintParameters && previousPaint is not null)
            return previousPaint;

        previousPaint?.Dispose();

        previousPaint = new SKPaint
        {
            Color = new SKColor(
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255),
                (byte)(color.W * 255)),
            IsAntialias = true,
            Style = isSolid ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            StrokeWidth = hollowLineWidth
        };
        previousPaintParameters = parameters;
        return previousPaint;
    }
}
