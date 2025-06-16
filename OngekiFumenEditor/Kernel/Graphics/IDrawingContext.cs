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

        void PrepareRenderLoop(FrameworkElement renderControl);
        void OnRenderSizeChanged(FrameworkElement renderControl, SizeChangedEventArgs e);

        void Render(TimeSpan ts);
    }
}
