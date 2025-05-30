using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Wpf;
using System;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IDrawingContext
    {
        DrawingTargetContext CurrentDrawingTargetContext { get; }

        IPerfomenceMonitor PerfomenceMonitor { get; }

        void PrepareRenderLoop(GLWpfControl glView);
        void OnRenderSizeChanged(GLWpfControl glView, SizeChangedEventArgs e);

        void Render(TimeSpan ts);
    }
}
