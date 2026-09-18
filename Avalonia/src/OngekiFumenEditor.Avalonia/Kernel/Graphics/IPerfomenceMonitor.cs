using System.Collections.Generic;
using System.Text;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics;

public interface IPerfomenceMonitor
{
    public interface IRenderPerformenceStatisticsData
    {
        long CurrentFrameSpendTicks { get; }
        double AveFrameSpendTicks { get; }
        double AveFrameFps { get; }
        long CurrentOnRenderSpendTicks { get; }
        double AveOnRenderSpendTicks { get; }
        double AveOnRenderFps { get; }
        long CurrentPresentSpendTicks { get; }
        double AvePresentSpendTicks { get; }
        double AvePresentFps { get; }
        double AveDrawCall { get; }
    }

    public interface ICategorizedPerformenceStatisticsData
    {
        public record PerformenceItem(string Name, double AveSpendTicks, double AveDrawCall = 0);
        IEnumerable<PerformenceItem> PerformenceRanks { get; }
        double AveSpendTicks { get; }
        double MostSpendTicks { get; }
    }

    void OnBeforeRender();
    void OnAfterRender();
    void OnBeforePresent();
    void OnAfterPresent();
    void OnBeginDrawCommand(DrawCommand command);
    void OnAfterDrawCommand(DrawCommand command);
    void OnBeginTargetDrawing(IDrawingTarget target);
    void OnAfterTargetDrawing(IDrawingTarget target);
    void CountDrawCall();
    ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData();
    ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData();
    IRenderPerformenceStatisticsData GetRenderPerformenceData();
    void FormatStatistics(StringBuilder builder);
    void Clear();
}
