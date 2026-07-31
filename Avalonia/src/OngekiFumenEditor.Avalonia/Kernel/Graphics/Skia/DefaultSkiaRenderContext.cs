using Avalonia.Media;
using Avalonia.Skia;
using SkiaSharp;
using System.Diagnostics;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

public class DefaultSkiaRenderContext : IRenderContext
{
    private readonly AvaloniaSkiaRenderControl renderControl;
    private int frameInProgress;
    private long previousTimestamp;
    private volatile bool isStart;

    public event Action<TimeSpan> OnRender;

    public SKCanvas Canvas { get; private set; }

    internal bool IsRendering => isStart;

    internal DefaultSkiaRenderContext(AvaloniaSkiaRenderControl renderControl)
    {
        this.renderControl = renderControl;
    }

    public void AfterRender(IDrawingContext context)
    {
        Canvas?.Restore();
    }

    public void BeforeRender(IDrawingContext context)
    {
        Canvas?.Save();
    }

    public void CleanRender(IDrawingContext context, Vector4 cleanColor)
    {
        Canvas?.Clear(new SKColorF(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W));
    }

    public void StartRendering()
    {
        if (isStart)
            return;

        isStart = true;
        previousTimestamp = Stopwatch.GetTimestamp();
        renderControl.InvalidateVisual();
    }

    public void StopRendering()
    {
        if (!isStart)
            return;

        isStart = false;
        renderControl.InvalidateVisual();
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
            Canvas = lease.SkCanvas;

            try
            {
                var timestamp = Stopwatch.GetTimestamp();
                var elapsed = previousTimestamp == 0
                    ? TimeSpan.Zero
                    : Stopwatch.GetElapsedTime(previousTimestamp, timestamp);
                previousTimestamp = timestamp;
                OnRender?.Invoke(elapsed);
            }
            finally
            {
                Canvas = null;
            }
        }
        finally
        {
            Volatile.Write(ref frameInProgress, 0);
        }
    }
}
