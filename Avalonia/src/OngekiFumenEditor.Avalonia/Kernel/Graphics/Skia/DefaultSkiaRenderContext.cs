using Avalonia.Media;
using Avalonia.Skia;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using SkiaSharp;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

public class DefaultSkiaRenderContext : IRenderContext
{
    private readonly AvaloniaSkiaRenderControl renderControl;
    private DefaultSkiaDrawingManagerImpl manager;
    private readonly object renderSync = new();
    private IPerfomenceMonitor perfomenceMonitor = DummyPerformenceMonitor.Instance;
    private long previousTimestamp;
    private volatile bool isStart;

    public event Action<IRenderContext, TimeSpan> OnRender;

    public string Name { get; set; }

    public SKCanvas Canvas { get; private set; }

    /// <inheritdoc />
    public IPerfomenceMonitor PerfomenceMonitor
    {
        get => Volatile.Read(ref perfomenceMonitor);
        set
        {
            // The UI can switch monitors while the compositor is rendering. Do not split a frame
            // across two monitors, including the backend draw calls made during replay.
            lock (renderSync)
                Volatile.Write(ref perfomenceMonitor, value ?? DummyPerformenceMonitor.Instance);
        }
    }

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
        lock (renderSync)
        {
            if (isStart)
                return;
            previousTimestamp = 0;
            perfomenceMonitor.Clear();
            isStart = true;
        }
    }

    public void StopRendering()
    {
        // Returning from Stop guarantees the current lease/replay has finished before release.
        lock (renderSync)
            isStart = false;
    }

    private void SwapAndPresentDrawCommandList()
    {
        if (manager is null)
            return;

        // Promote a newly built frame when one is queued. When the frame was throttled and
        // nothing new was posted, PresentDrawCommandList re-presents the retained front so
        // the custom draw surface is never left empty.
        manager.SwapDrawCommandList(this);
        manager.PresentDrawCommandList(this);
    }

    internal void RenderFrame(ImmediateDrawingContext drawingContext)
    {
        lock (renderSync)
        {
            if (!isStart)
                return;

            if (drawingContext.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature leaseFeature)
                throw new NotSupportedException("The active Avalonia renderer does not expose the SkiaSharp lease feature.");

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            var saveCount = canvas.Save();
            try
            {
                canvas.ClipRect(
                    SKRect.Create((float)renderControl.Bounds.Width, (float)renderControl.Bounds.Height),
                    SKClipOperation.Intersect,
                    antialias: false);
                Canvas = canvas;

                var timestamp = Stopwatch.GetTimestamp();
                var elapsed = previousTimestamp == 0
                    ? TimeSpan.Zero
                    : Stopwatch.GetElapsedTime(previousTimestamp, timestamp);
                previousTimestamp = timestamp;

                var monitor = perfomenceMonitor;
                monitor.OnBeforeRender();
                try
                {
                    OnRender?.Invoke(this, elapsed);
                }
                finally
                {
                    monitor.OnAfterRender();
                }

                SwapAndPresentDrawCommandList();
            }
            finally
            {
                Canvas = null;
                canvas.RestoreToCount(saveCount);
            }
        }
    }
}
