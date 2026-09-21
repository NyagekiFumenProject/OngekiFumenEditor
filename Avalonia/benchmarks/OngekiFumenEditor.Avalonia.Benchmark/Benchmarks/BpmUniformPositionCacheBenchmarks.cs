using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 审计项 DAT-C1（P2）—— <c>BpmList.GetCachedAllBpmUniformPositionList()</c> 名为缓存，
/// 命中判断却每次都对整表 <c>Aggregate</c> 求内容哈希，命中路径成本与 BPM 数量线性相关。
///
/// 修复前（Base/Collections/BpmList.cs:148-162）：
///     int calcHash(BPMChange e) =&gt; HashCode.Combine(e.BPM, e.TGrid.TotalGrid);
///     var hash = this.Aggregate(0, (x, e) =&gt; HashCode.Combine(x, calcHash(e)));   // 每次调用都跑
///     hash = HashCode.Combine(hash);
///     if (hash != cachedBpmContentHash) { UpdateCachedAllBpmUniformPositionList(); cachedBpmContentHash = hash; }
///     return cachedBpmUniformPosition;
/// 修复后：BpmList 维护内容令牌（Add / Remove / 任一 BPMChange 的 BPM 或 TGrid 变化都取一个新的
/// 全局唯一值，对外为 <c>BpmList.ContentVersion</c>），命中判断降为一次整数比较。
///
/// 为什么值得改（调用方，全仓 grep 实证）：
///   <c>TGridCalculator.GetAllBpmUniformPositionList</c>（Modules/FumenVisualEditor/TGridCalculator.cs:274-279）
///   是本方法的唯一调用点，而它被 <c>ConvertTGridToAudioTime</c> / <c>ConvertAudioTimeToTGrid</c>
///   （同文件 :22-44）逐次调用；后两者在帧内是**逐对象**调用的：
///   DrawJudgeLineHelper.cs:40、DrawTimeSignatureHelper.cs:98、BeamLazerDrawingTarget.cs:66/75/114/116、
///   ProjectileBatchDrawTargetBase.cs:277、DrawPlayerLocationHelper.cs:58、FumenVisualEditorViewModel.Drawing.cs:354/363 …
///   即本方法的命中路径成本 = O(该帧参与时间换算的对象数 × BPM 变更数)。
///
/// 量纲说明（重要）：
///   报告值是「一次命中的 <c>GetCachedAllBpmUniformPositionList()</c> 调用」的成本（逐调用量纲）。
///   换算单帧总成本请乘上该帧调用次数（≈ 参与时间换算的对象数）。**不是**整帧加速比：
///   本项只消除挂在调用点上的 O(n) 哈希，调用点自身紧随其后的线性查找
///   （TGridCalculator.cs:29/:44 的 <c>LastOrDefault</c>）不属于本项、两种实现完全相同，作差会抵消。
///
/// 保真度说明：
///   新旧两条路径都使用**真实生产类型** BpmList / BPMChange / TGrid（Benchmark 程序集已通过 IVT 访问 internal）。
///   <see cref="Optimized_ProductionHit"/> 直接调用生产方法本体；
///   <see cref="Original_ContentHashGate"/> 按修复前 BpmList.cs:148-162 逐字复刻那道闸门
///   （含它自己的失效依据字段），以便在同一份输入上做新旧对拍。
///   <c>UpdateCachedAllBpmUniformPositionList</c> 本次未改动，重算分支的成本两种实现相同，故不在基准内复刻。
///   运行要求：**Release**。DEBUG 下生产方法内保留了一次内容哈希对拍（BpmList.cs 的 <c>#if DEBUG</c>），
///   会把命中路径重新拉回 O(n)，Debug 下跑出的数字不代表线上行为。
/// </summary>
[MemoryDiagnoser]
public class BpmUniformPositionCacheBenchmarks
{
    /// <summary>BPM 变更总数（含 TGrid 0 处的初始 BPM）。真实谱面量级数十；1024 用于放大 O(n) 项。</summary>
    [Params(1, 16, 128, 1024)]
    public int BpmChangeCount;

    private BpmList bpmList = null!;

    /// <summary>生产实现的缓存列表（命中路径返回的对象）。</summary>
    private List<(TimeSpan audioTime, BPMChange bpm)> cachedUniformPositions = null!;

    /// <summary>修复前字段 <c>cachedBpmContentHash</c> 的位置：旧闸门的失效依据。</summary>
    private int originalContentHashToken;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntime.EnsureInitialized();

        bpmList = CreateBpmList(BpmChangeCount);
        cachedUniformPositions = bpmList.GetCachedAllBpmUniformPositionList();
        originalContentHashToken = ComputeContentHash(bpmList);

        VerifyNewAndOldAgree();
    }

    // =====================================================================
    // 被测：一次命中调用
    // =====================================================================

    /// <summary>修复前：命中也要先对整表求一遍内容哈希。</summary>
    [Benchmark(Baseline = true)]
    public int Original_ContentHashGate()
    {
        // 复刻修复前 :151-152
        var hash = ComputeContentHash(bpmList);
        hash = HashCode.Combine(hash);

        if (hash != originalContentHashToken)
        {
            // 真实实现在这里走 UpdateCachedAllBpmUniformPositionList() 并更新令牌；
            // Setup 已把令牌设为当前内容哈希，故命中路径不进入本分支（重算成本两种实现相同）。
            originalContentHashToken = hash;
        }

        return cachedUniformPositions.Count;
    }

    /// <summary>修复后：读一次版本号并比较（生产方法本体）。</summary>
    [Benchmark]
    public int Optimized_ProductionHit() => bpmList.GetCachedAllBpmUniformPositionList().Count;

    /// <summary>隔离项：只量那份被删掉的内容哈希，用于归因（差值 ≈ 它减去一次整数比较）。</summary>
    [Benchmark]
    public int Isolate_ContentHashOnly()
    {
        var hash = ComputeContentHash(bpmList);
        return HashCode.Combine(hash);
    }

    // =====================================================================
    // 对拍
    // =====================================================================

    /// <summary>
    /// 基准只对比耗时；若新旧实现的失效判定/结果不一致，那份耗时对比就没有意义。
    /// 这里在 <see cref="Setup"/> 里逐条驱动「所有能改变 BPM 内容的变更路径」，
    /// 断言新实现确实重算、且重算结果与内容相符，同时断言旧的内容哈希闸门对这些变更同样翻转。
    /// 不一致直接抛，避免产出「快但错」的数字。
    /// </summary>
    private void VerifyNewAndOldAgree()
    {
        if (cachedUniformPositions.Count != BpmChangeCount)
            throw new InvalidOperationException(
                $"initial position count {cachedUniformPositions.Count} != bpm change count {BpmChangeCount}");

        // 命中路径不得重算：连续两次调用必须返回同一个列表实例。
        if (!ReferenceEquals(bpmList.GetCachedAllBpmUniformPositionList(), cachedUniformPositions))
            throw new InvalidOperationException("hit path rebuilt the cache");

        VerifyMutationInvalidatesCache();
    }

    private static void VerifyMutationInvalidatesCache()
    {
        // 与生产一致的初始态：TGrid 0 处 BPM 240 + 7 个变更。
        var list = CreateBpmList(8);
        var getter = list.GetCachedAllBpmUniformPositionList();
        if (getter.Count != 8)
            throw new InvalidOperationException($"initial count {getter.Count} != 8");

        var token = ComputeContentHash(list);
        var firstBpm = list.First();
        var lastChange = list.Last();

        // 1) BPM 值变更：首的 BPM 翻倍 → 每段的时长减半 → 末项 audioTime 必须变小。
        var beforeLastAudioTime = getter[^1].audioTime;
        firstBpm.BPM *= 2;
        AssertRebuilt("BPM 值变更", list, token, getter, p => p.Count == 8 && p[^1].audioTime < beforeLastAudioTime);
        token = ComputeContentHash(list);

        // 2) TGrid 等值替换（走 OngekiTimelineObjectBase.TGrid setter）。
        beforeLastAudioTime = getter[^1].audioTime;
        var moved = TGrid.FromTotalGrid(lastChange.TGrid.TotalGrid + 1920 * 10);
        lastChange.TGrid = moved;
        AssertRebuilt("TGrid 替换", list, token, getter, p => p[^1].audioTime > beforeLastAudioTime);
        token = ComputeContentHash(list);

        // 3) TGrid 子属性变更（GridBase.Unit setter → 经转发链到达 BpmList，是最易漏掉的一条路径）。
        beforeLastAudioTime = getter[^1].audioTime;
        moved.Unit += 8;
        AssertRebuilt("TGrid 子属性变更", list, token, getter, p => p[^1].audioTime > beforeLastAudioTime);
        token = ComputeContentHash(list);

        // 4) Add：末项 audioTime 的增量必须等于「原末项到新项」那一段（用同一个公开口径算期望值）。
        var prevLastAudioTime = getter[^1].audioTime;
        var prevLastGrid = list.Last().TGrid;
        var addedGrid = TGrid.FromTotalGrid(prevLastGrid.TotalGrid + 1920 * 4);
        var added = new BPMChange { TGrid = addedGrid, BPM = 90 };
        var expectedDeltaMs = MathUtils.CalculateBPMLength(list.Last(), addedGrid);
        list.Add(added);
        AssertRebuilt("Add", list, token, getter, p =>
            p.Count == 9
            && p[^1].bpm == added
            && p[^1].audioTime > prevLastAudioTime
            && Math.Abs((p[^1].audioTime - prevLastAudioTime).TotalMilliseconds - expectedDeltaMs) < 1e-6);
        token = ComputeContentHash(list);

        // 5) Remove（不能删首个 BPM）。
        var removed = list.ElementAt(1);
        if (!list.Remove(removed))
            throw new InvalidOperationException("remove failed");
        AssertRebuilt("Remove", list, token, getter, p => p.Count == 8 && p.All(x => x.bpm != removed));
    }

    private static void AssertRebuilt(
        string scenario,
        BpmList list,
        int tokenBefore,
        List<(TimeSpan audioTime, BPMChange bpm)> cached,
        Func<List<(TimeSpan audioTime, BPMChange bpm)>, bool> expectation)
    {
        // 旧闸门：内容变了，它也必须翻转（否则新旧实现的失效判定不等价）。
        if (ComputeContentHash(list) == tokenBefore)
            throw new InvalidOperationException($"{scenario}: old content hash did not change");

        // 触发命中判断：内容已变，这里必须走重算分支（DEBUG 下生产代码自己也会对拍这一条）。
        list.GetCachedAllBpmUniformPositionList();

        if (!expectation(cached))
            throw new InvalidOperationException($"{scenario}: new implementation did not rebuild correctly");
    }

    // =====================================================================
    // 与生产代码逐字对应的实现体
    // =====================================================================

    /// <summary>修复前 BpmList.cs:150-152 的那份内容哈希。</summary>
    private static int ComputeContentHash(BpmList bpmList)
    {
        static int CalcHash(BPMChange e) => HashCode.Combine(e.BPM, e.TGrid.TotalGrid);
        return HashCode.Combine(bpmList.Aggregate(0, (x, e) => HashCode.Combine(x, CalcHash(e))));
    }

    /// <summary>造 <paramref name="count"/> 个 BPM 变更（含 TGrid 0 处由 BpmList 自己补的初始 BPM）。</summary>
    private static BpmList CreateBpmList(int count)
    {
        var changes = new List<BPMChange>();
        for (var i = 1; i < count; i++)
            changes.Add(new BPMChange
            {
                TGrid = TGrid.FromTotalGrid(i * 1920 * 4),
                BPM = 120 + (i % 7) * 20,
            });

        return new BpmList(changes);
    }
}
