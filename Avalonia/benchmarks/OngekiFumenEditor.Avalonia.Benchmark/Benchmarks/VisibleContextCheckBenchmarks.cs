using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 审计项 RND-C1（P1）—— 可见性查询每次都整体遍历 drawingContexts 容器。
///
/// 对照现状：
///   Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs
///     :265  字段声明（由 258c32003 起为 ConcurrentDictionary）
///     :699  CheckVisible(TGrid)          —— foreach (var ctx in drawingContexts.Values)
///     :710  CheckRangeVisible(TGrid,TGrid) —— foreach (var ctx in drawingContexts.Values)
///     :953  CheckRangeVisible(DrawingTargetContext,TGrid,TGrid)
///           —— 参数 context 被完全忽略，改而 foreach (drawingContexts.SelectMany(...))
///
/// 为什么慢：
///   1) 这三处是「逐个可见子物体」调用的。调用方见
///      VisibleLineVerticesQuery.cs:45,46,94、CommonLaneEditorObjectDrawingTarget.cs:44、
///      CommonHorizonalDrawingTarget.cs:82、LaneBlockerDrawingTarget.cs:143、
///      ProjectileBatchDrawTargetBase.cs:263。
///      于是单帧成本 = O(可见子物体数 × soflan 组数 × 每组可见区间数)，
///      而语义上只需要其中很小一部分信息。
///   2) 容器是 ConcurrentDictionary —— 它的 .Values 先分配一个快照数组再逐个装箱进
///      ICollection<TValue> 接口调用。只用只读枚举却付了并发容器的钱（字段本身也不再被并发访问：
///      渲染回调与指针输入都在 UI 线程，见 Drawing.cs:250-256 的注释与
///      UserInteractionActions.cs:1694 的调用链）。
///   3) :953 的重载用 SelectMany 把「组 × 区间」两层摊平成一条惰性链，
///      每次都要重新走一次迭代器状态机 + 委托调用。
///
/// 优化做了什么：
///   - 容器回退为 Dictionary<int, DrawingTargetContext>（写入点 :305/:408/:509/:511/:1211 全在渲染线程）。
///   - 把「所有组的可见区间」在帧内预合并一次（MergeAllVisibleTGridRanges，:433 已经在算同一份数据），
///     于是 CheckRangeVisible 退化为对该缓存列表的一次线性扫描，不再触碰字典。
///   - CheckVisible 直接遍历 Dictionary.Values（struct 枚举器，无快照分配、无接口派发）。
///
/// 量纲说明（重要）：
///   本基准报告的是「一次 CheckVisible / CheckRangeVisible 调用」的耗时，是**逐调用**量纲。
///   换算单帧总成本请乘上该帧的调用次数（≈ 可见子物体数）——也就是说这 ~n J 的差值
///   在真实帧里会被放大成「可见子物体数」倍。它**不是**整帧加速比，
///   只代表 RND-C1 所辖那部分成本的消除比例。
///
/// 保真度说明：
///   容器、DrawingTargetContext、SortableCollection、TGrid 全部使用**真实生产类型**
///   （Benchmark 程序集已通过 IVT 访问 internal 成员）。唯一被替换为合成物的是
///   「被检查的物体」——真实调用方传的是 lane / hold / bullet 的 TGrid，
///   这里用均匀分布的 TGrid 数组代替，因为查询本身不关心物体身份，只用到 TGrid。
///   未覆盖：Merge() 之外的区间生成路径（GetVisibleRanges_PreviewMode）与整帧绘制。
/// </summary>
[MemoryDiagnoser]
public class VisibleContextCheckBenchmarks
{
    /// <summary>模拟一帧内可见的 soflan 组数（含默认组 0）。</summary>
    [Params(1, 4, 16)]
    public int SoflanGroupCount;

    /// <summary>单个组内的可见 TGrid 区间数（Preview 模式下 soflan 会把视口切碎成多段）。</summary>
    [Params(1, 4)]
    public int RangesPerGroup;

    /// <summary>每帧被查询的物体数（≈ 可见子物体数，驱动调用次数）。</summary>
    [Params(64, 512)]
    public int QueryCount;

    private ConcurrentDictionary<int, DrawingTargetContext> concurrentContexts = new();
    private Dictionary<int, DrawingTargetContext> plainContexts = new();

    /// <summary>优化后新增：帧内预合并一次的「所有组可见区间」缓存。</summary>
    private List<(TGrid minTGrid, TGrid maxTGrid)> mergedVisibleRanges = new();

    private TGrid[] queryTGrids = Array.Empty<TGrid>();
    private (TGrid minTGrid, TGrid maxTGrid)[] queryRanges = Array.Empty<(TGrid, TGrid)>();

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        // 造 SoflanGroupCount 个组，每组 RangesPerGroup 段可见区间，整体铺在 [0, 200000) 的 TGrid 轴上。
        var groups = new List<(int group, SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid> ranges)>();
        var cursor = 0;
        for (var g = 0; g < SoflanGroupCount; g++)
        {
            var ranges = new SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid>(x => x.minTGrid);
            for (var r = 0; r < RangesPerGroup; r++)
            {
                var start = cursor;
                var end = cursor + 5000 + rng.Next(0, 20000);
                ranges.Add((TGrid.FromTotalGrid(start), TGrid.FromTotalGrid(end)));
                cursor = end + 1000;
            }
            groups.Add((g, ranges));
        }

        var maxTotalGrid = cursor + 10000;

        foreach (var (group, ranges) in groups)
        {
            var context = new DrawingTargetContext
            {
                SoflanGroupId = group,
                VisibleTGridRanges = ranges,
                CurrentTGrid = TGrid.FromTotalGrid(maxTotalGrid / 2),
            };
            concurrentContexts[group] = context;
            plainContexts[group] = context;
        }

        // 优化实现用它替代「每次现算 SelectMany」：帧内合并一次。
        mergedVisibleRanges = new List<(TGrid minTGrid, TGrid maxTGrid)>();
        foreach (var (_, ranges) in groups)
        {
            foreach (var range in ranges)
                mergedVisibleRanges.Add(range);
        }
        mergedVisibleRanges.Sort(static (a, b) => a.minTGrid.CompareTo(b.minTGrid));

        // 查询输入：一半落在可见区间内，一半落在空隙里（保证命中/未命中两条路都被走到）。
        queryTGrids = new TGrid[QueryCount];
        queryRanges = new (TGrid, TGrid)[QueryCount];
        for (var i = 0; i < QueryCount; i++)
        {
            var baseGrid = (i & 1) == 0 ? rng.Next(0, maxTotalGrid) : maxTotalGrid + rng.Next(5000, 50000);
            var tGrid = TGrid.FromTotalGrid(baseGrid);
            queryTGrids[i] = tGrid;
            queryRanges[i] = (tGrid, TGrid.FromTotalGrid(baseGrid + 1920));
        }

        // 保证两组容器内容一致（同一批 context 实例）。
        if (concurrentContexts.Count != plainContexts.Count)
            throw new InvalidOperationException("container mismatch");

        VerifyNewAndOldAgree();
    }

    /// <summary>
    /// 基准只对比耗时；如果新旧实现结果不一致，那份耗时对比就没有意义。
    /// 这里在 GlobalSetup 里对全量查询输入逐条对拍，不一致直接抛，避免产出「快但错」的数字。
    /// </summary>
    private void VerifyNewAndOldAgree()
    {
        for (var i = 0; i < queryTGrids.Length; i++)
        {
            var tGrid = queryTGrids[i];
            var oldVisible = false;
            foreach (var ctx in concurrentContexts.Values)
            {
                if (CheckVisible(ctx, tGrid))
                {
                    oldVisible = true;
                    break;
                }
            }

            var newVisible = false;
            foreach (var ctx in plainContexts.Values)
            {
                if (CheckVisible(ctx, tGrid))
                {
                    newVisible = true;
                    break;
                }
            }

            if (oldVisible != newVisible)
                throw new InvalidOperationException($"CheckVisible mismatch at index {i}.");

            var (minTGrid, maxTGrid) = queryRanges[i];
            if (CheckRangeVisible_Original(minTGrid, maxTGrid) != CheckRangeVisible_Optimized(minTGrid, maxTGrid))
                throw new InvalidOperationException($"CheckRangeVisible mismatch at index {i}.");
        }

        // 两个 Isolate 变体都只换实现手段、不改语义，结果必须等同。
        if (Isolate_ConcurrentButPremerged() != Isolate_PlainButPerGroupScan())
            throw new InvalidOperationException("Isolate variants mismatch.");
    }

    // =====================================================================
    // CheckVisible(TGrid)
    // =====================================================================

    [Benchmark(Baseline = true)]
    public int Original_CheckVisible()
    {
        var hits = 0;
        foreach (var tGrid in queryTGrids)
        {
            foreach (var ctx in concurrentContexts.Values)
            {
                if (CheckVisible(ctx, tGrid))
                {
                    hits++;
                    break;
                }
            }
        }
        return hits;
    }

    [Benchmark]
    public int Optimized_CheckVisible()
    {
        var hits = 0;
        foreach (var tGrid in queryTGrids)
        {
            foreach (var ctx in plainContexts.Values)
            {
                if (CheckVisible(ctx, tGrid))
                {
                    hits++;
                    break;
                }
            }
        }
        return hits;
    }

    // =====================================================================
    // CheckRangeVisible(TGrid, TGrid) —— 现状走 SelectMany 摊平，忽略传入 context
    // =====================================================================

    [Benchmark]
    public int Original_CheckRangeVisible()
    {
        var hits = 0;
        foreach (var (minTGrid, maxTGrid) in queryRanges)
        {
            if (CheckRangeVisible_Original(minTGrid, maxTGrid))
                hits++;
        }
        return hits;
    }

    [Benchmark]
    public int Optimized_CheckRangeVisible()
    {
        var hits = 0;
        foreach (var (minTGrid, maxTGrid) in queryRanges)
        {
            if (CheckRangeVisible_Optimized(minTGrid, maxTGrid))
                hits++;
        }
        return hits;
    }

    // =====================================================================
    // Isolate：把成本拆成「容器类型」与「SelectMany 摊平」两部分，
    // 用来回答「到底是回退 Dictionary 贡献大，还是去掉 SelectMany 贡献大」。
    // =====================================================================

    /// <summary>只去掉 SelectMany，仍保留 ConcurrentDictionary.Values 的快照分配。</summary>
    [Benchmark]
    public int Isolate_ConcurrentButPremerged()
    {
        var hits = 0;
        foreach (var (minTGrid, maxTGrid) in queryRanges)
        {
            foreach (var ctx in concurrentContexts.Values)
            {
                if (CheckRangeVisible_Ranges(ctx.VisibleTGridRanges, minTGrid, maxTGrid))
                {
                    hits++;
                    break;
                }
            }
        }
        return hits;
    }

    /// <summary>只回退容器为 Dictionary，仍保留「每次现算」的逐组扫描（等价于现状语义但无 SelectMany）。</summary>
    [Benchmark]
    public int Isolate_PlainButPerGroupScan()
    {
        var hits = 0;
        foreach (var (minTGrid, maxTGrid) in queryRanges)
        {
            foreach (var ctx in plainContexts.Values)
            {
                if (CheckRangeVisible_Ranges(ctx.VisibleTGridRanges, minTGrid, maxTGrid))
                {
                    hits++;
                    break;
                }
            }
        }
        return hits;
    }

    // =====================================================================
    // 与生产代码逐字对应的实现体
    // =====================================================================

    private static bool CheckVisible(DrawingTargetContext context, TGrid tGrid)
    {
        foreach (var (minTGrid, maxTGrid) in context.VisibleTGridRanges)
            if (minTGrid <= tGrid && tGrid <= maxTGrid)
                return true;
        return false;
    }

    private static bool CheckRangeVisible_Ranges(
        SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid> ranges, TGrid minTGrid, TGrid maxTGrid)
    {
        foreach (var range in ranges)
        {
            var result = !(minTGrid > range.maxTGrid || maxTGrid < range.minTGrid);
            if (result)
                return true;
        }
        return false;
    }

    /// <summary>现状：Drawing.cs:951-961 —— 忽略 context，改为 SelectMany 摊平全部组。</summary>
    private bool CheckRangeVisible_Original(TGrid minTGrid, TGrid maxTGrid)
    {
        foreach (var visibleRange in SelectManyVisibleRanges(concurrentContexts))
        {
            var result = !(minTGrid > visibleRange.maxTGrid || maxTGrid < visibleRange.minTGrid);
            if (result)
                return true;
        }
        return false;
    }

    /// <summary>优化：对帧内预合并的缓存做一次线性扫描，不再触碰字典。</summary>
    private bool CheckRangeVisible_Optimized(TGrid minTGrid, TGrid maxTGrid)
    {
        var ranges = mergedVisibleRanges;
        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            var result = !(minTGrid > range.maxTGrid || maxTGrid < range.minTGrid);
            if (result)
                return true;
        }
        return false;
    }

    private static IEnumerable<(TGrid minTGrid, TGrid maxTGrid)> SelectManyVisibleRanges(
        ConcurrentDictionary<int, DrawingTargetContext> contexts)
    {
        foreach (var pair in contexts)
        {
            foreach (var range in pair.Value.VisibleTGridRanges)
                yield return range;
        }
    }
}
