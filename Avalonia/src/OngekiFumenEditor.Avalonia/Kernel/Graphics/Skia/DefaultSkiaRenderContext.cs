using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

public class DefaultSkiaRenderContext : IRenderContext
{
    private bool isStart;
    private DateTime prevRenderTime;
    private CancellationTokenSource loopCts;

    public event Action<TimeSpan> OnRender;

    public SKCanvas Canvas { get; internal set; }

    public void AfterRender(IDrawingContext context)
    {
    }

    public void BeforeRender(IDrawingContext context)
    {
    }

    public void CleanRender(IDrawingContext context, Vector4 cleanColor)
    {
    }

    public void StartRendering()
    {
        if (isStart)
            return;

        isStart = true;
        prevRenderTime = DateTime.UtcNow;
        loopCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!loopCts.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var ts = now - prevRenderTime;
                prevRenderTime = now;
                OnRender?.Invoke(ts);
                await Task.Delay(16, loopCts.Token);
            }
        }, loopCts.Token);
    }

    public void StopRendering()
    {
        if (!isStart)
            return;

        isStart = false;
        loopCts?.Cancel();
        loopCts?.Dispose();
        loopCts = default;
    }
}
