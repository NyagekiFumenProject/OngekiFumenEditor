using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.CircleDrawing;

public class DefaultSkiaCircleDrawing : CommonSkiaDrawingBase, ICircleDrawing
{
    public DefaultSkiaCircleDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Begin(IDrawingContext target)
    {
    }

    public void Post(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth)
    {
    }

    public void End()
    {
    }
}
