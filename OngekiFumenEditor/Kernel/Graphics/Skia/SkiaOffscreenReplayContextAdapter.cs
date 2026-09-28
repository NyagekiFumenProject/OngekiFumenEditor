using System;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using SkiaSharp;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// <see cref="ISkiaRenderContext"/> adapter used internally by offscreen rendering.
    /// It does one thing only -- provide the canvas and performance monitor for replay -- and takes no part in the control
    /// drawing cycle: <see cref="OnRender"/> never fires and render-loop members are unavailable.
    /// </summary>
    internal sealed class SkiaOffscreenReplayContextAdapter : ISkiaRenderContext
    {
        private readonly SkiaOffscreenRenderContext owner;

        public SkiaOffscreenReplayContextAdapter(SkiaOffscreenRenderContext owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Name = $"Offscreen:{owner.Options.Width}x{owner.Options.Height}";
        }

        /// <inheritdoc />
        public event Action<IRenderContext, TimeSpan> OnRender
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public IPerfomenceMonitor PerfomenceMonitor { get; set; } = DummyPerformenceMonitor.Instance;

        /// <inheritdoc />
        public int LimitFPS { get; set; } = -1;

        /// <inheritdoc />
        public SKCanvas Canvas => owner.SurfaceCanvas;

        /// <inheritdoc />
        public void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true)
        {
            throw new NotSupportedException("The offscreen render context does not accept deferred submissions, use IOffscreenRenderContext.RenderToImageAsync() instead.");
        }

        /// <inheritdoc />
        public void StartRendering()
        {
            throw new NotSupportedException("The offscreen render context has no render loop.");
        }

        /// <inheritdoc />
        public void StopRendering()
        {
            throw new NotSupportedException("The offscreen render context has no render loop.");
        }
    }
}
