using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using SkiaSharp;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// Skia 离屏渲染上下文：持有一个 raster <see cref="SKSurface"/>，渲染在 manager 的离屏渲染通道线程上顺序执行，
    /// 每次渲染通过 <c>SKSurface.Snapshot()</c> 产出一张独立的 <see cref="SkiaImage"/>（所有权归调用方）。
    /// </summary>
    internal sealed class SkiaOffscreenRenderContext : IOffscreenRenderContext
    {
        private readonly DefaultSkiaDrawingManagerImpl manager;
        private readonly SkiaOffscreenRenderLane lane;
        private readonly SKSurface surface;
        private readonly SKColorSpace colorSpace;
        private readonly SkiaOffscreenReplayContextAdapter replayContext;
        private readonly object stateGate = new();
        private readonly ManualResetEventSlim idle = new(true);
        private bool disposed;
        private int pending;

        public SkiaOffscreenRenderContext(DefaultSkiaDrawingManagerImpl manager, SkiaOffscreenRenderLane lane, OffscreenRenderOptions options)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.lane = lane ?? throw new ArgumentNullException(nameof(lane));
            Options = options ?? throw new ArgumentNullException(nameof(options));

            colorSpace = CreateColorSpace(options.ColorSpace);
            var imageInfo = CreateImageInfo(options, colorSpace);

            surface = SKSurface.Create(imageInfo) ?? throw new InvalidOperationException(
                $"无法创建离屏 Skia 表面(Width={options.Width}, Height={options.Height}, PixelFormat={options.PixelFormat}, AlphaType={options.AlphaType}, ColorSpace={options.ColorSpace})。");
            replayContext = new SkiaOffscreenReplayContextAdapter(this);
        }

        /// <inheritdoc />
        public OffscreenRenderOptions Options { get; }

        /// <inheritdoc />
        public bool IsDisposed
        {
            get
            {
                lock (stateGate)
                    return disposed;
            }
        }

        /// <summary>当前表面的画布；仅在渲染通道线程上有效。</summary>
        internal SKCanvas SurfaceCanvas => surface.Canvas;

        /// <inheritdoc />
        public Task<IImage> RenderToImageAsync(DrawCommandList drawCommandList, bool autoDispose = true, CancellationToken cancellationToken = default)
        {
            if (drawCommandList is null)
                throw new ArgumentNullException(nameof(drawCommandList));

            if (cancellationToken.IsCancellationRequested)
            {
                if (autoDispose)
                    drawCommandList.Dispose();

                return Task.FromCanceled<IImage>(cancellationToken);
            }

            lock (stateGate)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(SkiaOffscreenRenderContext));

                if (drawCommandList.IsDisposed)
                    throw new ObjectDisposedException(nameof(drawCommandList));

                if (!drawCommandList.TryBeginPresent())
                    throw new InvalidOperationException("命令列表正在被其它渲染流程使用，或已进入释放流程。");

                pending++;
                idle.Reset();

                var request = new SkiaOffscreenRenderRequest(this, drawCommandList, autoDispose, cancellationToken);
                try
                {
                    lane.Submit(request);
                }
                catch
                {
                    pending--;
                    if (pending == 0)
                        idle.Set();

                    // 与渲染完成路径保持同一顺序：先置 DisposeRequested，再由 EndPresent() 完成释放。
                    if (autoDispose)
                        drawCommandList.Dispose();

                    drawCommandList.EndPresent();

                    throw;
                }

                return request.Task;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (stateGate)
            {
                if (disposed)
                    return;

                disposed = true;
            }

            // Skia 语义：已提交的渲染照常完成（不取消），因此这里等待本上下文的所有请求结算完毕。
            idle.Wait();

            surface.Dispose();
            colorSpace?.Dispose();
        }

        /// <summary>请求结算记账；由渲染通道线程调用。</summary>
        internal void OnRequestFinished()
        {
            if (Interlocked.Decrement(ref pending) == 0)
                idle.Set();
        }

        /// <summary>在渲染通道线程上执行一次渲染。</summary>
        internal IImage RenderCore(DrawCommandList drawCommandList)
        {
            Options.EnsureViewportMatches(drawCommandList.FrameState);

            var canvas = surface.Canvas;
            var saveCount = canvas.SaveCount;

            try
            {
                manager.PresentCommands(replayContext, drawCommandList, canvas);
                return new SkiaImage(surface.Snapshot());
            }
            finally
            {
                // 兜底：无论绘制是否抛异常，都不把矩阵/clip 状态残留给下一次渲染。
                canvas.RestoreToCount(saveCount);
            }
        }

        private static SKColorSpace CreateColorSpace(OffscreenColorSpace colorSpace)
        {
            return colorSpace switch
            {
                OffscreenColorSpace.Srgb => SKColorSpace.CreateSrgb(),
                OffscreenColorSpace.SrgbLinear => SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Linear, SKColorSpaceXyz.Srgb),
                OffscreenColorSpace.DisplayP3 => SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.DisplayP3),
                OffscreenColorSpace.Null => null,
                _ => throw new NotSupportedException($"Skia 后端不支持的离屏颜色空间: {colorSpace}。"),
            };
        }

        private static SKImageInfo CreateImageInfo(OffscreenRenderOptions options, SKColorSpace colorSpace)
        {
            var colorType = options.PixelFormat switch
            {
                OffscreenPixelFormat.PlatformDefault => SKImageInfo.PlatformColorType,
                OffscreenPixelFormat.Rgba8888 => SKColorType.Rgba8888,
                OffscreenPixelFormat.Bgra8888 => SKColorType.Bgra8888,
                OffscreenPixelFormat.Rgba1010102 => SKColorType.Rgba1010102,
                OffscreenPixelFormat.RgbaF16 => SKColorType.RgbaF16,
                OffscreenPixelFormat.RgbaF32 => SKColorType.RgbaF32,
                OffscreenPixelFormat.Gray8 => SKColorType.Gray8,
                OffscreenPixelFormat.Alpha8 => SKColorType.Alpha8,
                _ => throw new NotSupportedException($"Skia 后端不支持的离屏像素格式: {options.PixelFormat}。"),
            };

            var alphaType = options.AlphaType switch
            {
                OffscreenAlphaType.Premul => SKAlphaType.Premul,
                OffscreenAlphaType.Opaque => SKAlphaType.Opaque,
                OffscreenAlphaType.Unpremul => throw new NotSupportedException("Skia 离屏渲染不支持 Unpremul（该 alpha 类型仅用于输入图像，渲染无法输出）。"),
                _ => throw new NotSupportedException($"Skia 后端不支持的离屏 alpha 类型: {options.AlphaType}。"),
            };

            return new SKImageInfo(options.Width, options.Height, colorType, alphaType, colorSpace);
        }
    }
}
