using System.Numerics;

using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
namespace OngekiFumenEditor.Avalonia.Kernel.Graphics
{
    public interface IRenderContext
    {
        event Action<IRenderContext, TimeSpan> OnRender;

        string Name { get; set; }

        /// <summary>
        /// Performance monitor used for both command construction and presentation on this context.
        /// A change takes effect between frames, keeping every begin/end pair on the same monitor.
        /// </summary>
        IPerfomenceMonitor PerfomenceMonitor { get; set; }

        /// <summary>
        /// Submits a command list into the back slot of this render context; it is presented after the current frame callback returns.
        /// </summary>
        void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true);

        void StartRendering();
        void StopRendering();
    }
}
