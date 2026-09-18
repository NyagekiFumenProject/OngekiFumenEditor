using System;
using System.Numerics;
using System.Windows;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IRenderContext
    {
        public event Action<IRenderContext, TimeSpan> OnRender;

        int LimitFPS { get; set; }

        IPerfomenceMonitor PerfomenceMonitor { get; set; }

        String Name { get; set; }

        void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true);

        void StartRendering();

        void StopRendering();
    }

}
