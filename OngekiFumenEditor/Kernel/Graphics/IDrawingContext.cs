using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IDrawingContext
    {
        DrawingTargetContext CurrentDrawingTargetContext { get; }
        IPerfomenceMonitor PerfomenceMonitor { get; }
        IRenderContext RenderContext { get; }
        void Render(TimeSpan ts);
    }
}
