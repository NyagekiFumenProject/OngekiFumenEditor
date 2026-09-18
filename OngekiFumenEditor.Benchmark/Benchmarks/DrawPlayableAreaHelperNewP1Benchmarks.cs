using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Benchmark.Infrastructure;
using System.Globalization;
using System.IO;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 对应 docs/DrawPlayableAreaHelper_new optimization analysis.md 的 P1。
/// 使用真实嵌入 OGKR 谱面中墙轨节点最多的样本，只轻量解析 P1 需要的墙轨/BPM/Soflan 数据。
/// </summary>
public abstract class DrawPlayableAreaHelperNewP1BenchmarkBase
{
    private const double DefaultLeftXGridUnit = -24;
    private const double DefaultRightXGridUnit = 24;

    [Params(64, 256, 1024)]
    public int SamplePointCount { get; set; }

    [Params(1, 4, 16)]
    public int VisibleRangeCount { get; set; }

    private OngekiFumen fumen = null!;
    private LaneStartBase[] cachedLeftCandidates = Array.Empty<LaneStartBase>();
    private LaneStartBase[] cachedRightCandidates = Array.Empty<LaneStartBase>();
    private int[] sampleTotalTGrids = Array.Empty<int>();
    private int[] wallNodeTotalTGrids = Array.Empty<int>();
    private int[] soflanNodeTotalTGrids = Array.Empty<int>();
    private VisibleRange[] visibleRanges = Array.Empty<VisibleRange>();
    private readonly HashSet<int> sampleSink = new();

    private enum BoundaryEdge
    {
        Bef,
        Aft
    }

    private readonly record struct BoundarySample(double Bef, double Aft);

    private readonly record struct VisibleRange(TGrid Min, TGrid Max, int CurrentTotalTGrid);

    [GlobalSetup]
    public void GlobalSetup()
    {
        var selected = SampleCorpus.OgkrSamples
            .Select(x => new
            {
                Sample = x,
                Fumen = LoadMinimalOgkrFumen(x)
            })
            .Select(x => new
            {
                x.Sample,
                x.Fumen,
                WallNodeCount = CountWallNodes(x.Fumen)
            })
            .OrderByDescending(x => x.WallNodeCount)
            .FirstOrDefault(x => x.WallNodeCount > 0)
            ?? throw new InvalidOperationException("No embedded OGKR sample contains WallLeft/WallRight lanes.");

        fumen = selected.Fumen;
        wallNodeTotalTGrids = BuildWallNodeIndex(fumen);
        soflanNodeTotalTGrids = BuildSoflanNodeIndex(fumen);
        visibleRanges = BuildVisibleRanges(wallNodeTotalTGrids, VisibleRangeCount);
        sampleTotalTGrids = BuildSampleTGrids(SamplePointCount);

        var candidateMin = TGrid.FromTotalGrid(sampleTotalTGrids[0]);
        var candidateMax = TGrid.FromTotalGrid(sampleTotalTGrids[^1]);
        cachedLeftCandidates = fumen.Lanes
            .GetVisibleStartObjects(candidateMin, candidateMax)
            .Where(x => x.LaneType == LaneType.WallLeft)
            .ToArray();
        cachedRightCandidates = fumen.Lanes
            .GetVisibleStartObjects(candidateMin, candidateMax)
            .Where(x => x.LaneType == LaneType.WallRight)
            .ToArray();

        ValidateBoundaryEquivalence();
        ValidateCollectEquivalence();
    }

    protected double BoundaryCurrentFourVisibleQueriesPerSampleCore()
    {
        var checksum = 0d;
        for (var i = 0; i < sampleTotalTGrids.Length; i++)
            checksum += ConsumeBoundaryCurrent(sampleTotalTGrids[i]);
        return checksum;
    }

    protected double BoundaryCachedCandidatesCurrentCalculatorCore()
    {
        var checksum = 0d;
        for (var i = 0; i < sampleTotalTGrids.Length; i++)
            checksum += ConsumeBoundaryCached(sampleTotalTGrids[i], CalculateBoundaryXGridUnitCurrent);
        return checksum;
    }

    protected double BoundaryCachedCandidatesSinglePassNoLinqCore()
    {
        var checksum = 0d;
        for (var i = 0; i < sampleTotalTGrids.Length; i++)
            checksum += ConsumeBoundaryCached(sampleTotalTGrids[i], CalculateBoundaryXGridUnitNoLinq);
        return checksum;
    }

    protected int CollectSamplesCurrentFullWallScanCore()
    {
        var count = 0;
        for (var i = 0; i < visibleRanges.Length; i++)
        {
            sampleSink.Clear();
            var range = visibleRanges[i];
            CollectBaseSampleTGridsCurrent(range.Min, range.Max, range.CurrentTotalTGrid, sampleSink);
            count += sampleSink.Count;
        }
        return count;
    }

    protected int CollectSamplesIndexedWallAndSoflanNodesCore()
    {
        var count = 0;
        for (var i = 0; i < visibleRanges.Length; i++)
        {
            sampleSink.Clear();
            var range = visibleRanges[i];
            CollectBaseSampleTGridsIndexed(range.Min, range.Max, range.CurrentTotalTGrid, sampleSink);
            count += sampleSink.Count;
        }
        return count;
    }

    private double ConsumeBoundaryCurrent(int totalTGrid)
    {
        var tGrid = TGrid.FromTotalGrid(totalTGrid);
        var left = QueryBoundaryXGridUnitCurrent(LaneType.WallLeft, tGrid)
            ?? new BoundarySample(DefaultLeftXGridUnit, DefaultLeftXGridUnit);
        var right = QueryBoundaryXGridUnitCurrent(LaneType.WallRight, tGrid)
            ?? new BoundarySample(DefaultRightXGridUnit, DefaultRightXGridUnit);

        return left.Bef + left.Aft + right.Bef + right.Aft;
    }

    private double ConsumeBoundaryCached(int totalTGrid, Func<LaneStartBase, TGrid, BoundaryEdge, double?> calculator)
    {
        var tGrid = TGrid.FromTotalGrid(totalTGrid);
        var left = QueryBoundaryXGridUnitCached(cachedLeftCandidates, LaneType.WallLeft, tGrid, calculator);
        var right = QueryBoundaryXGridUnitCached(cachedRightCandidates, LaneType.WallRight, tGrid, calculator);
        return left.Bef + left.Aft + right.Bef + right.Aft;
    }

    private BoundarySample? QueryBoundaryXGridUnitCurrent(LaneType laneType, TGrid tGrid)
    {
        var bef = QueryBoundaryXGridUnitCurrent(laneType, tGrid, BoundaryEdge.Bef);
        var aft = QueryBoundaryXGridUnitCurrent(laneType, tGrid, BoundaryEdge.Aft);
        if (!bef.HasValue && !aft.HasValue)
            return null;

        var defaultValue = laneType == LaneType.WallLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit;
        return new BoundarySample(bef ?? defaultValue, aft ?? defaultValue);
    }

    private double? QueryBoundaryXGridUnitCurrent(LaneType laneType, TGrid tGrid, BoundaryEdge edge)
    {
        var candidates = fumen.Lanes
            .GetVisibleStartObjects(tGrid, tGrid)
            .Where(x => x.LaneType == laneType)
            .Where(x => IsActiveAtBoundaryEdge(x, tGrid, edge))
            .Select(x => CalculateBoundaryXGridUnitCurrent(x, tGrid, edge))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        return laneType == LaneType.WallLeft
            ? candidates.Min()
            : candidates.Max();
    }

    private static BoundarySample QueryBoundaryXGridUnitCached(
        IReadOnlyList<LaneStartBase> candidates,
        LaneType laneType,
        TGrid tGrid,
        Func<LaneStartBase, TGrid, BoundaryEdge, double?> calculator)
    {
        double? bef = null;
        double? aft = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            var lane = candidates[i];
            if (IsActiveAtBoundaryEdge(lane, tGrid, BoundaryEdge.Bef)
                && calculator(lane, tGrid, BoundaryEdge.Bef) is double befValue)
            {
                bef = MergeBoundary(laneType, bef, befValue);
            }

            if (IsActiveAtBoundaryEdge(lane, tGrid, BoundaryEdge.Aft)
                && calculator(lane, tGrid, BoundaryEdge.Aft) is double aftValue)
            {
                aft = MergeBoundary(laneType, aft, aftValue);
            }
        }

        var defaultValue = laneType == LaneType.WallLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit;
        return new BoundarySample(bef ?? defaultValue, aft ?? defaultValue);
    }

    private static double? CalculateBoundaryXGridUnitCurrent(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
    {
        var children = lane.GetChildObjectsFromTGrid(tGrid).ToArray();
        if (children.Length == 0)
        {
            var x = lane.CalulateXGrid(tGrid)?.TotalUnit ?? lane.XGrid?.TotalUnit ?? double.NaN;
            return double.IsNaN(x) ? null : x;
        }

        var exactChildren = children
            .Where(x => x.TGrid.TotalGrid == tGrid.TotalGrid)
            .ToArray();
        if (exactChildren.Length > 0)
        {
            return edge == BoundaryEdge.Bef
                ? exactChildren.First().XGrid.TotalUnit
                : exactChildren.Last().XGrid.TotalUnit;
        }

        if (lane.IsPathVaild())
            return children.FirstOrDefault()?.CalulateXGrid(tGrid)?.TotalUnit;

        var values = children
            .Select(x => x.CalulateXGrid(tGrid)?.TotalUnit)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        if (values.Length == 0)
            return null;

        return lane.LaneType == LaneType.WallLeft
            ? values.Min()
            : values.Max();
    }

    private static double? CalculateBoundaryXGridUnitNoLinq(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
    {
        var children = lane.GetChildObjectsFromTGrid(tGrid);
        var isPathValid = lane.IsPathVaild();
        var childCount = 0;
        OngekiFumenEditor.Base.OngekiObjects.ConnectableObject.ConnectableChildObjectBase? firstChild = null;
        OngekiFumenEditor.Base.OngekiObjects.ConnectableObject.ConnectableChildObjectBase? firstExactChild = null;
        OngekiFumenEditor.Base.OngekiObjects.ConnectableObject.ConnectableChildObjectBase? lastExactChild = null;
        double? bestValue = null;

        foreach (var child in children)
        {
            childCount++;
            firstChild ??= child;

            if (child.TGrid.TotalGrid == tGrid.TotalGrid)
            {
                firstExactChild ??= child;
                lastExactChild = child;
            }

            if (!isPathValid && child.CalulateXGrid(tGrid)?.TotalUnit is double childValue)
                bestValue = MergeBoundary(lane.LaneType, bestValue, childValue);
        }

        if (childCount == 0)
        {
            var x = lane.CalulateXGrid(tGrid)?.TotalUnit ?? lane.XGrid?.TotalUnit ?? double.NaN;
            return double.IsNaN(x) ? null : x;
        }

        if (firstExactChild is not null && lastExactChild is not null)
        {
            var child = edge == BoundaryEdge.Bef ? firstExactChild : lastExactChild;
            return child.XGrid.TotalUnit;
        }

        if (isPathValid)
            return firstChild?.CalulateXGrid(tGrid)?.TotalUnit;

        return bestValue;
    }

    private void CollectBaseSampleTGridsCurrent(TGrid minTGrid, TGrid maxTGrid, int currentTotalTGrid, ISet<int> result)
    {
        var minTotalTGrid = minTGrid.TotalGrid;
        var maxTotalTGrid = maxTGrid.TotalGrid;
        int? prevContextTotalTGrid = null;
        int? nextContextTotalTGrid = null;

        void AddSampleOrContext(int totalTGrid)
        {
            AddSampleOrContextCore(
                totalTGrid,
                minTotalTGrid,
                maxTotalTGrid,
                result,
                ref prevContextTotalTGrid,
                ref nextContextTotalTGrid);
        }

        AddSampleOrContext(minTotalTGrid);
        AddSampleOrContext(maxTotalTGrid);
        AddSampleOrContext(currentTotalTGrid);

        foreach (var lane in fumen.Lanes.Where(x => IsWallLane(x.LaneType)))
        {
            AddSampleOrContext(lane.MinTGrid.TotalGrid);
            AddSampleOrContext(lane.MaxTGrid.TotalGrid);
            AddSampleOrContext(lane.TGrid.TotalGrid);

            foreach (var child in lane.Children)
                AddSampleOrContext(child.TGrid.TotalGrid);
        }

        foreach (var point in fumen.SoflansMap.DefaultSoflanList.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList))
            AddSampleOrContext(point.TGrid.TotalGrid);

        if (prevContextTotalTGrid.HasValue)
            result.Add(prevContextTotalTGrid.Value);
        if (nextContextTotalTGrid.HasValue)
            result.Add(nextContextTotalTGrid.Value);
    }

    private void CollectBaseSampleTGridsIndexed(TGrid minTGrid, TGrid maxTGrid, int currentTotalTGrid, ISet<int> result)
    {
        var minTotalTGrid = minTGrid.TotalGrid;
        var maxTotalTGrid = maxTGrid.TotalGrid;
        int? prevContextTotalTGrid = null;
        int? nextContextTotalTGrid = null;

        AddSampleOrContextCore(
            minTotalTGrid,
            minTotalTGrid,
            maxTotalTGrid,
            result,
            ref prevContextTotalTGrid,
            ref nextContextTotalTGrid);
        AddSampleOrContextCore(
            maxTotalTGrid,
            minTotalTGrid,
            maxTotalTGrid,
            result,
            ref prevContextTotalTGrid,
            ref nextContextTotalTGrid);
        AddSampleOrContextCore(
            currentTotalTGrid,
            minTotalTGrid,
            maxTotalTGrid,
            result,
            ref prevContextTotalTGrid,
            ref nextContextTotalTGrid);

        AddIndexedSamplesOrContext(
            wallNodeTotalTGrids,
            minTotalTGrid,
            maxTotalTGrid,
            result,
            ref prevContextTotalTGrid,
            ref nextContextTotalTGrid);
        AddIndexedSamplesOrContext(
            soflanNodeTotalTGrids,
            minTotalTGrid,
            maxTotalTGrid,
            result,
            ref prevContextTotalTGrid,
            ref nextContextTotalTGrid);

        if (prevContextTotalTGrid.HasValue)
            result.Add(prevContextTotalTGrid.Value);
        if (nextContextTotalTGrid.HasValue)
            result.Add(nextContextTotalTGrid.Value);
    }

    private int[] BuildSampleTGrids(int samplePointCount)
    {
        var set = new HashSet<int>();
        foreach (var range in visibleRanges)
            CollectBaseSampleTGridsCurrent(range.Min, range.Max, range.CurrentTotalTGrid, set);

        var fillIndex = 0;
        while (set.Count < samplePointCount)
        {
            var range = visibleRanges[fillIndex % visibleRanges.Length];
            var offset = fillIndex / visibleRanges.Length;
            var span = Math.Max(1, range.Max.TotalGrid - range.Min.TotalGrid);
            var total = range.Min.TotalGrid + (int)Math.Round(span * ((offset % samplePointCount) + 1d) / (samplePointCount + 1d));
            set.Add(total);
            fillIndex++;
        }

        var sorted = set.OrderBy(x => x).ToArray();
        if (sorted.Length <= samplePointCount)
            return sorted;

        var result = new int[samplePointCount];
        for (var i = 0; i < result.Length; i++)
        {
            var idx = (int)Math.Round(i * (sorted.Length - 1d) / (result.Length - 1));
            result[i] = sorted[idx];
        }
        return result.Distinct().OrderBy(x => x).ToArray();
    }

    private void ValidateBoundaryEquivalence()
    {
        var current = BoundaryCurrentFourVisibleQueriesPerSampleCore();
        var cachedCurrentCalculator = BoundaryCachedCandidatesCurrentCalculatorCore();
        var cachedNoLinq = BoundaryCachedCandidatesSinglePassNoLinqCore();

        if (Math.Abs(current - cachedCurrentCalculator) > 0.0001d)
            throw new InvalidOperationException(
                $"Boundary cached candidate benchmark is not equivalent. Current={current}, Cached={cachedCurrentCalculator}");

        if (Math.Abs(current - cachedNoLinq) > 0.0001d)
            throw new InvalidOperationException(
                $"Boundary no-LINQ benchmark is not equivalent. Current={current}, NoLinq={cachedNoLinq}");
    }

    private void ValidateCollectEquivalence()
    {
        var current = new HashSet<int>();
        var indexed = new HashSet<int>();

        foreach (var range in visibleRanges)
        {
            current.Clear();
            indexed.Clear();

            CollectBaseSampleTGridsCurrent(range.Min, range.Max, range.CurrentTotalTGrid, current);
            CollectBaseSampleTGridsIndexed(range.Min, range.Max, range.CurrentTotalTGrid, indexed);

            if (!current.SetEquals(indexed))
                throw new InvalidOperationException("Indexed sample collection benchmark is not equivalent to the current full scan.");
        }
    }

    private static VisibleRange[] BuildVisibleRanges(IReadOnlyList<int> wallNodeTimes, int visibleRangeCount)
    {
        var min = wallNodeTimes.Count == 0 ? 0 : wallNodeTimes[0];
        var max = wallNodeTimes.Count == 0 ? (int)TGrid.DEFAULT_RES_T * 16 : wallNodeTimes[^1];
        var chartSpan = Math.Max((int)TGrid.DEFAULT_RES_T * 16, max - min);
        var rangeSpan = Math.Max((int)TGrid.DEFAULT_RES_T * 4, chartSpan / 24);
        var ranges = new VisibleRange[visibleRangeCount];

        for (var i = 0; i < ranges.Length; i++)
        {
            var center = min + (int)Math.Round((i + 0.5d) * chartSpan / visibleRangeCount);
            var rangeMin = Math.Max(0, center - rangeSpan / 2);
            var rangeMax = Math.Max(rangeMin + 1, center + rangeSpan / 2);
            ranges[i] = new VisibleRange(TGrid.FromTotalGrid(rangeMin), TGrid.FromTotalGrid(rangeMax), center);
        }

        return ranges;
    }

    private static int CountWallNodes(OngekiFumen fumen)
    {
        var count = 0;
        foreach (var lane in fumen.Lanes.Where(x => IsWallLane(x.LaneType)))
            count += 3 + lane.Children.Count();
        return count;
    }

    private static int[] BuildWallNodeIndex(OngekiFumen fumen)
    {
        var list = new List<int>();
        foreach (var lane in fumen.Lanes.Where(x => IsWallLane(x.LaneType)))
        {
            list.Add(lane.MinTGrid.TotalGrid);
            list.Add(lane.MaxTGrid.TotalGrid);
            list.Add(lane.TGrid.TotalGrid);

            foreach (var child in lane.Children)
                list.Add(child.TGrid.TotalGrid);
        }

        list.Sort();
        return list.ToArray();
    }

    private static int[] BuildSoflanNodeIndex(OngekiFumen fumen)
    {
        var list = fumen.SoflansMap.DefaultSoflanList
            .GetCachedSoflanPositionList_PreviewMode(fumen.BpmList)
            .Select(x => x.TGrid.TotalGrid)
            .ToList();
        list.Sort();
        return list.ToArray();
    }

    private static OngekiFumen LoadMinimalOgkrFumen(FumenSample sample)
    {
        var fumen = new OngekiFumen();
        var startsByRecordId = new Dictionary<int, LaneStartBase>();

        using var stream = new MemoryStream(sample.Data, writable: false);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            switch (parts[0])
            {
                case "WLS":
                    AddWallStart<WallLeftStart>(fumen, startsByRecordId, parts);
                    break;
                case "WRS":
                    AddWallStart<WallRightStart>(fumen, startsByRecordId, parts);
                    break;
                case "WLN":
                case "WLE":
                    AddWallChild<WallLeftNext>(startsByRecordId, parts);
                    break;
                case "WRN":
                case "WRE":
                    AddWallChild<WallRightNext>(startsByRecordId, parts);
                    break;
                case "SFL":
                    fumen.AddObject(ParseSoflan(parts));
                    break;
                case "BPM":
                    fumen.AddObject(ParseBpm(parts));
                    break;
            }
        }

        fumen.Setup();
        return fumen;
    }

    private static void AddWallStart<T>(
        OngekiFumen fumen,
        IDictionary<int, LaneStartBase> startsByRecordId,
        IReadOnlyList<string> parts)
        where T : LaneStartBase, new()
    {
        var recordId = ParseInt(parts[1]);
        var start = new T
        {
            RecordId = recordId,
            TGrid = ParseTGrid(parts, 2),
            XGrid = ParseXGrid(parts, 4)
        };

        startsByRecordId[recordId] = start;
        fumen.AddObject(start);
    }

    private static void AddWallChild<T>(
        IReadOnlyDictionary<int, LaneStartBase> startsByRecordId,
        IReadOnlyList<string> parts)
        where T : LaneNextBase, new()
    {
        var recordId = ParseInt(parts[1]);
        if (!startsByRecordId.TryGetValue(recordId, out var start))
            return;

        start.AddChildObject(new T
        {
            TGrid = ParseTGrid(parts, 2),
            XGrid = ParseXGrid(parts, 4)
        });
    }

    private static Soflan ParseSoflan(IReadOnlyList<string> parts)
    {
        var soflan = new Soflan
        {
            TGrid = ParseTGrid(parts, 1),
            Speed = ParseFloat(parts[4])
        };

        soflan.EndTGrid = soflan.TGrid + new GridOffset(0, ParseInt(parts[3]));
        if (parts.Count > 5)
            soflan.SoflanGroup = ParseInt(parts[5]);
        return soflan;
    }

    private static BPMChange ParseBpm(IReadOnlyList<string> parts)
    {
        return new BPMChange
        {
            TGrid = ParseTGrid(parts, 1),
            BPM = ParseFloat(parts[3])
        };
    }

    private static TGrid ParseTGrid(IReadOnlyList<string> parts, int unitIndex)
        => new(ParseFloat(parts[unitIndex]), ParseInt(parts[unitIndex + 1]));

    private static XGrid ParseXGrid(IReadOnlyList<string> parts, int unitIndex)
        => new(ParseFloat(parts[unitIndex]));

    private static float ParseFloat(string value)
        => float.Parse(value, CultureInfo.InvariantCulture);

    private static int ParseInt(string value)
        => int.Parse(value, CultureInfo.InvariantCulture);

    private static void AddIndexedSamplesOrContext(
        int[] sortedTotalTGrids,
        int minTotalTGrid,
        int maxTotalTGrid,
        ISet<int> result,
        ref int? prevContextTotalTGrid,
        ref int? nextContextTotalTGrid)
    {
        var idx = LowerBound(sortedTotalTGrids, minTotalTGrid);

        if (idx > 0)
            UpdatePrev(sortedTotalTGrids[idx - 1], ref prevContextTotalTGrid);

        while (idx < sortedTotalTGrids.Length && sortedTotalTGrids[idx] <= maxTotalTGrid)
        {
            result.Add(sortedTotalTGrids[idx]);
            idx++;
        }

        if (idx < sortedTotalTGrids.Length)
            UpdateNext(sortedTotalTGrids[idx], ref nextContextTotalTGrid);
    }

    private static int LowerBound(int[] source, int value)
    {
        var lo = 0;
        var hi = source.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (source[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private static void AddSampleOrContextCore(
        int totalTGrid,
        int minTotalTGrid,
        int maxTotalTGrid,
        ISet<int> result,
        ref int? prevContextTotalTGrid,
        ref int? nextContextTotalTGrid)
    {
        if (minTotalTGrid <= totalTGrid && totalTGrid <= maxTotalTGrid)
        {
            result.Add(totalTGrid);
            return;
        }

        if (totalTGrid < minTotalTGrid)
            UpdatePrev(totalTGrid, ref prevContextTotalTGrid);
        else
            UpdateNext(totalTGrid, ref nextContextTotalTGrid);
    }

    private static void UpdatePrev(int totalTGrid, ref int? prevContextTotalTGrid)
    {
        prevContextTotalTGrid = !prevContextTotalTGrid.HasValue || totalTGrid > prevContextTotalTGrid.Value
            ? totalTGrid
            : prevContextTotalTGrid;
    }

    private static void UpdateNext(int totalTGrid, ref int? nextContextTotalTGrid)
    {
        nextContextTotalTGrid = !nextContextTotalTGrid.HasValue || totalTGrid < nextContextTotalTGrid.Value
            ? totalTGrid
            : nextContextTotalTGrid;
    }

    private static bool IsWallLane(LaneType laneType)
        => laneType is LaneType.WallLeft or LaneType.WallRight;

    private static bool IsActiveAtBoundaryEdge(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
    {
        var totalGrid = tGrid.TotalGrid;
        return edge == BoundaryEdge.Bef
            ? lane.MinTGrid.TotalGrid < totalGrid && totalGrid <= lane.MaxTGrid.TotalGrid
            : lane.MinTGrid.TotalGrid <= totalGrid && totalGrid < lane.MaxTGrid.TotalGrid;
    }

    private static double? MergeBoundary(LaneType laneType, double? current, double value)
    {
        if (!current.HasValue)
            return value;

        return laneType == LaneType.WallLeft
            ? Math.Min(current.Value, value)
            : Math.Max(current.Value, value);
    }
}

/// <summary>
/// P1 边界查询: 对比当前每个采样点最多 4 次 GetVisibleStartObjects(t, t)
/// 与候选墙轨缓存 / 单次循环边界计算。
/// </summary>
[MemoryDiagnoser]
public class DrawPlayableAreaHelperNewP1BoundaryBenchmarks : DrawPlayableAreaHelperNewP1BenchmarkBase
{
    [Benchmark(Baseline = true)]
    [STAThread]
    public double CurrentFourVisibleQueriesPerSample()
        => BoundaryCurrentFourVisibleQueriesPerSampleCore();

    [Benchmark]
    [STAThread]
    public double CachedCandidatesCurrentCalculator()
        => BoundaryCachedCandidatesCurrentCalculatorCore();

    [Benchmark]
    [STAThread]
    public double CachedCandidatesSinglePassNoLinq()
        => BoundaryCachedCandidatesSinglePassNoLinqCore();
}

/// <summary>
/// P1 采样点收集: 对比当前每个可见区间全谱扫描墙轨节点，
/// 与预排序墙轨/Soflan 节点索引范围查询。
/// </summary>
[MemoryDiagnoser]
public class DrawPlayableAreaHelperNewP1SampleCollectionBenchmarks : DrawPlayableAreaHelperNewP1BenchmarkBase
{
    [Benchmark(Baseline = true)]
    [STAThread]
    public int CurrentFullWallScan()
        => CollectSamplesCurrentFullWallScanCore();

    [Benchmark]
    [STAThread]
    public int IndexedWallAndSoflanNodes()
        => CollectSamplesIndexedWallAndSoflanNodesCore();
}
