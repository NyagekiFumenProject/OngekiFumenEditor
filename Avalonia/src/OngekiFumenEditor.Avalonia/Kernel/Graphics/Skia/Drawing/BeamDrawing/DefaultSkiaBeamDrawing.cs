using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing;
using OpenTK.Mathematics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.BeamDrawing;

public class DefaultSkiaBeamDrawing : CommonSkiaDrawingBase, IBeamDrawing
{
    public DefaultSkiaBeamDrawing(DefaultSkiaDrawingManagerImpl manager) : base(manager)
    {
    }

    public void Draw(IDrawingContext target, IImage tex, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
    {
    }
}
