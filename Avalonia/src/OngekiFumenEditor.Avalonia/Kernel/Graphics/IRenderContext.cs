using System.Numerics;

using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
namespace OngekiFumenEditor.Avalonia.Kernel.Graphics
{
    public interface IRenderContext
    {
        /// <summary>
        /// Builds the next frame on the UI thread. The compositor only replays submitted commands.
        /// </summary>
        event Action<IRenderContext, TimeSpan> OnRender;

        string Name { get; set; }

        /// <summary>
        /// Performance monitor used for both command construction and presentation on this context.
        /// A change takes effect between passes, keeping every begin/end pair on the same monitor.
        /// </summary>
        IPerfomenceMonitor PerfomenceMonitor { get; set; }

        /// <summary>
        /// Submits a command list into the back slot of this render context for subsequent compositor presentation.
        /// </summary>
        void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true);

        void StartRendering();
        void StopRendering();
    }
}
