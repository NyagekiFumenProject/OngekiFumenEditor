using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics;

public interface IDrawingContext
{
    DrawingTargetContext CurrentDrawingTargetContext { get; }
    IPerfomenceMonitor PerfomenceMonitor { get; }
    IRenderContext RenderContext { get; }
    void Render(TimeSpan ts);
}
