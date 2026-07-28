using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Avalonia.Utils;
using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.TextureDrawing;

internal class DefaultSkiaBatchTextureDrawing : CommonSkiaDrawingBase, IBatchTextureDrawing
{
    private SkiaImage texture;
    private readonly List<(Vector2 size, Vector2 position, float rotation, Vector4 color)> list = [];
    private SKCanvas canvas;
    private IDrawingContext target;

    public DefaultSkiaBatchTextureDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
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

        target = default;
        canvas = default;
        texture = default;
    }

    private void DoDraw()
    {
        if (texture?.Image is null)
            return;

        using var paint = new SKPaint();

        foreach (var (size, position, rotation, color) in list)
        {
            paint.Color = color.ToSKColor();

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
