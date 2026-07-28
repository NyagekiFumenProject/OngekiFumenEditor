using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics;

public interface IRenderContext
{
    event Action<TimeSpan> OnRender;

    void BeforeRender(IDrawingContext context);
    void AfterRender(IDrawingContext context);
    void CleanRender(IDrawingContext context, Vector4 cleanColor);
    void StartRendering();
    void StopRendering();
}
