using System.Diagnostics;
using System.Text;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;

[RegisterTransient<IPerfomenceMonitor>]
public class DefaultReleasePerfomenceMonitor : IPerfomenceMonitor
{
    // A context has one renderer, but the panel can read/reset its monitor concurrently.
    protected readonly Lock SampleLock = new();
    protected bool IsRendering { get; private set; }
    protected bool IsPresenting { get; private set; }
    protected int CurrentDrawCall;

    private readonly PerformanceSampleWindow frameSpendTicks;
    private readonly PerformanceSampleWindow onRenderSpendTicks;
    private readonly PerformanceSampleWindow presentSpendTicks;
    private readonly PerformanceSampleWindow drawCall;
    private long previousFrameTimestamp;
    private long renderBeginTimestamp;
    private long presentBeginTimestamp;

    public DefaultReleasePerfomenceMonitor() : this(60) { }

    protected DefaultReleasePerfomenceMonitor(int sampleCount)
    {
        frameSpendTicks = new(sampleCount);
        onRenderSpendTicks = new(sampleCount);
        presentSpendTicks = new(sampleCount);
        drawCall = new(sampleCount);
    }

    public void OnBeforeRender()
    {
        lock (SampleLock)
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (previousFrameTimestamp != 0)
                frameSpendTicks.Enqueue(Stopwatch.GetElapsedTime(previousFrameTimestamp, timestamp).Ticks);
            previousFrameTimestamp = timestamp;
            renderBeginTimestamp = timestamp;
            IsRendering = true;
            OnRenderStarted();
        }
    }

    public void OnAfterRender()
    {
        lock (SampleLock)
        {
            if (!IsRendering)
                return;
            onRenderSpendTicks.Enqueue(Stopwatch.GetElapsedTime(renderBeginTimestamp).Ticks);
            OnRenderCompleted();
            IsRendering = false;
        }
    }

    public void OnBeforePresent()
    {
        lock (SampleLock)
        {
            CurrentDrawCall = 0;
            presentBeginTimestamp = Stopwatch.GetTimestamp();
            IsPresenting = true;
            OnPresentStarted();
        }
    }

    public void OnAfterPresent()
    {
        lock (SampleLock)
        {
            if (!IsPresenting)
                return;
            presentSpendTicks.Enqueue(Stopwatch.GetElapsedTime(presentBeginTimestamp).Ticks);
            drawCall.Enqueue(CurrentDrawCall);
            OnPresentCompleted();
            IsPresenting = false;
        }
    }

    public virtual void CountDrawCall()
    {
        lock (SampleLock)
        {
            if (IsPresenting)
                CurrentDrawCall++;
        }
    }

    public virtual void OnBeginDrawCommand(DrawCommand command) { }
    public virtual void OnAfterDrawCommand(DrawCommand command) { }
    public virtual void OnBeginTargetDrawing(IDrawingTarget target) { }
    public virtual void OnAfterTargetDrawing(IDrawingTarget target) { }
    protected virtual void OnRenderStarted() { }
    protected virtual void OnRenderCompleted() { }
    protected virtual void OnPresentStarted() { }
    protected virtual void OnPresentCompleted() { }
    protected virtual void ClearCategories() { }

    public virtual ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData() => DummyPerformenceMonitor.EmptyCategories;
    public virtual ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData() => DummyPerformenceMonitor.EmptyCategories;

    public IRenderPerformenceStatisticsData GetRenderPerformenceData()
    {
        lock (SampleLock)
            return new RenderPerformenceStatisticsData
            {
                CurrentFrameSpendTicks = frameSpendTicks.Current,
                AveFrameSpendTicks = frameSpendTicks.Average,
                CurrentOnRenderSpendTicks = onRenderSpendTicks.Current,
                AveOnRenderSpendTicks = onRenderSpendTicks.Average,
                CurrentPresentSpendTicks = presentSpendTicks.Current,
                AvePresentSpendTicks = presentSpendTicks.Average,
                AveDrawCall = drawCall.Average
            };
    }

    public virtual void FormatStatistics(StringBuilder builder)
    {
        lock (SampleLock)
        {
            if (onRenderSpendTicks.Count == 0 && presentSpendTicks.Count == 0)
                return;
            var render = GetRenderPerformenceData();
            builder.AppendLine($"Frame FPS avg: {render.AveFrameFps:F2} ({render.AveFrameSpendTicks / TimeSpan.TicksPerMillisecond:F2} ms avg)");
            builder.AppendLine($"OnRender FPS avg: {render.AveOnRenderFps:F2} ({render.AveOnRenderSpendTicks / TimeSpan.TicksPerMillisecond:F2} ms avg)");
            builder.AppendLine($"Present FPS avg: {render.AvePresentFps:F2} ({render.AvePresentSpendTicks / TimeSpan.TicksPerMillisecond:F2} ms avg)");
            builder.AppendLine($"DrawCall avg: {render.AveDrawCall:F1}");
        }
    }

    public void Clear()
    {
        lock (SampleLock)
        {
            frameSpendTicks.Clear();
            onRenderSpendTicks.Clear();
            presentSpendTicks.Clear();
            drawCall.Clear();
            previousFrameTimestamp = 0;
            renderBeginTimestamp = 0;
            presentBeginTimestamp = 0;
            CurrentDrawCall = 0;
            IsRendering = false;
            IsPresenting = false;
            ClearCategories();
        }
    }
}
