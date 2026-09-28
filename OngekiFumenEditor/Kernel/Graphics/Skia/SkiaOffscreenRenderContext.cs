using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using SkiaSharp;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// Skia offscreen render context: holds one raster <see cref="SKSurface"/>; renders run sequentially on the manager's
    /// offscreen render lane thread, and each render produces its own <see cref="SkiaImage"/> through <c>SKSurface.Snapshot()</c>
    /// (owned by the caller).
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
                $"Failed to create the offscreen Skia surface (Width={options.Width}, Height={options.Height}, PixelFormat={options.PixelFormat}, AlphaType={options.AlphaType}, ColorSpace={options.ColorSpace}).");
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

        /// <summary>Canvas of the current surface; only valid on the render lane thread.</summary>
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
                    throw new InvalidOperationException("The command list is being used by another render flow, or is already being released.");

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

                    // Keep the same order as the render completion path: set DisposeRequested first, then let EndPresent() do the release.
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

            // Skia semantics: already submitted renders still complete (they are not cancelled), so wait here for all requests of this context to settle.
            idle.Wait();

            surface.Dispose();
            colorSpace?.Dispose();
        }

        /// <summary>Request settlement bookkeeping; called by the render lane thread.</summary>
        internal void OnRequestFinished()
        {
            if (Interlocked.Decrement(ref pending) == 0)
                idle.Set();
        }

        /// <summary>Runs one render on the render lane thread.</summary>
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
                // Safety net: whatever happens during drawing, never leave matrix/clip state behind for the next render.
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
                _ => throw new NotSupportedException($"Unsupported offscreen color space for the Skia backend: {colorSpace}."),
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
                _ => throw new NotSupportedException($"Unsupported offscreen pixel format for the Skia backend: {options.PixelFormat}."),
            };

            var alphaType = options.AlphaType switch
            {
                OffscreenAlphaType.Premul => SKAlphaType.Premul,
                OffscreenAlphaType.Opaque => SKAlphaType.Opaque,
                OffscreenAlphaType.Unpremul => throw new NotSupportedException("Skia offscreen rendering does not support Unpremul (that alpha type is only for input images; rendering cannot output it)."),
                _ => throw new NotSupportedException($"Unsupported offscreen alpha type for the Skia backend: {options.AlphaType}."),
            };

            return new SKImageInfo(options.Width, options.Height, colorType, alphaType, colorSpace);
        }
    }
}
