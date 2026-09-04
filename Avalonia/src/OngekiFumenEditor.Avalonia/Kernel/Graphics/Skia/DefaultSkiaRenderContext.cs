using Avalonia.Media;
using Avalonia.Skia;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using SkiaSharp;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

public class DefaultSkiaRenderContext : IRenderContext
{
    private readonly AvaloniaSkiaRenderControl renderControl;
    private DefaultSkiaDrawingManagerImpl manager;
    private int frameInProgress;
    private long previousTimestamp;
    private volatile bool isStart;

    public event Action<IRenderContext, TimeSpan> OnRender;

    public SKCanvas Canvas { get; private set; }

    internal bool IsRendering => isStart;

    internal DefaultSkiaRenderContext(AvaloniaSkiaRenderControl renderControl)
    {
        this.renderControl = renderControl;
    }

    internal void AttachManager(DefaultSkiaDrawingManagerImpl drawingManager)
    {
        manager = drawingManager;
    }

    public void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true)
    {
        if (manager is null)
            throw new InvalidOperationException("The render context has not been attached to a render manager yet.");

        manager.PostDrawCommandList(this, drawCommandList, autoDispose);
    }

    public void StartRendering()
    {
        previousTimestamp = 0;
        isStart = true;
    }

    public void StopRendering()
    {
        isStart = false;
    }

    private void SwapAndPresentDrawCommandList()
    {
        if (manager is null)
            return;
        if (!manager.SwapDrawCommandList(this))
            return;
        manager.PresentDrawCommandList(this);
    }

    internal void RenderFrame(ImmediateDrawingContext drawingContext)
    {
        if (!isStart || Interlocked.Exchange(ref frameInProgress, 1) != 0)
            return;

        try
        {
            if (drawingContext.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature leaseFeature)
                throw new NotSupportedException("The active Avalonia renderer does not expose the SkiaSharp lease feature.");

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            var saveCount = canvas.Save();

            try
            {
                canvas.ClipRect(
                    SKRect.Create(
                        (float)renderControl.Bounds.Width,
                        (float)renderControl.Bounds.Height),
                    SKClipOperation.Intersect,
                    antialias: false);
                Canvas = canvas;

                var timestamp = Stopwatch.GetTimestamp();
                var elapsed = previousTimestamp == 0
                    ? TimeSpan.Zero
                    : Stopwatch.GetElapsedTime(previousTimestamp, timestamp);
                previousTimestamp = timestamp;
                OnRender?.Invoke(this, elapsed);
                SwapAndPresentDrawCommandList();
            }
            finally
            {
                Canvas = null;
                canvas.RestoreToCount(saveCount);
            }
        }
        finally
        {
            Volatile.Write(ref frameInProgress, 0);
        }
    }
}
