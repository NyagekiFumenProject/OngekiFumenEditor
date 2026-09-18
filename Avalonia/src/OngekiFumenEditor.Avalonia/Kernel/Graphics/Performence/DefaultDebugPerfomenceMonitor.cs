using System.Diagnostics;
using System.Text;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor.ICategorizedPerformenceStatisticsData;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;

public sealed class DefaultDebugPerfomenceMonitor : DefaultReleasePerfomenceMonitor
{
    private const int SampleCount = 165;

    private sealed class Category(string name)
    {
        public readonly string Name = name;
        public readonly PerformanceSampleWindow SpendTicks = new(SampleCount);
        public readonly PerformanceSampleWindow DrawCalls = new(SampleCount);
        public long FrameTicks;
        public int FrameCalls;
        public int Depth;
        public long BeginTimestamp;
        public bool HasSample;

        public void Begin()
        {
            HasSample = true;
            if (Depth++ == 0)
                BeginTimestamp = Stopwatch.GetTimestamp();
        }

        public void End()
        {
            if (Depth > 0 && --Depth == 0)
                FrameTicks += Stopwatch.GetElapsedTime(BeginTimestamp).Ticks;
        }

        public void ResetFrame()
        {
            FrameTicks = 0;
            FrameCalls = 0;
            Depth = 0;
            HasSample = false;
        }

        public void Publish()
        {
            if (!HasSample)
                return;
            SpendTicks.Enqueue(FrameTicks);
            DrawCalls.Enqueue(FrameCalls);
        }
    }

    private readonly Dictionary<Type, Category> commands = new();
    private readonly Dictionary<Type, Category> targets = new();
    private Category currentCommand;

    public DefaultDebugPerfomenceMonitor() : base(SampleCount) { }

    private static Category GetCategory(Dictionary<Type, Category> categories, Type type)
    {
        if (!categories.TryGetValue(type, out var category))
            categories.Add(type, category = new Category(type.Name));
        return category;
    }

    protected override void OnRenderStarted()
    {
        foreach (var category in targets.Values)
            category.ResetFrame();
    }

    protected override void OnRenderCompleted()
    {
        foreach (var category in targets.Values)
            category.Publish();
    }

    protected override void OnPresentStarted()
    {
        currentCommand = null;
        foreach (var category in commands.Values)
            category.ResetFrame();
    }

    protected override void OnPresentCompleted()
    {
        currentCommand = null;
        foreach (var category in commands.Values)
            category.Publish();
    }

    public override void OnBeginTargetDrawing(IDrawingTarget target)
    {
        lock (SampleLock)
        {
            if (IsRendering)
                GetCategory(targets, target.GetType()).Begin();
        }
    }

    public override void OnAfterTargetDrawing(IDrawingTarget target)
    {
        lock (SampleLock)
        {
            if (IsRendering && targets.TryGetValue(target.GetType(), out var category))
                category.End();
        }
    }

    public override void OnBeginDrawCommand(DrawCommand command)
    {
        lock (SampleLock)
        {
            if (!IsPresenting)
                return;
            currentCommand = GetCategory(commands, command.GetType());
            currentCommand.Begin();
        }
    }

    public override void OnAfterDrawCommand(DrawCommand command)
    {
        lock (SampleLock)
        {
            if (IsPresenting && commands.TryGetValue(command.GetType(), out var category))
                category.End();
            currentCommand = null;
        }
    }

    public override void CountDrawCall()
    {
        lock (SampleLock)
        {
            if (!IsPresenting)
                return;
            CurrentDrawCall++;
            if (currentCommand is not null)
                currentCommand.FrameCalls++;
        }
    }

    public override ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData()
    {
        lock (SampleLock)
            return Snapshot(commands);
    }

    public override ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData()
    {
        lock (SampleLock)
            return Snapshot(targets);
    }

    private static ICategorizedPerformenceStatisticsData Snapshot(Dictionary<Type, Category> categories)
    {
        var ranks = new List<PerformenceItem>(categories.Count);
        double total = 0;
        double most = 0;
        foreach (var category in categories.Values)
        {
            if (category.SpendTicks.Count == 0)
                continue;
            var average = category.SpendTicks.Average;
            total += average;
            most = Math.Max(most, category.SpendTicks.Max);
            ranks.Add(new PerformenceItem(category.Name, average, category.DrawCalls.Average));
        }
        ranks.Sort(static (a, b) => b.AveSpendTicks.CompareTo(a.AveSpendTicks));
        return new DummyPerformenceMonitor.CategorizedStatistics
        {
            PerformenceRanks = ranks,
            AveSpendTicks = ranks.Count == 0 ? 0 : total / ranks.Count,
            MostSpendTicks = most
        };
    }

    public override void FormatStatistics(StringBuilder builder)
    {
        lock (SampleLock)
        {
            base.FormatStatistics(builder);
            AppendRanks(builder, "Command", Snapshot(commands), includeDrawCalls: true);
            AppendRanks(builder, "Target", Snapshot(targets));
        }
    }

    private static void AppendRanks(StringBuilder builder, string label, ICategorizedPerformenceStatisticsData statistics, bool includeDrawCalls = false)
    {
        var index = 0;
        foreach (var item in statistics.PerformenceRanks)
        {
            builder.Append($"{label} TOP{++index}: {item.Name} {item.AveSpendTicks / TimeSpan.TicksPerMillisecond:F3} ms");
            if (includeDrawCalls)
                builder.Append($", {item.AveDrawCall:F1} dc");
            builder.AppendLine();
            if (index == 3)
                break;
        }
    }

    protected override void ClearCategories()
    {
        currentCommand = null;
        commands.Clear();
        targets.Clear();
    }
}
