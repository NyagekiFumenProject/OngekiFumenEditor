using System;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using SkiaSharp;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// 离屏渲染内部使用的 <see cref="ISkiaRenderContext"/> 适配器。
    /// 只承担「给重放提供画布与监视器」这一件事，不参与控件绘制周期：
    /// <see cref="OnRender"/> 永不触发，渲染循环相关成员不可用。
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
            throw new NotSupportedException("离屏渲染上下文不接受延迟提交，请使用 IOffscreenRenderContext.RenderToImageAsync()。");
        }

        /// <inheritdoc />
        public void StartRendering()
        {
            throw new NotSupportedException("离屏渲染上下文没有渲染循环。");
        }

        /// <inheritdoc />
        public void StopRendering()
        {
            throw new NotSupportedException("离屏渲染上下文没有渲染循环。");
        }
    }
}
