using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Avalonia.Utils;
using OpenTK.Mathematics;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.BeamDrawing;

public class DefaultSkiaBeamDrawing : CommonSkiaDrawingBase, IBeamDrawing
{
    public DefaultSkiaBeamDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Draw(IDrawingContext target, IImage tex, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
    {
        if (tex is not SkiaImage texture || texture.Image is null)
            return;

        OnBegin(target);

        var canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
        var height = target.CurrentDrawingTargetContext.Rect.Height;
        var alpha = MathUtils.SmoothStep(-1f, 0f, progress) * (1 - MathUtils.SmoothStep(1f, 2f, progress));
        var actualWidth = alpha * width;
        var angle = MathUtils.RadianToAngle(rotate);

        var fixedColor = color;
        fixedColor.W *= alpha;

        var colorMatrix = new float[20];
        CreateSolidColorMatrix(colorMatrix, fixedColor);

        using var colorFilter = SKColorFilter.CreateColorMatrix(colorMatrix);
        using var paint = new SKPaint { ColorFilter = colorFilter };

        var rect = new SKRect(x - actualWidth / 2, -height, x + actualWidth / 2, 2 * height);
        var pivotY = rect.MidY - judgeOffset / 2f;

        canvas.Save();
        canvas.RotateDegrees(360 - angle, rect.MidX, pivotY);
        canvas.DrawImage(texture.Image, rect, paint);
        canvas.Restore();
        target.PerfomenceMonitor.CountDrawCall(this);

        OnEnd();
    }

    private static void CreateSolidColorMatrix(Span<float> destination, Vector4 color)
    {
        ReadOnlySpan<float> matrix =
        [
            color.X, 0, 0, 0, 0,
            0, color.Y, 0, 0, 0,
            0, 0, color.Z, 0, 0,
            0, 0, 0, color.W, 0
        ];

        matrix.CopyTo(destination);
    }
}
