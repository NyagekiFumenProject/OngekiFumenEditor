using System.Numerics;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.SvgDrawing;

internal sealed class DefaultSkiaSvgDrawing : CommonSkiaDrawingBase, ISvgDrawing
{
    public DefaultSkiaSvgDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position)
    {
        if (svg.ProcessingBitmap is null)
            return;

        OnBegin(target);
        var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
        DrawToCanvas(canvas, svg, position);
        target.PerfomenceMonitor.CountDrawCall(this);
        OnEnd();
    }

    internal static void DrawToCanvas(SKCanvas canvas, SvgPrefabBase svg, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(svg);
        if (svg.ProcessingBitmap is not { } bitmap)
            return;

        var bounds = svg.SourceBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        canvas.Save();
        canvas.Translate(position.X, position.Y);
        canvas.RotateDegrees(svg.Rotation.CurrentValue);
        canvas.Scale(svg.Scale, -svg.Scale);
        canvas.Translate(-bounds.Width * svg.OffsetX.CurrentValue, -bounds.Height * svg.OffsetY.CurrentValue);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(bitmap, SKRect.Create(0, 0, bounds.Width, bounds.Height), paint);
        canvas.Restore();
    }
}
