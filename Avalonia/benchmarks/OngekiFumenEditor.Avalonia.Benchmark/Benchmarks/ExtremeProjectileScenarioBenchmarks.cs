using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 审计项 RND-008（P2）—— 在**代码构造的极端谱面**上对比「原实现」与「各种改法」，
/// 并对物件位置计算做极其频繁、全面的逐量对比（含预览模式 Y 映射）。
///
/// 被对比的实现（都复刻生产形状）：
///   原实现        ViewModels/FumenVisualEditorViewModel.Drawing.cs:532-533 全量取 [curTGrid, TGrid.MaxValue]
///                 + TargetImpl/.../ProjectileBatchDrawTargetBase.cs:215-335 的 _Draw 裁剪/位置计算
///                 + :310 逐项 lane 查询、:350 Parallel.ForEach(DOP = Max(2, ProcessorCount-2))
///   改法1(朴素)   预筛选上界 = curTGrid + (判定线到视口底)/scale（假设 spd=1、用默认组）
///   改法1(安全)   预筛选上界 = max over soflan 组 的反解 Y：ΔY ≤ (rectMaxY-baseY)/(spd_min*scale)
///                 即按「场景内最慢弹速」放大窗口，并逐段反解 TGrid（能处理负速/倒车/静止段）
///   改法2         帧内 TGrid→EnemyLane 缓存（逐项缓存命中 + 逐帧建缓存两种量纲）
///   改法3(并发)   并行区内改读帧内不可变数据；脏树的重复重建由「先喂一次」收敛为一次
///
/// 量纲说明：
///   *_Position / *_MissedDraws / PositionCompare_* 是**整帧**（一次调用 = 一个播放位置处理完所有弹丸）；
///   Original_LaneQueryPerItem / Fix2_CachedLookupPerItem 是**逐项**；DirtyStorm_* 是**逐帧**。
///   全部不是整帧渲染加速比：本项只覆盖 RND-008 所辖的预筛选、lane 查询、位置计算。
///
/// 保真度与边界（重要）：
///   1. Y 映射直接调用生产的 <c>TGridCalculator.ConvertTGridUnitToY_PreviewMode</c>，soflan 缓存即
///      <c>SoflanList.GetCachedSoflanPositionList_PreviewMode</c> —— 与生产同一条链路（含负速 soflan 的符号语义）。
///   2. X 方向裁剪（`rectMinX..rectMaxX` 与 <c>ConvertXGridToX</c>）**未建模**：它只会进一步剔除物件，
///      因此本模型判定「该画」的集合是生产的**超集** —— 「收紧后是否漏画」的结论因此偏保守（更严格）。
///   3. lane 的 x 用真实 <c>EnemyLaneStart.CalulateXGrid(TGrid)</c>，但不做屏幕 X 投影（同上，超集）。
///   4. 视口取固定 (height=900, judgeOffsetY=200, scale=1)，即只看「Y 方向窗口」这一个自由维度；
///      场景本身负责把弹速/变速/组数/密度/BPM 推到极端。
/// </summary>
[MemoryDiagnoser]
public class ExtremeProjectileScenarioBenchmarks
{
    [ParamsSource(nameof(Scenarios))]
    public string Scenario = null!;

    public static IEnumerable<string> Scenarios => ExtremeFumenFactory.ScenarioNames;

    private ExtremeFumen fumen = null!;
    private TGrid playhead;
    private TGrid visibleMin, visibleMax;
    private int dop;
    private int parallelCountLimit;
    private ParallelOptions parallelOptions = null!;
    private EnemyLaneStart?[] directLaneOf = null!;
    private SoflanPointList[] groupPoints = null!;

    /// <summary>收紧范围的安全余量（1.05 = 多留 5%）。</summary>
    private const double SafetyMargin = 1.05;

    private int stormExceptions;
    private int stormCalls;

    // soflan 位置点列表（与生产同源）；索引 = group id
    private sealed record SoflanPointList(IList<SoflanList.SoflanPoint> Points);

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntime.EnsureInitialized();

        dop = Math.Max(2, Environment.ProcessorCount - 2);
        parallelCountLimit = OngekiFumenEditor.Avalonia.Models.Settings.EditorGlobalSetting.Default.ParallelCountLimit;
        parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = dop };

        fumen = ExtremeFumenFactory.Build(Scenario);

        // 预热并缓存每组的 soflan 位置点（生产亦然：GetCachedSoflanPositionList_PreviewMode）
        var groups = fumen.Groups.Keys.OrderBy(x => x).ToArray();
        groupPoints = new SoflanPointList[groups.Max() + 1];
        foreach (var g in groups)
            groupPoints[g] = new SoflanPointList(fumen.GroupList(g).GetCachedSoflanPositionList_PreviewMode(fumen.Fumen.BpmList));

        playhead = TGrid.FromTotalGrid(fumen.MaxTGrid.TotalGrid / 2);
        visibleMin = TGrid.FromTotalGrid(fumen.MaxTGrid.TotalGrid / 2 - 1920 * 8);
        visibleMax = TGrid.FromTotalGrid(fumen.MaxTGrid.TotalGrid / 2 + 1920 * 8);

        directLaneOf = new EnemyLaneStart?[fumen.Projectiles.Count];
        for (var i = 0; i < fumen.Projectiles.Count; i++)
            directLaneOf[i] = QueryEnemyLane(fumen.Projectiles[i].TGrid);

        PrintFacts();

        // 差分自检：安全规则不得漏画（否则立刻抛，避免产出「快但错」的数字）
        var missedSafe = CountMissedDraws(NarrowMaxUnit_Safe(playhead));
        if (missedSafe != 0)
            throw new InvalidOperationException($"[{Scenario}] safe narrowing missed {missedSafe} drawable projectiles.");
        if (CountMismatchesOverSweep() != 0)
            throw new InvalidOperationException($"[{Scenario}] position pipeline mismatch detected.");
    }

    // =====================================================================
    // ① 原实现 vs 改法1：预筛选范围 + 位置计算（整帧）
    // =====================================================================

    /// <summary>原实现：全量预筛选 + `_Draw` 的 Y 裁剪 + 位置计算（逐帧）。</summary>
    [Benchmark(Baseline = true)]
    public int Original_PrescreenFull_Position()
    {
        var drawn = 0;
        var cur = playhead;
        for (var i = 0; i < fumen.Projectiles.Count; i++)
        {
            var e = fumen.Projectiles[i];
            if (e.TGrid.TotalGrid < cur.TotalGrid)
                continue;                                   // 生产预筛选下界 = curTGrid
            if (ComputePosition(i, cur, out _))
                drawn++;
        }
        return drawn;
    }

    /// <summary>改法1（安全规则）：预筛选上界按「最慢弹速 + 逐组反解 Y」收紧，位置计算与上面逐字相同。</summary>
    [Benchmark]
    public int Fix1_PrescreenNarrowed_Position()
    {
        var drawn = 0;
        var cur = playhead;
        var maxUnit = NarrowMaxUnit_Safe(cur);
        for (var i = 0; i < fumen.Projectiles.Count; i++)
        {
            var e = fumen.Projectiles[i];
            var u = e.TGrid.TotalUnit;
            if (u < cur.TotalUnit || u > maxUnit)
                continue;
            if (ComputePosition(i, cur, out _))
                drawn++;
        }
        return drawn;
    }

    /// <summary>改法1（安全规则）在本帧漏画的物件的数量（必须为 0）。</summary>
    [Benchmark]
    public int Fix1_MissedDraws_Safe() => CountMissedDraws(NarrowMaxUnit_Safe(playhead));

    /// <summary>改法1（朴素规则：假设 spd=1、单组）漏画的物件数量 —— 该数字就是边缘条件失效的规模。</summary>
    [Benchmark]
    public int Fix1_MissedDraws_Naive() => CountMissedDraws(NarrowMaxUnit_Naive(playhead));

    /// <summary>
    /// 极其频繁全面的位置对比：sweep 全部播放位置 × 全部弹丸，比较
    /// (是否该画, timeY 精确值, lane 解析结果) 与帧内 lane 缓存的一致性，返回不一致数（必须为 0）。
    /// </summary>
    [Benchmark]
    public int PositionCompare_Mismatch() => CountMismatchesOverSweep();

    // =====================================================================
    // ② 改法2：lane 查询缓存
    // =====================================================================

    /// <summary>原实现：逐枚弹丸一次 lane 区间查询（生产 :310 表达式）。</summary>
    [Benchmark]
    public int Original_LaneQueryPerItem()
    {
        var hits = 0;
        for (var i = 0; i < fumen.Projectiles.Count; i++)
            if (QueryEnemyLane(fumen.Projectiles[i].TGrid) is not null)
                hits++;
        return hits;
    }

    /// <summary>改法2：帧内缓存命中路径（逐项量纲）。</summary>
    [Benchmark]
    public int Fix2_CachedLookupPerItem()
    {
        var hits = 0;
        for (var i = 0; i < fumen.Projectiles.Count; i++)
            if (directLaneOf[i] is not null)
                hits++;
        return hits;
    }

    /// <summary>改法2 的真实形状：帧内建一个字典，未命中才算（整帧量纲，含字典分配）。</summary>
    [Benchmark]
    public int Fix2_FrameLocalCache()
    {
        var cache = new Dictionary<int, EnemyLaneStart?>();
        var hits = 0;
        for (var i = 0; i < fumen.Projectiles.Count; i++)
        {
            var t = fumen.Projectiles[i].TGrid;
            if (!cache.TryGetValue(t.TotalGrid, out var lane))
                cache[t.TotalGrid] = lane = QueryEnemyLane(t);
            if (lane is not null)
                hits++;
        }
        return hits;
    }

    // =====================================================================
    // ③ 改法3：脏树的并发重建
    // =====================================================================

    [Benchmark]
    public int DirtyStorm_CurrentShape()
    {
        var probe = MakeDirtyProbe();
        fumen.Fumen.Lanes.Add(probe);
        var lastCount = 0;
        Parallel.For(0, dop, _ =>
        {
            try { Interlocked.Exchange(ref lastCount, CountLanesFullRange()); }
            catch (NullReferenceException) { Interlocked.Increment(ref stormExceptions); }
        });
        fumen.Fumen.Lanes.Remove(probe);
        Interlocked.Increment(ref stormCalls);
        return lastCount;
    }

    [Benchmark]
    public int DirtyStorm_AfterSingleRebuild()
    {
        var probe = MakeDirtyProbe();
        fumen.Fumen.Lanes.Add(probe);
        _ = CountLanesFullRange();
        var lastCount = 0;
        Parallel.For(0, dop, _ => Interlocked.Exchange(ref lastCount, CountLanesFullRange()));
        fumen.Fumen.Lanes.Remove(probe);
        return lastCount;
    }

    [GlobalCleanup]
    public void Cleanup()
        => Console.WriteLine($"[{Scenario}] DirtyStorm_CurrentShape: {stormCalls} 次调用共 {stormExceptions} 次 NullReferenceException");

    // =====================================================================
    // 位置计算 / 裁剪 模型（逐字对应 _Draw，仅省略 X 方向裁剪与屏幕投影）
    // =====================================================================

    private bool ComputePosition(int index, TGrid currentTGrid, out double timeY)
    {
        var e = fumen.Projectiles[index];
        var spd = e.Speed;
        var appearOffsetTime = fumen.ViewHeight / spd;
        var toTime = fumen.Y(e.TGrid, e.SoflanGroup);
        var currentTime = fumen.Y(currentTGrid, e.SoflanGroup);
        var fromTime = toTime - appearOffsetTime;
        var precent = (currentTime - fromTime) / appearOffsetTime;
        timeY = fumen.BaseY + fumen.ViewHeight * (1 - precent);

        if (timeY > fumen.RectMaxY)
            return false;
        if (timeY < fumen.RectMinY)
            return false;
        if (precent > 1 && !(visibleMin <= e.TGrid && e.TGrid <= visibleMax))
            return false;
        return true;
    }

    /// <summary>原实现（全量）会画、而被给定上界收紧后不再进入管线的物件数。</summary>
    private int CountMissedDraws(double narrowedMaxUnit)
    {
        var cur = playhead;
        var missed = 0;
        for (var i = 0; i < fumen.Projectiles.Count; i++)
        {
            var e = fumen.Projectiles[i];
            var u = e.TGrid.TotalUnit;
            if (u < cur.TotalUnit)
                continue;
            if (u <= narrowedMaxUnit)
                continue;                        // 仍在管线内
            if (ComputePosition(i, cur, out _))
                missed++;                        // 原实现会画，收紧后丢了
        }
        return missed;
    }

    /// <summary>密集 sweep：每个播放位置都比较「该画集合 + timeY + lane 解析」。</summary>
    private int CountMismatchesOverSweep()
    {
        var steps = fumen.FrameTGridCount;
        var mismatches = 0;
        for (var s = 0; s < steps; s++)
        {
            var cur = TGrid.FromTotalGrid((int)((double)fumen.MaxTGrid.TotalGrid * s / (steps - 1)));
            var narrowMaxUnit = NarrowMaxUnit_Safe(cur);

            // 帧内 lane 缓存（改法2 形状）
            var cache = new Dictionary<int, EnemyLaneStart?>();

            for (var i = 0; i < fumen.Projectiles.Count; i++)
            {
                var e = fumen.Projectiles[i];
                var t = e.TGrid;
                if (t.TotalGrid < cur.TotalGrid)
                    continue;

                var drawnByOriginal = ComputePosition(i, cur, out var timeYOriginal);
                var inNarrowed = t.TotalUnit <= narrowMaxUnit;
                var timeYFixed = double.NaN;
                var drawnByFixed = false;
                if (inNarrowed)
                    drawnByFixed = ComputePosition(i, cur, out timeYFixed);

                if (drawnByOriginal != drawnByFixed)
                    mismatches++;
                else if (drawnByOriginal && Math.Abs(timeYOriginal - timeYFixed) > 1e-9)
                    mismatches++;

                // lane 解析（决定 fromXUnit）一致性：直查 vs 帧内缓存必须命中同一个 lane 对象
                if (!cache.TryGetValue(t.TotalGrid, out var cachedLane))
                    cache[t.TotalGrid] = cachedLane = QueryEnemyLane(t);
                if (!ReferenceEquals(directLaneOf[i], cachedLane))
                    mismatches++;
            }
        }
        return mismatches;
    }

    // =====================================================================
    // 收紧范围的两条规则
    // =====================================================================

    /// <summary>朴素规则：假设 spd = 1、只看默认组 → 上界 = 反解(Y_default(cur) + (rectMaxY-baseY)/scale)。</summary>
    private double NarrowMaxUnit_Naive(TGrid cur)
    {
        var targetYUnscaled = UnscaledY(cur, 0) + (fumen.RectMaxY - fumen.BaseY) / fumen.Scale * SafetyMargin;
        return InvertY(cur, 0, targetYUnscaled);
    }

    /// <summary>
    /// 安全规则：按场景最慢弹速放大窗口（ΔY ≤ (rectMaxY-baseY)/(spd_min*scale)），
    /// 并对每个 soflan 组分别反解 TGrid（负速/静止段由 InvertY 保守覆盖），取各组上界的最大值。
    /// </summary>
    private double NarrowMaxUnit_Safe(TGrid cur)
    {
        var delta = (fumen.RectMaxY - fumen.BaseY) / (fumen.MinSpeed * fumen.Scale) * SafetyMargin;
        var best = cur.TotalUnit;
        foreach (var g in fumen.Groups.Keys)
            best = Math.Max(best, InvertY(cur, g, UnscaledY(cur, g) + delta));
        return best;
    }

    /// <summary>未缩放 Y（soflan 点空间；生产 Y = 未缩放 Y × scale）。</summary>
    private double UnscaledY(TGrid t, int group) => fumen.Y(t, group) / fumen.Scale;

    private double YOfUnit(double unit, int group)
        => fumen.YUnit(unit, group) / fumen.Scale;

    /// <summary>
    /// 反解：给出目标未缩放 Y，返回「可能落在该 Y 之下/之内的最大 TGrid unit」。
    /// 逐段二分（段内 Y 对 unit 单调，符号由 soflan speed 决定），整段都在目标以下时直接取段末，
    /// 保证是**保守上界**（宁可多取，不可漏）。
    /// </summary>
    private double InvertY(TGrid cur, int group, double targetYUnscaled)
    {
        var points = groupPoints[group].Points;
        var curUnit = cur.TotalUnit;
        var best = curUnit;

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var segStart = p.TGrid.TotalUnit;
            var segEnd = i + 1 < points.Count ? points[i + 1].TGrid.TotalUnit : fumen.MaxTGrid.TotalUnit;
            if (segEnd <= curUnit)
                continue;

            var yStart = p.Y;
            var yEnd = i + 1 < points.Count ? points[i + 1].Y : YOfUnit(segEnd, group);

            var lo = Math.Max(segStart, curUnit);
            var yLo = YOfUnit(lo, group);

            if (Math.Max(yLo, yEnd) <= targetYUnscaled)
            {
                // 整段（含其后）都在目标之下 → 取段末
                best = Math.Max(best, segEnd);
                continue;
            }
            if (Math.Min(yLo, yEnd) > targetYUnscaled)
                continue;   // 整段都在目标之上 → 不可能被画

            // 段内穿越：二分求 Y(unit) == target
            var a = lo;
            var b = segEnd;
            var increasing = yEnd >= yLo;
            for (var it = 0; it < 60; it++)
            {
                var mid = (a + b) * 0.5;
                var yMid = YOfUnit(mid, group);
                if (increasing ? yMid < targetYUnscaled : yMid > targetYUnscaled)
                    a = mid;
                else
                    b = mid;
            }
            best = Math.Max(best, (a + b) * 0.5);
        }

        return Math.Min(best, fumen.MaxTGrid.TotalUnit);
    }

    // =====================================================================

    private EnemyLaneStart? QueryEnemyLane(TGrid t)
        => fumen.Fumen.Lanes.GetVisibleStartObjects(t, t).OfType<EnemyLaneStart>().LastOrDefault();

    private int CountLanesFullRange()
    {
        var hits = 0;
        foreach (var _ in fumen.Fumen.Lanes.GetVisibleStartObjects(TGrid.Zero, fumen.MaxTGrid))
            hits++;
        return hits;
    }

    private EnemyLaneStart MakeDirtyProbe() => new()
    {
        RecordId = int.MaxValue - 1 - (int)(sinkProbe++ & 1023),
        TGrid = TGrid.FromTotalGrid(fumen.MaxTGrid.TotalGrid + 1920 * 64),
    };

    private int sinkProbe;

    private void PrintFacts()
    {
        var speeds = fumen.Projectiles.Select(x => x.Speed).ToArray();
        var negativeSoflan = 0;
        foreach (var g in fumen.Groups.Keys)
            negativeSoflan += groupPoints[g].Points.Count(p => p.Speed < 0);

        var fullK = 0;
        var narrowK = 0;
        var narrowMaxUnit = NarrowMaxUnit_Safe(playhead);
        var naiveMaxUnit = NarrowMaxUnit_Naive(playhead);
        foreach (var e in fumen.Projectiles)
        {
            var u = e.TGrid.TotalUnit;
            if (u >= playhead.TotalUnit) fullK++;
            if (u >= playhead.TotalUnit && u <= narrowMaxUnit) narrowK++;
        }

        Console.WriteLine($"[{Scenario}] {fumen.Description}");
        Console.WriteLine($"  projectiles={fumen.Projectiles.Count}  lanes={fumen.Fumen.Lanes.Count}  groups={fumen.Groups.Count}  " +
                          $"bpmChanges={fumen.Fumen.BpmList.Count}  soflanPoints={groupPoints.Where(x => x is not null).Sum(x => x.Points.Count)}  负速段={negativeSoflan}");
        Console.WriteLine($"  弹速 min={speeds.Min():F3} max={speeds.Max():F1}（场景 MinSpeed={fumen.MinSpeed}）  " +
                          $"ParallelCountLimit={parallelCountLimit} DOP={dop}");
        Console.WriteLine($"  @playhead(50%): K(full)={fullK}  K(safe-narrowed)={narrowK}  " +
                          $"上界占比 safe={narrowMaxUnit / fumen.MaxTGrid.TotalUnit:P1} naive={naiveMaxUnit / fumen.MaxTGrid.TotalUnit:P1}");
        Console.WriteLine($"  漏画自检：safe={CountMissedDraws(narrowMaxUnit)}  naive={CountMissedDraws(naiveMaxUnit)}（naive 非 0 即说明边缘条件失效）");
    }
}
