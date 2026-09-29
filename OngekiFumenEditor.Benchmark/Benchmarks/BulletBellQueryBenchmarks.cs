using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Utils.ObjectPool;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 预览模式子弹/Bell 分桶查询（性能报告 §8 P2 的落地验证）。
///
/// 每帧预览模式下 <c>FumenVisualEditorViewModel.OnEditorRender</c> 会把「当前时间之后」的子弹/Bell
/// 按 soflan group 可见性过滤后塞进各 drawingTarget 的桶里：
/// 旧实现是 <c>BinaryFindRange(yield).Where(...)</c> 的惰性管道 + 每 target 重新枚举，
/// 新实现（已落地）改用 <c>BinaryFindRangeIndex</c> + 索引循环 + 谓词内联，直接写入目标桶。
///
/// 本类同时保留两种未采纳形态作为对照，便于后续回看决策依据：
/// <list type="bullet">
/// <item><c>Loop_IndexRange_SharedList</c>：一次扫描 + 帧内池化列表复用（T=1 时收益与已落地形态持平，T≥2 才明显，故未采纳）；</item>
/// <item><c>Loop_IndexRange_SharedList_GroupCache</c>：再加 group 可见性帧内缓存（实测更慢，已否决）。</item>
/// </list>
/// 自建数据（不依赖 IoC/解析器，避免宿主目录不可写时引导失败），但查询路径全部用主工程真实类型：
/// <c>TGridSortList&lt;Bullet&gt;</c> / <c>GlobalCacheSoflanGroupRecorder</c>（SetCache + Freeze，
/// 与生产同走 FrozenDictionary）/ <c>IndividualSoflanAreaListMap</c> / <c>ObjectPool</c>。
/// 一次操作 = 一帧；Allocated 即每帧分配。
/// </summary>
public class BulletBellQueryBenchmarks
{
    private const int SoflanGroupCount = 8;

    [Params(1000, 10000)]
    public int BulletCount { get; set; }

    [Params(1, 2)]
    public int TargetCount { get; set; }

    private TGridSortList<Bullet> bullets = null!;
    private GlobalCacheSoflanGroupRecorder recorder = null!;
    private IndividualSoflanAreaListMap soflanMap = null!;
    private SoflanList defaultSoflanList = null!;
    private TGrid curTGrid;
    private DrawingTargetContext[] contexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        defaultSoflanList = new SoflanList();
        recorder = new GlobalCacheSoflanGroupRecorder();
        recorder.SetDefault(defaultSoflanList);

        // 1/8 的 soflan group 在预览模式隐藏，其余可见（对应真实谱面里用户关掉个别组）
        soflanMap = new IndividualSoflanAreaListMap();
        for (var group = 0; group < SoflanGroupCount; group++)
            soflanMap.TryGetOrCreateSoflanGroupWrapItem(group, out _).IsDisplayInPreviewMode = group != 0;

        bullets = new TGridSortList<Bullet>();
        bullets.BeginBatchAction();
        for (var i = 0; i < BulletCount; i++)
        {
            var bullet = new Bullet { TGrid = TGrid.FromTotalGrid(i * 10) };
            bullets.Add(bullet);
            recorder.SetCache(bullet.Id, defaultSoflanList, i % SoflanGroupCount);
        }
        bullets.EndBatchAction();
        recorder.Freeze();

        curTGrid = bullets[0].TGrid;

        contexts = new DrawingTargetContext[TargetCount];
        for (var i = 0; i < TargetCount; i++)
            contexts[i] = new DrawingTargetContext { SoflanGroupId = 0 };

        ValidateEquivalence();
    }

    // ---------- 旧形态：LINQ 管道 + 每个 target 重新枚举（基线）----------
    [Benchmark(Baseline = true)]
    public int Linq_Where_PerTarget()
    {
        var blts = bullets.BinaryFindRange(curTGrid, TGrid.MaxValue).Where(x =>
        {
            recorder.GetCache(x, out var soflanGroup);
            return CheckVisible(soflanGroup);
        });
        return FillTargets_Enumerable(blts);
    }

    // ---------- 对照：同样的 LINQ 管道、谓词恒真（隔离迭代器/委托本身的成本）----------
    [Benchmark]
    public int Linq_Where_NoPredicate_PerTarget()
    {
        var blts = bullets.BinaryFindRange(curTGrid, TGrid.MaxValue).Where(static _ => true);
        return FillTargets_Enumerable(blts);
    }

    // ---------- 保留 LINQ，但只求值一次到池化列表（未采纳）----------
    [Benchmark]
    public int Linq_Where_SharedList()
    {
        using var shared = ObjectPool.GetPooledList<OngekiObjectBase>();
        shared.AddRange(bullets.BinaryFindRange(curTGrid, TGrid.MaxValue).Where(x =>
        {
            recorder.GetCache(x, out var soflanGroup);
            return CheckVisible(soflanGroup);
        }));
        return FillTargets_SharedList(shared);
    }

    // ---------- 已落地：索引区间 + 直写循环（方案 A）----------
    [Benchmark]
    public int Loop_IndexRange_PerTarget()
    {
        var (minIndex, maxIndex) = bullets.BinaryFindRangeIndex(curTGrid, TGrid.MaxValue);
        var total = 0;
        for (var t = 0; t < TargetCount; t++)
        {
            using var rPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
            using var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
            rPool[contexts[t]] = rrPool;
            for (var i = minIndex; i < maxIndex; i++)
            {
                var bullet = bullets[i];
                recorder.GetCache(bullet, out var soflanGroup);
                if (CheckVisible(soflanGroup))
                    rrPool.Add(bullet);
            }
            total += rrPool.Count;
        }
        return total;
    }

    // ---------- 未采纳：一次扫描 + 帧内池化列表复用 ----------
    [Benchmark]
    public int Loop_IndexRange_SharedList()
    {
        using var shared = ObjectPool.GetPooledList<OngekiObjectBase>();
        var (minIndex, maxIndex) = bullets.BinaryFindRangeIndex(curTGrid, TGrid.MaxValue);
        for (var i = minIndex; i < maxIndex; i++)
        {
            var bullet = bullets[i];
            recorder.GetCache(bullet, out var soflanGroup);
            if (CheckVisible(soflanGroup))
                shared.Add(bullet);
        }
        return FillTargets_SharedList(shared);
    }

    // ---------- 已否决：再加 soflan group 可见性帧内缓存（实测比上一形态慢）----------
    [Benchmark]
    public int Loop_IndexRange_SharedList_GroupCache()
    {
        using var shared = ObjectPool.GetPooledList<OngekiObjectBase>();
        using var groupVisible = ObjectPool.GetPooledDictionary<int, bool>();
        var (minIndex, maxIndex) = bullets.BinaryFindRangeIndex(curTGrid, TGrid.MaxValue);
        for (var i = minIndex; i < maxIndex; i++)
        {
            var bullet = bullets[i];
            recorder.GetCache(bullet, out var soflanGroup);
            if (!groupVisible.TryGetValue(soflanGroup, out var visible))
                groupVisible[soflanGroup] = visible = CheckVisible(soflanGroup);
            if (visible)
                shared.Add(bullet);
        }
        return FillTargets_SharedList(shared);
    }

    private int FillTargets_Enumerable(IEnumerable<OngekiObjectBase> source)
    {
        var total = 0;
        for (var t = 0; t < TargetCount; t++)
        {
            using var rPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
            using var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
            rPool[contexts[t]] = rrPool;
            rrPool.AddRange(source);
            total += rrPool.Count;
        }
        return total;
    }

    private int FillTargets_SharedList(IPooledList<OngekiObjectBase> shared)
    {
        var total = 0;
        for (var t = 0; t < TargetCount; t++)
        {
            using var rPool = ObjectPool.GetPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>();
            using var rrPool = ObjectPool.GetPooledList<OngekiObjectBase>();
            rPool[contexts[t]] = rrPool;
            rrPool.AddRange(shared);
            total += rrPool.Count;
        }
        return total;
    }

    // 与 CheckSoflanGroupVisible(int) 的预览模式分支一致
    private bool CheckVisible(int soflanGroup)
        => soflanMap.TryGetOrCreateSoflanGroupWrapItem(soflanGroup, out _).IsDisplayInPreviewMode;

    private List<OngekiObjectBase> Filter_Reference() => bullets
        .BinaryFindRange(curTGrid, TGrid.MaxValue)
        .Where(x =>
        {
            recorder.GetCache(x, out var soflanGroup);
            return CheckVisible(soflanGroup);
        })
        .Cast<OngekiObjectBase>()
        .ToList();

    private List<OngekiObjectBase> Filter_Loop()
    {
        var result = new List<OngekiObjectBase>();
        var (minIndex, maxIndex) = bullets.BinaryFindRangeIndex(curTGrid, TGrid.MaxValue);
        for (var i = minIndex; i < maxIndex; i++)
        {
            var bullet = bullets[i];
            recorder.GetCache(bullet, out var soflanGroup);
            if (CheckVisible(soflanGroup))
                result.Add(bullet);
        }
        return result;
    }

    private List<OngekiObjectBase> Filter_Loop_GroupCache()
    {
        var result = new List<OngekiObjectBase>();
        var groupVisible = new Dictionary<int, bool>();
        var (minIndex, maxIndex) = bullets.BinaryFindRangeIndex(curTGrid, TGrid.MaxValue);
        for (var i = minIndex; i < maxIndex; i++)
        {
            var bullet = bullets[i];
            recorder.GetCache(bullet, out var soflanGroup);
            if (!groupVisible.TryGetValue(soflanGroup, out var visible))
                groupVisible[soflanGroup] = visible = CheckVisible(soflanGroup);
            if (visible)
                result.Add(bullet);
        }
        return result;
    }

    private void ValidateEquivalence()
    {
        var reference = Filter_Reference();
        AssertSame(nameof(Filter_Loop), reference, Filter_Loop());
        AssertSame(nameof(Filter_Loop_GroupCache), reference, Filter_Loop_GroupCache());

        var visibleCount = reference.Count;
        var expected = visibleCount * TargetCount;
        var cases = new (string Name, int Total)[]
        {
            (nameof(Linq_Where_PerTarget), Linq_Where_PerTarget()),
            (nameof(Linq_Where_SharedList), Linq_Where_SharedList()),
            (nameof(Loop_IndexRange_PerTarget), Loop_IndexRange_PerTarget()),
            (nameof(Loop_IndexRange_SharedList), Loop_IndexRange_SharedList()),
            (nameof(Loop_IndexRange_SharedList_GroupCache), Loop_IndexRange_SharedList_GroupCache()),
        };
        foreach (var (name, total) in cases)
            if (total != expected)
                throw new InvalidOperationException($"{name}: 分桶总数 {total} != 期望 {expected}");

        if (Linq_Where_NoPredicate_PerTarget() != BulletCount * TargetCount)
            throw new InvalidOperationException($"{nameof(Linq_Where_NoPredicate_PerTarget)} 分桶总数与全部子弹数不符");
    }

    private static void AssertSame(string name, List<OngekiObjectBase> expected, List<OngekiObjectBase> actual)
    {
        if (expected.Count != actual.Count)
            throw new InvalidOperationException($"{name}: 元素数 {actual.Count} != 期望 {expected.Count}");
        for (var i = 0; i < expected.Count; i++)
            if (!ReferenceEquals(expected[i], actual[i]))
                throw new InvalidOperationException($"{name}: 下标 {i} 的元素引用不同");
    }
}
