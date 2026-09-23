using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 审计项 RND-008（P2）—— 用**真实谱面**量化三件事：
///   ① 剩余弹幕规模 K（= `BinaryFindRange(curTGrid, TGrid.MaxValue)` 到底捞了多少）；
///   ② 逐项 lane 查询成本，以及「帧内 TGrid→enemyLane 缓存」（推荐改法 2）能省多少；
///   ③ 脏树 + 并行查询时 `IntervalTree.RebuildInternal` 被重复触发的代价（推荐改法 3）。
///
/// 对照与落地：
///   ViewModels/FumenVisualEditorViewModel.Drawing.cs:532-533  预览模式按 [curTGrid, TGrid.MaxValue] 全量取
///   TargetImpl/OngekiFumenEditor.../BulletBell/ProjectileBatchDrawTargetBase.cs  逐项 lane 查询 → 帧内缓存 + 零分配 QueryInto
///   同文件 DrawPreviewMode   K ≥ ParallelCountLimit(默认 3000) 时走 Parallel.ForEach（DOP = Max(2, ProcessorCount-2)）
///   Base/Collections/Base/RangeTree/IntervalTree.cs  已按 RND-008 §8 改造：写入只改私有 staging、重建单飞后
///        「先建新图 → 原子发布」，节点不可变且不再就地 Release —— 并发只读查询安全（修复前实测 NRE 1e-4~1.6e-3 每脏帧）
///
/// 量纲说明（重要）：
///   Original_LaneQueryPerItem / Optimized_CachedLaneLookupPerItem 是**逐项**（单枚弹丸一次查询）；
///   Optimized_FrameWithFrameLocalCache 与两个 DirtyStorm_* 是**逐帧**（一帧内 K 项的全部工作）。
///   它们不是整帧加速比：帧内还有插值、可见性判定、缓冲合并、命令构造等，本项只覆盖 RND-008 所辖部分。
///
/// 保真度说明：
///   谱面来自 Data/FumenSamples（`8090_10.ogkr` / `8089_10.ogkr`），走**真实解析路径**
///   （`SampleCorpus` + `IFumenParserManager`），lane 树、弹丸 TGrid 序列、BPM 列表都是真实数据；
///   `DirtyStorm_*` 用一次对称的 `Lanes.Add`/`Remove` 制造脏标记（不改动真实谱面内容），
///   两个变体差异只在「并行查询前是否先单线程喂一次查询」。
///   未覆盖：`_Draw` 内部的几何与可见性判定、soflan 变速下的真实可见集合（推荐改法 1 的差分验证需要在编辑器内做）。
/// </summary>
[MemoryDiagnoser]
public class ProjectileBatchRealChartBenchmarks
{
    /// <summary>Data/FumenSamples 下的谱面文件名（随 [Params] 分别出数）。</summary>
    [Params("8090_10.ogkr", "8089_10.ogkr")]
    public string Chart = null!;

    private int dop;
    private int parallelCountLimit;
    private int distinctCount;
    private ParallelOptions parallelOptions = null!;

    private OngekiFumen fumen = null!;

    /// <summary>50% 播放位置之后的子弹+bell（≈ 生产 `[curTGrid, MaxValue]` 的并集）。</summary>
    private TGrid[] remainingTGrids = null!;

    /// <summary>帧内缓存（推荐改法 2），预热后用于量命中路径。</summary>
    private Dictionary<int, EnemyLaneStart?> warmLaneCache = null!;

    private TGrid laneQueryMin;
    private TGrid laneQueryMax;
    private EnemyLaneStart dirtyProbe = null!;

    /// <summary>逐弹丸口径的敌方 lane 命中数（含同 TGrid 的重复项），作为并行 miss 形状的正确性门槛。</summary>
    private int expectedHits;

    private int missExceptions;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntime.EnsureInitialized();

        dop = Math.Max(2, Environment.ProcessorCount - 2);
        parallelCountLimit = EditorGlobalSetting.Default.ParallelCountLimit;

        var sample = SampleCorpus.AllSamples.FirstOrDefault(x => string.Equals(x.FileName, Chart, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"'{Chart}' not found in Data/FumenSamples (embedded resources).");
        fumen = SampleCorpus.Deserialize(sample);

        // 查询范围取「整棵 lane 树」：与生产里 GetVisibleStartObjects(t, t) 走同一条 Query 路径。
        var maxTotalGrid = fumen.Lanes.Select(x => x.MaxTGrid.TotalGrid).DefaultIfEmpty(0).Max();
        laneQueryMin = TGrid.FromTotalGrid(0);
        laneQueryMax = TGrid.FromTotalGrid(Math.Max(1, maxTotalGrid));
        dirtyProbe = new EnemyLaneStart { RecordId = int.MaxValue - 1, TGrid = TGrid.FromTotalGrid(maxTotalGrid + 1920 * 64) };

        var from = TGrid.FromTotalGrid(Math.Max(1, maxTotalGrid / 2));
        remainingTGrids = EnumerateRemainingProjectiles(from).ToArray();
        distinctCount = remainingTGrids.Select(x => x.TotalGrid).Distinct().Count();
        parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = dop };

        warmLaneCache = new Dictionary<int, EnemyLaneStart?>();
        foreach (var t in remainingTGrids)
            warmLaneCache[t.TotalGrid] = QueryEnemyLane(t);

        PrintFacts(sample, maxTotalGrid);

        // 等价性：两条路径必须给出同一批 lane（否则耗时对比没有意义）。
        var direct = CountDirect(remainingTGrids);
        var cached = CountCached(remainingTGrids);
        if (direct != cached)
            throw new InvalidOperationException($"lane lookup mismatch: direct={direct} cached={cached}");

        // 生产形状（改法2 + §8）：缓存命中口径必须与逐项查询一致，脏帧里也一样
        expectedHits = direct;
        if (CountPooled(remainingTGrids) != expectedHits)
            throw new InvalidOperationException("pooled lane query (production helper) disagreed with the direct expression.");
        if (RunParallelMissWithPerThreadCache(preSync: true) != expectedHits)
            throw new InvalidOperationException("pre-synced parallel miss shape returned the wrong hit count.");
        if (RunParallelMissWithPerThreadCache(preSync: false) != expectedHits)
            throw new InvalidOperationException("parallel miss shape (no pre-sync) returned the wrong hit count.");
        fumen.Lanes.Add(dirtyProbe);
        try
        {
            if (RunParallelMissWithPerThreadCache(preSync: false) != expectedHits)
                throw new InvalidOperationException("dirty parallel miss shape returned the wrong hit count.");
        }
        finally
        {
            fumen.Lanes.Remove(dirtyProbe);
        }

        // 脏风暴：修复后两种形状都必须完全正确，且一次 NRE 都不允许（这是 §8 的验收门槛）。
        var lanesBefore = fumen.Lanes.Count;
        var expected = CountLanes(laneQueryMin, laneQueryMax);
        var current = DirtyStorm_CurrentShape();
        if (fumen.Lanes.Count != lanesBefore)
            throw new InvalidOperationException("DirtyStorm_CurrentShape left the lane tree modified.");
        if (stormExceptions != 0)
            throw new InvalidOperationException($"DirtyStorm_CurrentShape threw {stormExceptions} NullReferenceException(s).");
        if (current != expected)
            throw new InvalidOperationException($"DirtyStorm_CurrentShape returned {current}, expected {expected}.");
        var fixedShape = DirtyStorm_AfterSingleRebuild();
        if (fixedShape != expected)
            throw new InvalidOperationException($"DirtyStorm_AfterSingleRebuild returned {fixedShape}, expected {expected}.");
        if (fumen.Lanes.Count != lanesBefore)
            throw new InvalidOperationException("DirtyStorm_AfterSingleRebuild left the lane tree modified.");

        // 归零，使 GlobalCleanup 的统计只反映正式测量区间。
        stormCalls = 0;
        stormExceptions = 0;
        missExceptions = 0;
    }

    // =====================================================================
    // ① 逐项：生产表达式 vs 帧内缓存命中
    // =====================================================================

    /// <summary>现状：ProjectileBatchDrawTargetBase.cs:310 —— 每枚弹丸一次区间查询 + OfType + LastOrDefault。</summary>
    [Benchmark(Baseline = true)]
    public int Original_LaneQueryPerItem() => CountDirect(remainingTGrids);

    /// <summary>推荐改法 2：同一批 TGrid 走帧内缓存（命中路径）。</summary>
    [Benchmark]
    public int Optimized_CachedLaneLookupPerItem() => CountCached(remainingTGrids);

    // =====================================================================
    // ② 逐帧：一帧 K 项的全部工作
    // =====================================================================

    /// <summary>推荐改法 2 的真实形状：帧内一个 Dictionary，未命中才算（含字典本身的分配）。</summary>
    [Benchmark]
    public int Optimized_FrameWithFrameLocalCache()
    {
        var cache = new Dictionary<int, EnemyLaneStart?>();
        var hits = 0;
        foreach (var t in remainingTGrids)
        {
            if (!cache.TryGetValue(t.TotalGrid, out var lane))
                cache[t.TotalGrid] = lane = QueryEnemyLane(t);
            if (lane is not null)
                hits++;
        }
        return hits;
    }

    /// <summary>现状的真实形状：一帧 K 项各自直查（顺序执行；生产的并行形状见 DirtyStorm_*）。</summary>
    [Benchmark]
    public int Original_FrameDirectQueries()
    {
        var hits = 0;
        foreach (var t in remainingTGrids)
            if (QueryEnemyLane(t) is not null)
                hits++;
        return hits;
    }

    /// <summary>现状的真实形状（并行版，DOP = 生产值）：K 项在并行区内各自直查共享 lane 树。</summary>
    [Benchmark]
    public int Original_FrameDirectQueriesParallel()
    {
        SyncLaneTree();   // 先单线程喂一次，使本项只量「查询」本身，不含重建
        var hits = 0;
        Parallel.ForEach(remainingTGrids, parallelOptions, () => 0,
            (t, _, local) => local + (QueryEnemyLane(t) is null ? 0 : 1),
            local => Interlocked.Add(ref hits, local));
        return hits;
    }

    /// <summary>改法 2+3：并行区之前顺序把 D 个 distinct TGrid 查完（miss），并行体内只读帧内字典。</summary>
    [Benchmark]
    public int Optimized_Frame_SequentialMissThenParallelLookup()
    {
        SyncLaneTree();
        var cache = new Dictionary<int, EnemyLaneStart?>(distinctCount);
        foreach (var t in remainingTGrids)
            if (!cache.ContainsKey(t.TotalGrid))
                cache[t.TotalGrid] = QueryEnemyLane(t);

        var hits = 0;
        Parallel.ForEach(remainingTGrids, parallelOptions, () => 0,
            (t, _, local) => local + (cache[t.TotalGrid] is null ? 0 : 1),
            local => Interlocked.Add(ref hits, local));
        return hits;
    }

    /// <summary>改法 2（保留并行、miss 也并行）：要求 lane 树可被并发只读——当前不满足（见 DirtyStorm_*）。</summary>
    [Benchmark]
    public int Optimized_Frame_ParallelMissAndLookup()
    {
        SyncLaneTree();
        var cache = new System.Collections.Concurrent.ConcurrentDictionary<int, EnemyLaneStart?>();
        var hits = 0;
        Parallel.ForEach(remainingTGrids, parallelOptions, () => 0,
            (t, _, local) => local + (cache.GetOrAdd(t.TotalGrid, _ => QueryEnemyLane(t)) is null ? 0 : 1),
            local => Interlocked.Add(ref hits, local));
        return hits;
    }

    // =====================================================================
    // ④ 落地形态（改法 2 + §8）：帧内缓存 + 并行 miss + 零分配查询
    // =====================================================================

    /// <summary>生产实现（<c>ProjectileBatchDrawTargetBase&lt;Bullet&gt;.QueryEnemyLane</c>）：零分配 QueryInto 版 lane 查询（逐项）。</summary>
    [Benchmark]
    public int Optimized_PooledLaneQueryPerItem() => CountPooled(remainingTGrids);

    /// <summary>
    /// 生产形状：进并行区前 <c>EnsureInSync()</c> 一次，区内每个工作线程一份帧内缓存、miss 就地查。
    /// </summary>
    [Benchmark]
    public int Fix2_ProductionShape_PreSyncThenParallelMiss() => RunParallelMissWithPerThreadCache(preSync: true);

    /// <summary>
    /// 真·§8 形状：脏树 + 并行区内发生重建（不做显式同步），结果仍必须正确。
    /// 审计文档里那个 <c>Optimized_Frame_ParallelMissAndLookup</c> 前置了 <c>SyncLaneTree()</c>，
    /// 量到的是「先同步 + 再并行 miss」，这一条才是「并行区自己扛住重建」。
    /// </summary>
    [Benchmark]
    public int Fix2_ParallelMiss_PerThreadCache_NoPreSync_OnDirtyTree()
    {
        fumen.Lanes.Add(dirtyProbe);
        //探针 lane 只落在谱面末尾之外，不该改变任何弹丸的命中数
        var hits = RunParallelMissWithPerThreadCache(preSync: false);
        fumen.Lanes.Remove(dirtyProbe);
        return hits;
    }

    /// <summary>对照形状：全线程共享一个 <c>ConcurrentDictionary</c> 帧内缓存（审计文档里的形状）。</summary>
    [Benchmark]
    public int Fix2_ParallelMiss_SharedConcurrentCache()
    {
        fumen.Lanes.EnsureInSync();

        var cache = new ConcurrentDictionary<int, EnemyLaneStart>();
        var hits = 0;
        Parallel.ForEach(remainingTGrids, parallelOptions, () => 0,
            (t, _, local) =>
            {
                var key = t.TotalGrid;
                if (!cache.TryGetValue(key, out var lane))
                {
                    lane = ProjectileBatchDrawTargetBase<Bullet>.QueryEnemyLane(fumen, t);
                    cache.TryAdd(key, lane);
                }

                return local + (lane is null ? 0 : 1);
            },
            local => Interlocked.Add(ref hits, local));
        return hits;
    }

    // =====================================================================

    /// <summary>
    /// 生产形状的并行 miss 执行体：<see cref="Parallel.ForEach{TSource, TLocal}(IEnumerable{TSource}, ParallelOptions, Func{TLocal}, Func{TSource, ParallelLoopState, TLocal, TLocal}, Action{TLocal})"/>
    /// 的 localInit/localFinally 形状与 <c>ProjectileBatchDrawTargetBase.DrawPreviewMode</c> 一致（每线程一份缓存）。
    /// </summary>
    private int RunParallelMissWithPerThreadCache(bool preSync)
    {
        if (preSync)
            fumen.Lanes.EnsureInSync();

        var hits = 0;
        Parallel.ForEach(remainingTGrids, parallelOptions,
            static () => (Buffer: new ProjectileBatchDrawTargetBase<Bullet>.DrawBuffer(), Hits: 0),
            (t, _, local) =>
            {
                try
                {
                    var lane = ProjectileBatchDrawTargetBase<Bullet>.GetEnemyLane(fumen, t, local.Buffer);
                    return (local.Buffer, local.Hits + (lane is null ? 0 : 1));
                }
                catch (NullReferenceException)
                {
                    Interlocked.Increment(ref missExceptions);
                    return local;
                }
            },
            local => Interlocked.Add(ref hits, local.Hits));
        return hits;
    }

    private int CountPooled(IReadOnlyList<TGrid> tGrids)
    {
        var hits = 0;
        foreach (var t in tGrids)
            if (ProjectileBatchDrawTargetBase<Bullet>.QueryEnemyLane(fumen, t) is not null)
                hits++;
        return hits;
    }

    /// <summary>把树的脏状态喂掉：本项只量查询/缓存成本，不含重建（重建成本由 DirtyStorm_* 负责）。</summary>
    private void SyncLaneTree() => _ = CountLanes(laneQueryMin, laneQueryMax);

    // =====================================================================
    // ③ 脏树 + 并行查询：重复重建
    // =====================================================================

    private int stormExceptions;
    private int stormCalls;

    /// <summary>
    /// 脏树 + 并行查询：<c>rebuildGate</c> 让重建只发生一次，其余读者等在闸门后直接用新树。
    /// 修复前这里每个工作线程各自重建整树，并因就地 Release 旧节点而偶发 NRE；
    /// 现在异常计数必须恒为 0（<see cref="Setup"/> 里已是硬断言），计数只是留作回归证据。
    /// </summary>
    [Benchmark]
    public int DirtyStorm_CurrentShape()
    {
        fumen.Lanes.Add(dirtyProbe);
        var lastCount = 0;
        Parallel.For(0, dop, _ =>
        {
            try { Interlocked.Exchange(ref lastCount, CountLanes(laneQueryMin, laneQueryMax)); }
            catch (NullReferenceException) { Interlocked.Increment(ref stormExceptions); }
        });
        fumen.Lanes.Remove(dirtyProbe);
        Interlocked.Increment(ref stormCalls);
        return lastCount;
    }

    /// <summary>推荐改法 3 的最小形态：并行前先单线程喂一次查询，把重建收敛成一次（之后索引已同步）。</summary>
    [Benchmark]
    public int DirtyStorm_AfterSingleRebuild()
    {
        fumen.Lanes.Add(dirtyProbe);
        _ = CountLanes(laneQueryMin, laneQueryMax);
        var lastCount = 0;
        Parallel.For(0, dop, _ => Interlocked.Exchange(ref lastCount, CountLanes(laneQueryMin, laneQueryMax)));
        fumen.Lanes.Remove(dirtyProbe);
        return lastCount;
    }

    [GlobalCleanup]
    public void Cleanup()
        => Console.WriteLine($"[{Chart}] DirtyStorm_CurrentShape: {stormCalls} 次调用共捕获 {stormExceptions} 次 " +
                             $"NullReferenceException（含 setup 期归零前的调用已排除）  " +
                             $"并行 miss 形状共捕获 {missExceptions} 次 NRE（两者都应为 0；修复前实测为 1e-4~1.6e-3 每脏帧）");

    // =====================================================================

    private EnemyLaneStart? QueryEnemyLane(TGrid t)
        => fumen.Lanes.GetVisibleStartObjects(t, t).OfType<EnemyLaneStart>().LastOrDefault();

    private int CountDirect(IReadOnlyList<TGrid> tGrids)
    {
        var hits = 0;
        foreach (var t in tGrids)
            if (QueryEnemyLane(t) is not null)
                hits++;
        return hits;
    }

    private int CountCached(IReadOnlyList<TGrid> tGrids)
    {
        var hits = 0;
        foreach (var t in tGrids)
            if (warmLaneCache[t.TotalGrid] is not null)
                hits++;
        return hits;
    }

    private int CountLanes(TGrid min, TGrid max)
    {
        var hits = 0;
        foreach (var _ in fumen.Lanes.GetVisibleStartObjects(min, max))
            hits++;
        return hits;
    }

    /// <summary>生产 `BinaryFindRange(curTGrid, TGrid.MaxValue)` 对子弹与 bell 的并集。</summary>
    private IEnumerable<TGrid> EnumerateRemainingProjectiles(TGrid from)
    {
        foreach (var bullet in fumen.Bullets.BinaryFindRange(from, TGrid.MaxValue))
            yield return bullet.TGrid;
        foreach (var bell in fumen.Bells.BinaryFindRange(from, TGrid.MaxValue))
            yield return bell.TGrid;
    }

    private void PrintFacts(FumenSample sample, int maxTotalGrid)
    {
        Console.WriteLine($"[{Chart}] {sample.Data.Length / 1024} KB  lanes={fumen.Lanes.Count}  " +
                          $"bullets={fumen.Bullets.Count}  bells={fumen.Bells.Count}  " +
                          $"bpmChanges={fumen.BpmList.Count}  meters={fumen.MeterChanges.Count}  maxTGrid={maxTotalGrid}");
        Console.WriteLine($"  ParallelCountLimit={parallelCountLimit}（K 低于该值走顺序路径）  DOP={dop}  " +
                          $"剩余弹幕输入 K(50%)={remainingTGrids.Length}  distinct TGrid={remainingTGrids.Select(x => x.TotalGrid).Distinct().Count()}");

        foreach (var frac in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var from = TGrid.FromTotalGrid((int)(maxTotalGrid * frac));
            var list = EnumerateRemainingProjectiles(from).ToArray();
            var distinct = list.Select(x => x.TotalGrid).Distinct().Count();
            Console.WriteLine($"    from {frac,4:P0} of chart: K={list.Length,6}  distinct TGrid={distinct,6}  " +
                              $"D/K={(list.Length == 0 ? 0 : distinct / (double)list.Length):F3}  " +
                              $"{(list.Length >= parallelCountLimit ? "→ 可能进并行路径" : "→ 顺序路径")}");
        }
    }
}
