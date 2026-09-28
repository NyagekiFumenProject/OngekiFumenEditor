using System;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OpenTK.Graphics.OpenGL;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    /// <summary>
    /// <see cref="IRenderContext"/> adapter used internally by OpenGL offscreen rendering.
    /// It only supplies context state (such as the performance monitor) to the static replay engine and takes no part in the
    /// control drawing cycle: <see cref="OnRender"/> never fires and render-loop members are unavailable.
    /// </summary>
    internal sealed class OpenGLOffscreenReplayContextAdapter : IRenderContext
    {
        private readonly OpenGLOffscreenRenderContext owner;

        public OpenGLOffscreenReplayContextAdapter(OpenGLOffscreenRenderContext owner)
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
