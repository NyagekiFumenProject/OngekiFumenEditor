using System.Text;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor.ICategorizedPerformenceStatisticsData;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;

public sealed class DummyPerformenceMonitor : IPerfomenceMonitor
{
    /// <summary>Stateless shared default; real monitors are owned by individual render contexts.</summary>
    public static readonly DummyPerformenceMonitor Instance = new();

    internal sealed class CategorizedStatistics : ICategorizedPerformenceStatisticsData
    {
        public IEnumerable<PerformenceItem> PerformenceRanks { get; init; } = [];
        public double AveSpendTicks { get; init; }
        public double MostSpendTicks { get; init; }
    }

    internal static readonly ICategorizedPerformenceStatisticsData EmptyCategories = new CategorizedStatistics();
    private static readonly IRenderPerformenceStatisticsData emptyRender = new RenderPerformenceStatisticsData();

    public void Clear() { }
    public void CountDrawCall() { }
    public void FormatStatistics(StringBuilder builder) { }
    public ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData() => EmptyCategories;
    public ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData() => EmptyCategories;
    public IRenderPerformenceStatisticsData GetRenderPerformenceData() => emptyRender;
    public void OnBeforeRender() { }
    public void OnAfterRender() { }
    public void OnBeforePresent() { }
    public void OnAfterPresent() { }
    public void OnBeginTargetDrawing(IDrawingTarget target) { }
    public void OnAfterTargetDrawing(IDrawingTarget target) { }
    public void OnBeginDrawCommand(DrawCommand command) { }
    public void OnAfterDrawCommand(DrawCommand command) { }
}
