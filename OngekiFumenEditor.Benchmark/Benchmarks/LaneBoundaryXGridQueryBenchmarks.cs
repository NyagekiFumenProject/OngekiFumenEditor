using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 渲染期「边界求值」链路的分配对比（性能报告 §8 P1 的落地验证）。
///
/// 每帧 VisibleLineVerticesQuery / DrawHitObjectEffectHelper 会按 soflan 边界点询问「该点的 XGrid」：
/// 旧实现每次分配 List（GetChildObjectsFromTGrid → GetRange）与 XGrid 对象（CalulateXGrid），
/// 新实现改用索引区间（TryGetValidPathChildRange/TryGetChildObjectFromTGrid）与 TotalUnit 数值返回。
///
/// 这里自建 lane（不依赖 IoC/解析器，避免宿主目录不可写时引导失败）：直轨与曲线路径共用同一套
/// CalulateXGridTotalGrid 分配行为，被测的就是上述两笔分配。一次操作 = 一个边界点的一次求值。
/// </summary>
public class LaneBoundaryXGridQueryBenchmarks
{
    private const int PointCount = 512;

    [Params(4, 16, 64)]
    public int ChildCount { get; set; }

    private ConnectableStartObject lane = null!;
    private TGrid[] points = Array.Empty<TGrid>();

    [GlobalSetup]
    public void Setup()
    {
        lane = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(0),
            XGrid = XGrid.FromTotalUnit(0),
        };

        for (var i = 0; i < ChildCount; i++)
        {
            var child = lane.CreateChildObject();
            child.RecordId = i;
            child.TGrid = TGrid.FromTotalGrid(1250 * (i + 1));
            child.XGrid = XGrid.FromTotalUnit((i & 1) == 0 ? i * 0.5f : -i * 0.5f);
            lane.AddChildObject(child);
        }

        var minTotalGrid = lane.MinTGrid.TotalGrid;
        var maxTotalGrid = Math.Max(minTotalGrid + (int)TGrid.DEFAULT_RES_T, lane.MaxTGrid.TotalGrid);

        points = new TGrid[PointCount];
        for (var i = 0; i < PointCount; i++)
        {
            var totalGrid = minTotalGrid + (int)((long)(maxTotalGrid - minTotalGrid) * i / (PointCount - 1));
            points[i] = TGrid.FromTotalGrid(totalGrid);
        }

        ValidateEquivalence();
    }

    private void ValidateEquivalence()
    {
        foreach (var tGrid in points)
        {
            var legacy = lane.CalulateXGrid(tGrid)?.TotalUnit;
            var hasValue = lane.TryCalulateXGridTotalUnit(tGrid, out var value);
            if (hasValue != legacy.HasValue
                || (hasValue && Math.Abs(value - legacy!.Value) > 1e-6 * Math.Max(1.0, Math.Abs(legacy.Value))))
                throw new InvalidOperationException($"TotalUnit 不一致: tGrid={tGrid}, legacy={legacy}, value={(hasValue ? value : (double?)null)}");

            var listChild = lane.GetChildObjectsFromTGrid(tGrid).FirstOrDefault();
            var rangeChild = lane.TryGetChildObjectFromTGrid(tGrid, out var child) ? child : null;
            if (!ReferenceEquals(listChild, rangeChild))
                throw new InvalidOperationException($"子物件定位不一致: tGrid={tGrid}, list={listChild}, range={rangeChild}");
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = PointCount)]
    [STAThread]
    public double BoundaryXGrid_Legacy()
    {
        var acc = 0d;
        for (var i = 0; i < points.Length; i++)
            acc += lane.CalulateXGrid(points[i])?.TotalUnit ?? 0;

        return acc;
    }

    [Benchmark(OperationsPerInvoke = PointCount)]
    [STAThread]
    public double BoundaryXGrid_Value()
    {
        var acc = 0d;
        for (var i = 0; i < points.Length; i++)
            if (lane.TryCalulateXGridTotalUnit(points[i], out var totalUnit))
                acc += totalUnit;

        return acc;
    }

    [Benchmark(OperationsPerInvoke = PointCount)]
    [STAThread]
    public int ChildLookup_List()
    {
        var acc = 0;
        for (var i = 0; i < points.Length; i++)
            acc += lane.GetChildObjectsFromTGrid(points[i]).FirstOrDefault()?.RecordId ?? 0;

        return acc;
    }

    [Benchmark(OperationsPerInvoke = PointCount)]
    [STAThread]
    public int ChildLookup_Range()
    {
        var acc = 0;
        for (var i = 0; i < points.Length; i++)
            if (lane.TryGetChildObjectFromTGrid(points[i], out var child))
                acc += child.RecordId;

        return acc;
    }
}
