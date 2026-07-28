using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Base;
using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.TextureDrawing;

internal class DefaultSkiaHighlightBatchTextureDrawing : CommonSkiaDrawingBase, IHighlightBatchTextureDrawing
{
    private SkiaImage texture;
    private readonly List<(Vector2 size, Vector2 position, float rotation, Vector4 color)> list = [];
    private SKCanvas canvas;
    private IDrawingContext target;

    public DefaultSkiaHighlightBatchTextureDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Begin(IDrawingContext target, IImage texture)
    {
        OnBegin(target);
        this.texture = texture as SkiaImage;
        canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
        this.target = target;
        list.Clear();
    }

    public void Draw(IDrawingContext target, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
    {
        Begin(target, texture);
        list.AddRange(instances);
        End();
    }

    public void End()
    {
        DoDraw();
        OnEnd();

        texture = default;
        canvas = default;
        target = default;
    }

    private void DoDraw()
    {
        if (texture?.Image is null)
            return;

        using var paint = new SKPaint();
        using var maskfilter = SKMaskFilter.CreateBlur(SKBlurStyle.Inner, 5f);
        using var colorFilter = SKColorFilter.CreateColorMatrix([
            0.5f, 0.5f, 0.0f, 0.0f, 0.2f,
            0.5f, 0.5f, 0.0f, 0.0f, 0.2f,
            0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 0.0f, 0.75f, 0.0f
        ]);

        paint.MaskFilter = maskfilter;
        paint.ColorFilter = colorFilter;

        foreach (var (size, position, rotation, _) in list)
        {
            canvas.Save();
            var adjustSize = new Vector2(Math.Abs(size.X), Math.Abs(size.Y));
            canvas.Translate(position.X, position.Y);
            canvas.RotateRadians(rotation);
            canvas.Scale(Math.Sign(size.X), -1 * Math.Sign(size.Y));

            var rect = SKRect.Create(-adjustSize.X / 2, -adjustSize.Y / 2, adjustSize.X, adjustSize.Y);
            canvas.DrawImage(texture.Image, rect, paint);
            target.PerfomenceMonitor.CountDrawCall(this);
            canvas.Restore();
        }
    }

    public void PostSprite(Vector2 size, Vector2 position, float rotation, Vector4 color)
    {
        list.Add((size, position, rotation, color));
    }
}
