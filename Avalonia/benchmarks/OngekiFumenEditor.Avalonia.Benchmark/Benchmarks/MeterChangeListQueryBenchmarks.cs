using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-DAT-010 / DAT-13 —— <c>MeterChangeList</c> 的 meter 查询重复排序 + 线性扫描：**枚举**路径。
///
/// 对照（09-09 修复前基线）: Avalonia/src/OngekiFumenEditor.Avalonia/Base/Collections/MeterChangeList.cs:69-74
///   <c>yield return firstMeter; foreach (var item in changedMeterList.<b>OrderBy(x =&gt; x.TGrid)</b>) yield return item;</c>
///   <c>changedMeterList</c> 是 <c>TGridSortList&lt;MeterChange&gt;</c>（插入即二分维持升序），所以这个
///   <c>OrderBy</c> 100% 冗余；但每次枚举仍要重新物化 + 全量稳定排序，并分配 OrderedEnumerable 与排序缓冲。
///   这条枚举被 <c>OngekiFumen.GetAllDisplayableObjects()</c> 的 <c>.Concat(MeterChanges.Skip(1))</c> 放大到
///   几乎每个交互/检查路径（全选、反选、拖动、命中测试、4 个 checker、formatter、声音重建）。
///
/// 修复: 直接枚举已升序的 backing，不排序、不分配排序缓冲。
///
/// 量纲: 报告值是**单次完整枚举**（= 一次 GetAllDisplayableObjects 走到 meter 那一段的成本），
/// 不是整帧或整份谱面的耗时。旧侧按旧代码逐字复刻建模（见 fixture 的 LegacySequence）。
/// </summary>
[MemoryDiagnoser]
public class MeterChangeListEnumerationBenchmarks
{
    /// <summary>非首个 meter change 的数量（firstMeter 不计入）。</summary>
    [Params(8, 64, 256)]
    public int MeterCount { get; set; }

    private MeterChangeList real = null!;
    private MeterChange firstMeter = null!;
    private List<MeterChange> legacyChanged = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = MeterChangeListBenchmarkData.Build(MeterCount);
        real = data.Real;
        firstMeter = data.FirstMeter;
        legacyChanged = data.LegacyChanged;
    }

    /// <summary>旧实现：每次枚举都 <c>OrderBy(x =&gt; x.TGrid)</c> 物化 + 排序。</summary>
    [Benchmark(Baseline = true)]
    public int Original_Enumerate()
    {
        var acc = 0;
        foreach (var meter in MeterChangeListBenchmarkData.LegacySequence(firstMeter, legacyChanged))
            acc += meter.BunShi;
        return acc;
    }

    /// <summary>新实现：backing 本身升序，直接枚举，零排序、零排序缓冲。</summary>
    [Benchmark]
    public int Optimized_Enumerate()
    {
        var acc = 0;
        foreach (var meter in real)
            acc += meter.BunShi;
        return acc;
    }
}

/// <summary>
/// PERF-DAT-010 / DAT-13 —— **早退也逃不掉排序**：<c>.Skip(1).FirstOrDefault()</c> 形态。
///
/// 消费者（如 <c>GetAllDisplayableObjects().FirstOrDefault(...)</c>、各种「只看后面几个」的视图）
/// 会以 <c>Skip(1)</c> 跳过 firstMeter 再取首个元素。旧实现的 <c>OrderBy</c> 是惰性迭代器的第一步，
/// 因此**即便只要第二个元素，也必须先把整个 changedMeterList 排序物化完**才能吐出它。修复后直接取
/// backing 的第 0 个元素。
///
/// 量纲: 报告值是**单次「跳过首项后取一项」**。
/// </summary>
[MemoryDiagnoser]
public class MeterChangeListEarlyExitBenchmarks
{
    [Params(8, 64, 256)]
    public int MeterCount { get; set; }

    private MeterChangeList real = null!;
    private MeterChange firstMeter = null!;
    private List<MeterChange> legacyChanged = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = MeterChangeListBenchmarkData.Build(MeterCount);
        real = data.Real;
        firstMeter = data.FirstMeter;
        legacyChanged = data.LegacyChanged;
    }

    /// <summary>旧实现：Skip(1) 后取首项——排序仍要先做完。</summary>
    [Benchmark(Baseline = true)]
    public int Original_SkipFirstTakeOne()
    {
        var meter = MeterChangeListBenchmarkData.LegacySequence(firstMeter, legacyChanged).Skip(1).FirstOrDefault();
        return meter?.BunShi ?? -1;
    }

    /// <summary>新实现：Skip(1) 后取首项。</summary>
    [Benchmark]
    public int Optimized_SkipFirstTakeOne()
    {
        var meter = real.Skip(1).FirstOrDefault();
        return meter?.BunShi ?? -1;
    }
}

/// <summary>
/// PERF-DAT-010 / DAT-13 —— <c>GetMeter</c> 的重复完整枚举 + 线性扫描（**声音重建热路径**）。
///
/// 对照（09-09 修复前基线）: MeterChangeList.cs:78
///   <c>public MeterChange GetMeter(TGrid time) =&gt; this.LastOrDefault(meter =&gt; meter.TGrid &lt;= time);</c>
///   <c>MeterChangeList</c> 不是 <c>IList&lt;T&gt;</c>，<c>Enumerable.LastOrDefault</c> 走非列表路径 →
///   <b>每次调用都先完整枚举一遍（含上面的 OrderBy 重排）</b>再做线性扫描，没有提前退出。
///
/// 真实调用点: <c>Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs:137</c> 的
///   <c>CalculateHoldTicks</c> 在 <c>:252</c> 对**每一个 Hold** 调一次 <c>GetMeter</c>，而 RebuildEvents
///   在 <c>:212</c> 遍历全部 displayable。所以成本 ≈ HoldCount ×（完整枚举 + 重排 + 线性扫描）。
///
/// 量纲: 报告值是**一遍「逐 Hold 调 GetMeter」扫描**（= <see cref="HoldCount"/> 次查询），不是整帧耗时。
///   每个规模点固定 HoldCount，以便横向比较 meter 数量对单次查询成本的放大。
/// </summary>
[MemoryDiagnoser]
public class MeterChangeListGetMeterBenchmarks
{
    /// <summary>Hold 个数（固定，隔离出 meter 数量的影响）。</summary>
    public const int HoldCount = 256;

    [Params(8, 64, 256)]
    public int MeterCount { get; set; }

    private MeterChangeList real = null!;
    private MeterChange firstMeter = null!;
    private List<MeterChange> legacyChanged = null!;
    private TGrid[] holdPoints = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = MeterChangeListBenchmarkData.Build(MeterCount);
        real = data.Real;
        firstMeter = data.FirstMeter;
        legacyChanged = data.LegacyChanged;
        holdPoints = data.HoldPoints;
    }

    /// <summary>旧实现：每个 Hold 一次「完整枚举 + 线性 LastOrDefault」。</summary>
    [Benchmark(Baseline = true)]
    public int Original_PerHoldGetMeter()
    {
        var acc = 0;
        foreach (var time in holdPoints)
        {
            var meter = MeterChangeListBenchmarkData.LegacySequence(firstMeter, legacyChanged)
                .LastOrDefault(m => m.TGrid <= time);
            acc += meter?.BunShi ?? -1;
        }
        return acc;
    }

    /// <summary>新实现：每个 Hold 一次 O(log n) 二分前驱，零分配。</summary>
    [Benchmark]
    public int Optimized_PerHoldGetMeter()
    {
        var acc = 0;
        foreach (var time in holdPoints)
        {
            var meter = real.GetMeter(time);
            acc += meter?.BunShi ?? -1;
        }
        return acc;
    }
}

/// <summary>
/// PERF-DAT-010 / DAT-13 —— <c>GetPrevMeter</c> / <c>GetNextMeter</c> 的重复完整枚举 + 线性扫描。
///
/// 对照（09-09 修复前基线）: MeterChangeList.cs:82,86
///   <c>GetPrevMeter(TGrid time) =&gt; this.LastOrDefault(meter =&gt; meter.TGrid &lt; time);</c>
///   <c>GetNextMeter(TGrid time) =&gt; this.FirstOrDefault(meter =&gt; time &lt; meter.TGrid);</c>
///   两者同样每次调用都完整枚举；<c>FirstOrDefault</c> 那条虽然能提前命中，但排序必须先跑完。
///   注：这两个方法当前在 Avalonia/src 内**没有任何 caller**（grep 只命中定义），属潜在 API，
///   本基准只用于确认修复后它们的成本量级，不代表现存热点。
///
/// 量纲: 报告值是**一遍「逐点取前驱 + 后继」扫描**（= <see cref="MeterCount"/> 组查询）。
/// </summary>
[MemoryDiagnoser]
public class MeterChangeListNeighborBenchmarks
{
    [Params(8, 64, 256)]
    public int MeterCount { get; set; }

    private MeterChangeList real = null!;
    private MeterChange firstMeter = null!;
    private List<MeterChange> legacyChanged = null!;
    private TGrid[] queryPoints = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var data = MeterChangeListBenchmarkData.Build(MeterCount);
        real = data.Real;
        firstMeter = data.FirstMeter;
        legacyChanged = data.LegacyChanged;
        queryPoints = data.QueryPoints;
    }

    /// <summary>旧实现：每个查询点各跑一次完整枚举（前驱 strict、后继 strict）。</summary>
    [Benchmark(Baseline = true)]
    public int Original_NeighborPass()
    {
        var acc = 0;
        foreach (var time in queryPoints)
        {
            var prev = MeterChangeListBenchmarkData.LegacySequence(firstMeter, legacyChanged)
                .LastOrDefault(m => m.TGrid < time);
            acc += prev?.BunShi ?? -1;

            var next = MeterChangeListBenchmarkData.LegacySequence(firstMeter, legacyChanged)
                .FirstOrDefault(m => time < m.TGrid);
            acc += next?.BunShi ?? -1;
        }
        return acc;
    }

    /// <summary>新实现：每个查询点两次 O(log n) 边界查找，零分配。</summary>
    [Benchmark]
    public int Optimized_NeighborPass()
    {
        var acc = 0;
        foreach (var time in queryPoints)
        {
            var prev = real.GetPrevMeter(time);
            acc += prev?.BunShi ?? -1;

            var next = real.GetNextMeter(time);
            acc += next?.BunShi ?? -1;
        }
        return acc;
    }
}

/// <summary>
/// 四个基准类共用的夹具：同一批 <see cref="MeterChange"/> 实例同时喂给
/// 「旧实现复刻（<see cref="LegacySequence"/>）」与「真实 <see cref="MeterChangeList"/> 新实现」，
/// 保证两侧看到完全相同的对象集合、相同的 TGrid 分布与相同的查询点。
///
/// 保真度说明: 旧侧是对 09-09 基线代码的**逐字 LINQ 复刻**（同样的 <c>OrderBy</c> + <c>Last/FirstOrDefault</c>
/// 组合、同样按「每次调用新建序列」的方式使用），不是等价重写；新侧直接调用**真实生产类型**。
/// </summary>
internal static class MeterChangeListBenchmarkData
{
    /// <summary>meter change 之间的间隔：一个 unit（1920 grid），确定性递增。</summary>
    private const int Spacing = (int)TGrid.DEFAULT_RES_T;

    /// <summary>Hold 采样点数（声音重建热路径的规模）。</summary>
    internal const int HoldPointCount = MeterChangeListGetMeterBenchmarks.HoldCount;

    internal sealed record Fixture(
        MeterChangeList Real,
        MeterChange FirstMeter,
        List<MeterChange> LegacyChanged,
        TGrid[] QueryPoints,
        TGrid[] HoldPoints);

    /// <summary>
    /// 旧实现 <c>MeterChangeList.GetEnumerator()</c>（09-09 基线）的逐字复刻：
    /// <code>
    /// yield return firstMeter;
    /// foreach (var item in changedMeterList.OrderBy(x =&gt; x.TGrid))
    ///     yield return item;
    /// </code>
    /// </summary>
    internal static IEnumerable<MeterChange> LegacySequence(MeterChange firstMeter, List<MeterChange> changed)
    {
        yield return firstMeter;
        foreach (var item in changed.OrderBy(x => x.TGrid))
            yield return item;
    }

    internal static Fixture Build(int meterCount)
    {
        var firstMeter = new MeterChange { TGrid = TGrid.Zero, Bunbo = 4, BunShi = 4 };

        var changes = new MeterChange[meterCount];
        for (var i = 0; i < meterCount; i++)
        {
            changes[i] = new MeterChange
            {
                TGrid = TGrid.FromTotalGrid((i + 1) * Spacing),
                Bunbo = 4,
                BunShi = (i % 3) + 2,
            };
        }

        // 乱序插入（模拟解析顺序）。TGrid 在 Add 之前设好，避免触发 backing 的 remove/add 重排。
        var rng = new Random(20260915);
        var order = Enumerable.Range(0, meterCount).ToArray();
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var real = new MeterChangeList();
        real.SetFirstMeter(firstMeter);
        foreach (var idx in order)
            real.Add(changes[idx]);

        // 旧侧用同一批实例（已是升序），仅顺序不同不影响 OrderBy 结果。
        var legacyChanged = changes.ToList();

        // 查询点：覆盖「恰好命中 / 落在两 meter 之间 / 早于首个 changed（退化 firstMeter）/ 晚于末尾（null）」。
        var queryPoints = new TGrid[meterCount];
        for (var i = 0; i < meterCount; i++)
        {
            var baseTotal = (i + 1) * Spacing;
            queryPoints[i] = (i % 5) switch
            {
                0 => TGrid.FromTotalGrid(baseTotal),
                1 => TGrid.FromTotalGrid(baseTotal - Spacing / 2),
                2 => TGrid.FromTotalGrid(baseTotal + Spacing / 2),
                3 => TGrid.FromTotalGrid(Spacing / 2),
                _ => TGrid.FromTotalGrid(Spacing * (meterCount + 2)),
            };
        }

        // Hold 采样点：从 0 到末尾均匀铺开，让 GetMeter 落在各个 meter 段上。
        var span = (long)Spacing * (meterCount + 1);
        var holdPoints = new TGrid[HoldPointCount];
        for (var i = 0; i < HoldPointCount; i++)
            holdPoints[i] = TGrid.FromTotalGrid((int)((i + 1) * span / (HoldPointCount + 1)));

        return new Fixture(real, firstMeter, legacyChanged, queryPoints, holdPoints);
    }
}
