using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using System.Linq.Expressions;
using System.Reflection;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// DrawPlayableAreaHelper_new 采样链路的分配验证（性能报告 §8 P2 的落地验证）。
///
/// 改造前每采样分配 2×TGrid(56 B)：<c>BuildAreaSample</c> 与 <c>ConvertToLimitParam</c> 各一次
/// <c>TGrid.FromTotalGrid</c>，<c>AddScreenDistanceSamples</c> 每相邻对再 2 次；无效路径墙轨还会走
/// <c>GetChildObjectsFromTGrid</c>(List/数组) + <c>CalulateXGrid</c>(XGrid)。
/// 现在 helper 复用同一个 <c>sampleTGrid</c>（IsNotifying=false），无效路径改索引扫描 + TryCalulateXGridTotalUnit。
///
/// Setup 内的等价性断言（任何不一致直接抛异常）：
///  1) 复用写法与 TGrid.FromTotalGrid 的 (Unit, Grid, TotalGrid, TotalUnit) 位级一致（扫描 + 边界值 + 随机值）；
///  2) 采样链（物化 vs 复用）累加和位级一致；
///  3) 无效路径下，反射直接调用已落地的私有方法 CalculateBoundaryXGridUnitLegacy，与旧实现逐值一致。
///
/// 注：本类自建墙轨（不依赖 IoC），一次操作 = 一遍采样循环（PrimitiveCount 由 Params 控制）。
/// </summary>
[MemoryDiagnoser]
public class PlayfieldAreaSamplingAllocationBenchmarks
{
    private const double DefaultLeftXGridUnit = -24;
    private const double DefaultRightXGridUnit = 24;
    private const int ChildrenPerLane = 32;

    [Params(1, 4, 16)]
    public int CandidateCount { get; set; }

    [Params(256, 1024)]
    public int SampleCount { get; set; }

    private int[] sampleTotals = Array.Empty<int>();
    private Candidate[] leftCandidates = Array.Empty<Candidate>();
    private Candidate[] rightCandidates = Array.Empty<Candidate>();
    private LaneStartBase invalidPathLane = null!;
    private readonly TGrid sampleTGrid = new TGrid { IsNotifying = false };

    private delegate double? BoundaryEdgeCalculator(LaneStartBase lane, TGrid tGrid, int edge);

    private BoundaryEdgeCalculator shippedInvalidPathCalculator = null!;

    private readonly struct Candidate
    {
        public Candidate(LaneStartBase lane)
        {
            Lane = lane;
            IsPathValid = lane.IsPathVaild();
            MinTotalTGrid = lane.MinTGrid.TotalGrid;
            MaxTotalTGrid = lane.MaxTGrid.TotalGrid;
        }

        public LaneStartBase Lane { get; }
        public bool IsPathValid { get; }
        public int MinTotalTGrid { get; }
        public int MaxTotalTGrid { get; }
    }

    private readonly record struct BoundarySample(double Prev, double Next);

    [GlobalSetup]
    public void Setup()
    {
        var span = SampleCount * 8;

        var left = new LaneStartBase[CandidateCount];
        var right = new LaneStartBase[CandidateCount];
        for (var i = 0; i < CandidateCount; i++)
        {
            left[i] = BuildWallLane(isLeft: true, i, span, descending: false);
            right[i] = BuildWallLane(isLeft: false, i, span, descending: false);

            if (!left[i].IsPathVaild() || !right[i].IsPathVaild())
                throw new InvalidOperationException("有效路径样本墙轨构建失败（IsPathVaild 应为 true）。");
        }

        leftCandidates = Array.ConvertAll(left, x => new Candidate(x));
        rightCandidates = Array.ConvertAll(right, x => new Candidate(x));

        // 无效路径样本：子物体 TGrid 递减 → GenerateConnectionPaths 判定无效（toP.Y < fromP.Y）
        invalidPathLane = BuildWallLane(isLeft: true, 0, span, descending: true);
        if (invalidPathLane.IsPathVaild())
            throw new InvalidOperationException("无效路径样本墙轨构建失败（IsPathVaild 应为 false）。");

        sampleTotals = new int[SampleCount];
        for (var i = 0; i < sampleTotals.Length; i++)
            sampleTotals[i] = (int)((long)span * (i + 1) / (SampleCount + 1));

        shippedInvalidPathCalculator = BuildShippedInvalidPathCalculator();
        ValidateEquivalence();
    }

    private static LaneStartBase BuildWallLane(bool isLeft, int index, int span, bool descending)
    {
        LaneStartBase lane = isLeft ? new WallLeftStart() : new WallRightStart();
        lane.TGrid = TGrid.FromTotalGrid(0);
        lane.XGrid = XGrid.FromTotalUnit((float)(isLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit));

        for (var i = 0; i < ChildrenPerLane; i++)
        {
            var child = lane.CreateChildObject();
            child.RecordId = i;

            var ratio = descending ? (ChildrenPerLane - i) : (i + 1);
            child.TGrid = TGrid.FromTotalGrid((int)((long)span * ratio / (ChildrenPerLane + 1)) + index * 3);
            child.XGrid = XGrid.FromTotalUnit((float)((isLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit) + (isLeft ? -1 : 1) * index * 0.5 + (isLeft ? -1 : 1) * i * 0.25));
            lane.AddChildObject(child);
        }

        return lane;
    }

    /// <summary>反射拿真实私有方法（把 int 边号转成私有枚举），用表达式编译成无装箱委托。</summary>
    private static BoundaryEdgeCalculator BuildShippedInvalidPathCalculator()
    {
        var helperType = typeof(DrawPlayableAreaHelper_new);
        var edgeType = helperType.GetNestedType("BoundaryEdge", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("未找到 BoundaryEdge 枚举。");
        var method = helperType.GetMethod("CalculateBoundaryXGridUnitLegacy", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("未找到 CalculateBoundaryXGridUnitLegacy。");

        var laneParam = Expression.Parameter(typeof(LaneStartBase), "lane");
        var tGridParam = Expression.Parameter(typeof(TGrid), "tGrid");
        var edgeParam = Expression.Parameter(typeof(int), "edge");

        var body = Expression.Call(method, laneParam, tGridParam, Expression.Convert(edgeParam, edgeType));
        return Expression.Lambda<BoundaryEdgeCalculator>(body, laneParam, tGridParam, edgeParam).Compile();
    }

    /// <summary>复用采样 TGrid 的原地写法（与 helper 的 GetSampleTGrid 相同）。</summary>
    private TGrid SampleTGridAt(int totalTGrid)
    {
        sampleTGrid.Unit = 0;
        sampleTGrid.Grid = totalTGrid;
        sampleTGrid.NormalizeSelf();
        return sampleTGrid;
    }

    private static double Consume(TGrid tGrid) => tGrid.TotalUnit * 0.25 + tGrid.TotalGrid;

    // ---- 采样链（BuildAreaSample + ConvertToLimitParam 形状） ----

    [Benchmark(Baseline = true)]
    [STAThread]
    public double SamplingChain_Current_Materialized()
    {
        var acc = 0d;
        for (var i = 0; i < sampleTotals.Length; i++)
        {
            var total = sampleTotals[i];
            var tGrid = TGrid.FromTotalGrid(total);
            var left = QueryBoundaryXGridUnit(leftCandidates, LaneType.WallLeft, tGrid);
            var right = QueryBoundaryXGridUnit(rightCandidates, LaneType.WallRight, tGrid);
            var y = Consume(TGrid.FromTotalGrid(total));

            acc += (left?.Prev ?? 0) + (left?.Next ?? 0) + (right?.Prev ?? 0) + (right?.Next ?? 0) + y;
        }

        return acc;
    }

    [Benchmark]
    [STAThread]
    public double SamplingChain_Reuse_SampleTGrid()
    {
        var acc = 0d;
        for (var i = 0; i < sampleTotals.Length; i++)
        {
            var total = sampleTotals[i];
            var tGrid = SampleTGridAt(total);
            var left = QueryBoundaryXGridUnit(leftCandidates, LaneType.WallLeft, tGrid);
            var right = QueryBoundaryXGridUnit(rightCandidates, LaneType.WallRight, tGrid);
            var y = Consume(SampleTGridAt(total));

            acc += (left?.Prev ?? 0) + (left?.Next ?? 0) + (right?.Prev ?? 0) + (right?.Next ?? 0) + y;
        }

        return acc;
    }

    // ---- AddScreenDistanceSamples 形状 ----

    [Benchmark]
    [STAThread]
    public double ScreenDistance_Current_Materialized()
    {
        var acc = 0d;
        for (var i = 0; i < sampleTotals.Length - 1; i++)
        {
            var fromY = Consume(TGrid.FromTotalGrid(sampleTotals[i]));
            var toY = Consume(TGrid.FromTotalGrid(sampleTotals[i + 1]));
            acc += Math.Abs(toY - fromY);
        }

        return acc;
    }

    [Benchmark]
    [STAThread]
    public double ScreenDistance_Reuse_SampleTGrid()
    {
        var acc = 0d;
        for (var i = 0; i < sampleTotals.Length - 1; i++)
        {
            var fromY = Consume(SampleTGridAt(sampleTotals[i]));
            var toY = Consume(SampleTGridAt(sampleTotals[i + 1]));
            acc += Math.Abs(toY - fromY);
        }

        return acc;
    }

    // ---- 无效路径墙轨：旧实现 vs 已落地实现 ----

    [Benchmark]
    [STAThread]
    public double InvalidPathBoundary_ListPlusXGrid()
    {
        var acc = 0d;
        for (var i = 0; i < sampleTotals.Length; i++)
        {
            var tGrid = TGrid.FromTotalGrid(sampleTotals[i]);
            acc += LegacyInvalidPathCalculator(invalidPathLane, tGrid, 0) ?? 0;
            acc += LegacyInvalidPathCalculator(invalidPathLane, tGrid, 1) ?? 0;
        }

        return acc;
    }

    [Benchmark]
    [STAThread]
    public double InvalidPathBoundary_IndexScanShipped()
    {
        var acc = 0d;
        for (var i = 0; i < sampleTotals.Length; i++)
        {
            var tGrid = TGrid.FromTotalGrid(sampleTotals[i]);
            acc += shippedInvalidPathCalculator(invalidPathLane, tGrid, 0) ?? 0;
            acc += shippedInvalidPathCalculator(invalidPathLane, tGrid, 1) ?? 0;
        }

        return acc;
    }

    /// <summary>旧实现逐行复刻（List + CalulateXGrid），仅用于等价对拍与分配对照。</summary>
    private static double? LegacyInvalidPathCalculator(LaneStartBase lane, TGrid tGrid, int edge)
    {
        var children = lane.GetChildObjectsFromTGrid(tGrid);
        var isPathValid = lane.IsPathVaild();
        var childCount = 0;
        ConnectableChildObjectBase? firstChild = null;
        ConnectableChildObjectBase? firstExactChild = null;
        ConnectableChildObjectBase? lastExactChild = null;
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
            var child = edge == 0 ? firstExactChild : lastExactChild;
            return child.XGrid.TotalUnit;
        }

        if (isPathValid)
            return firstChild?.CalulateXGrid(tGrid)?.TotalUnit;

        return bestValue;
    }

    // ---- 复刻 helper 的 QueryBoundaryXGridUnit（走真实 lane API） ----

    private static BoundarySample? QueryBoundaryXGridUnit(IReadOnlyList<Candidate> candidates, LaneType laneType, TGrid tGrid)
    {
        double? prev = null;
        double? next = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (IsActiveAtBoundaryEdge(candidate, tGrid.TotalGrid, prevEdge: true)
                && CalculateBoundaryXGridUnit(candidate, tGrid, prevEdge: true) is double prevValue)
            {
                prev = MergeBoundary(laneType, prev, prevValue);
            }

            if (IsActiveAtBoundaryEdge(candidate, tGrid.TotalGrid, prevEdge: false)
                && CalculateBoundaryXGridUnit(candidate, tGrid, prevEdge: false) is double nextValue)
            {
                next = MergeBoundary(laneType, next, nextValue);
            }
        }

        if (!prev.HasValue && !next.HasValue)
            return null;

        var defaultValue = laneType == LaneType.WallLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit;
        return new BoundarySample(prev ?? defaultValue, next ?? defaultValue);
    }

    private static bool IsActiveAtBoundaryEdge(Candidate candidate, int totalTGrid, bool prevEdge)
        => prevEdge
            ? candidate.MinTotalTGrid < totalTGrid && totalTGrid <= candidate.MaxTotalTGrid
            : candidate.MinTotalTGrid <= totalTGrid && totalTGrid < candidate.MaxTotalTGrid;

    private static double? CalculateBoundaryXGridUnit(Candidate candidate, TGrid tGrid, bool prevEdge)
    {
        if (!candidate.IsPathValid)
            return null;

        var lane = candidate.Lane;
        if (lane.TryGetValidPathChildRange(tGrid, out var start, out var count))
        {
            if (lane.GetChildObjectAt(start).TGrid.TotalGrid == tGrid.TotalGrid)
            {
                var child = prevEdge ? lane.GetChildObjectAt(start) : lane.GetChildObjectAt(start + count - 1);
                return child.XGrid.TotalUnit;
            }

            return lane.GetChildObjectAt(start).TryCalulateXGridTotalUnit(tGrid, out var totalUnit) ? totalUnit : null;
        }

        var x = lane.TryCalulateXGridTotalUnit(tGrid, out var fallbackTotalUnit) ? fallbackTotalUnit : lane.XGrid?.TotalUnit ?? double.NaN;
        return double.IsNaN(x) ? null : x;
    }

    private static double MergeBoundary(LaneType laneType, double? currentValue, double newValue)
        => laneType == LaneType.WallLeft
            ? (currentValue.HasValue ? Math.Min(currentValue.Value, newValue) : newValue)
            : (currentValue.HasValue ? Math.Max(currentValue.Value, newValue) : newValue);

    // ---- 等价性 ----

    private void ValidateEquivalence()
    {
        AssertSameGrid(0);
        AssertSameGrid(1);
        AssertSameGrid(1919);
        AssertSameGrid(1920);
        AssertSameGrid(1921);
        AssertSameGrid(int.MaxValue);
        AssertSameGrid(int.MinValue + 1);

        var rand = new Random(20260929);
        for (var i = 0; i < 50_000; i++)
            AssertSameGrid(rand.Next());
        for (var i = 0; i < 50_000; i++)
            AssertSameGrid(i);

        AssertSameDouble(SamplingChain_Current_Materialized(), SamplingChain_Reuse_SampleTGrid(), nameof(SamplingChain_Reuse_SampleTGrid));
        AssertSameDouble(ScreenDistance_Current_Materialized(), ScreenDistance_Reuse_SampleTGrid(), nameof(ScreenDistance_Reuse_SampleTGrid));
        AssertSameDouble(InvalidPathBoundary_ListPlusXGrid(), InvalidPathBoundary_IndexScanShipped(), nameof(InvalidPathBoundary_IndexScanShipped));
    }

    private void AssertSameGrid(int totalTGrid)
    {
        var materialized = TGrid.FromTotalGrid(totalTGrid);
        var reused = SampleTGridAt(totalTGrid);

        if (BitConverter.SingleToInt32Bits(materialized.Unit) != BitConverter.SingleToInt32Bits(reused.Unit)
            || materialized.Grid != reused.Grid
            || materialized.TotalGrid != reused.TotalGrid
            || BitConverter.DoubleToInt64Bits(materialized.TotalUnit) != BitConverter.DoubleToInt64Bits(reused.TotalUnit))
        {
            throw new InvalidOperationException($"SampleTGridAt({totalTGrid}) 与 TGrid.FromTotalGrid 不一致。");
        }
    }

    private static void AssertSameDouble(double expected, double actual, string name)
    {
        if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual))
            throw new InvalidOperationException($"{name} 数值不一致: expected={expected}, actual={actual}");
    }
}
