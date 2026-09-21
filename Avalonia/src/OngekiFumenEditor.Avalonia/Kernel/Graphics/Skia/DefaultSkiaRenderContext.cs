using Avalonia;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Threading;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using SkiaSharp;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

public class DefaultSkiaRenderContext : IRenderContext
{
    private DefaultSkiaDrawingManagerImpl manager;
    private readonly object renderSync = new();
    private readonly Action invalidateVisual;
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
            // Keep each command-construction or presentation pass on one monitor,
            // including backend draw calls made during replay. A monitor must never
            // carry samples from an earlier context or attachment.
            lock (renderSync)
            {
                value?.Clear();
                Volatile.Write(ref perfomenceMonitor, value ?? DummyPerformenceMonitor.Instance);
            }
        }
    }

    internal bool IsRendering => isStart;

    internal DefaultSkiaRenderContext(Action invalidateVisual)
    {
        this.invalidateVisual = invalidateVisual;
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

        // Loaded can start rendering after the control's initial (stopped) paint.
        RequestFrame();
    }

    internal void RequestFrame() => Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);

    public void StopRendering()
    {
        // Returning from Stop guarantees the current lease/replay has finished before release.
        lock (renderSync)
        {
            // Statistics must not outlive the rendering they describe.
            perfomenceMonitor.Clear();
            isStart = false;
        }
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

    internal void PrepareFrame()
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (renderSync)
        {
            if (!isStart)
                return;

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
        }
    }

    internal void RenderFrame(ImmediateDrawingContext drawingContext, Size size)
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
                    SKRect.Create((float)size.Width, (float)size.Height),
                    SKClipOperation.Intersect,
                    antialias: false);
                Canvas = canvas;
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
