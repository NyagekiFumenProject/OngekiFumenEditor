using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;

public struct RenderPerformenceStatisticsData : IRenderPerformenceStatisticsData
{
    public long CurrentFrameSpendTicks { get; init; }
    public double AveFrameSpendTicks { get; init; }
    public double AveFrameFps => ToFps(AveFrameSpendTicks);
    public long CurrentOnRenderSpendTicks { get; init; }
    public double AveOnRenderSpendTicks { get; init; }
    public double AveOnRenderFps => ToFps(AveOnRenderSpendTicks);
    public long CurrentPresentSpendTicks { get; init; }
    public double AvePresentSpendTicks { get; init; }
    public double AvePresentFps => ToFps(AvePresentSpendTicks);
    public double AveDrawCall { get; init; }

    private static double ToFps(double ticks) => ticks > 0 ? TimeSpan.TicksPerSecond / ticks : 0;
}
