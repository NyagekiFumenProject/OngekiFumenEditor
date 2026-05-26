using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 性能热点 #14 - RebuildObjectSoflanGroupRecord 的 LINQ + Parallel.ForEach + IEnumerable 多次枚举
/// 对照: FumenVisualEditorViewModel.UserInteractionActions.cs:1062-1083
///
/// 原版:
///   1) objs = Fumen.GetAllDisplayableObjects().OfType<OngekiMovableObjectBase>()  // 延迟 enumerable
///   2) objs = objs.Where(x => x switch { IndividualSoflanArea ... => false, _ => true })
///   3) Parallel.ForEach(objs, ...)  // partitioner 走 IEnumerable 通道
///   4) #if DEBUG: objs.Any()        // 重新枚举
///
/// 用户指示:用 IPooledList 接收数据再并行化。对比三种方案:
///   - Original_LinqParallel:       原版 LINQ chain + Parallel.ForEach
///   - Optimized_PooledListParallel:实化到 IPooledList + Parallel.ForEach(list, ...) — 走 IEnumerable 通道但已实化
///   - Optimized_PartitionerRange:  实化到 IPooledList + Parallel.ForEach(Partitioner.Create(asList, true), ...) — 静态范围分区
///
/// 用独立 DTO 模拟,避免引用 OngekiFumenEditor 内部 Caliburn/Gemini 类型导致
/// wrapper 进程类型加载需要 Costura.Attach 而失败。
/// </summary>
public class RebuildSoflanGroupBenchmarks
{
    private enum ObjKind { Tap, Hold, Bullet, Bell, IndSoflan, IndSoflanEnd, Connectable, Other }

    private sealed class FakeObj
    {
        public ObjKind Kind;
        public int Id;
        public double Value;
    }

    private FakeObj[] all = Array.Empty<FakeObj>();
    private readonly ParallelOptions parallelOpts = new();
    private readonly ConcurrentDictionary<int, double> cache = new();

    [Params(500, 2000, 8000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var list = new List<FakeObj>(N);
        for (var i = 0; i < N; i++)
        {
            list.Add(new FakeObj
            {
                Kind = (ObjKind)(rng.Next() % 8),
                Id = i,
                Value = rng.NextDouble(),
            });
        }
        all = list.ToArray();
    }

    [IterationSetup]
    public void IterSetup()
    {
        cache.Clear();
    }

    // 模拟 QuerySoflanGroup + SetCache 的非平凡 per-item 工作
    private double SimulateWork(FakeObj obj)
    {
        var v = obj.Value;
        // 一些计算让并行收益可观,~150ns/item 量级
        for (var i = 0; i < 50; i++)
            v = Math.Sqrt(v + 1.0);
        cache[obj.Id] = v;
        return v;
    }

    private static bool ShouldSkip(FakeObj x) => x.Kind switch
    {
        ObjKind.IndSoflan or ObjKind.IndSoflanEnd or ObjKind.Connectable => true,
        _ => false,
    };

    // ============ 原版:LINQ chain + Parallel.ForEach(IEnumerable) ============

    [Benchmark(Baseline = true)]
    public int Original_LinqParallel()
    {
        IEnumerable<FakeObj> objs = all.OfType<FakeObj>();
        objs = objs.Where(x => !ShouldSkip(x));

        Parallel.ForEach(objs, parallelOpts, obj => SimulateWork(obj));

        // 原版 DEBUG 分支还会 objs.Any() 再次枚举一遍,这里模拟该额外枚举
        var any = false;
        foreach (var _ in objs)
        {
            any = true;
            break;
        }
        return any ? cache.Count : 0;
    }

    // ============ 优化 A:实化到 IPooledList + Parallel.ForEach(list, ...) ============

    [Benchmark]
    public int Optimized_PooledListParallel()
    {
        using var list = ObjectPool.GetPooledList<FakeObj>();
        for (var i = 0; i < all.Length; i++)
        {
            var obj = all[i];
            if (ShouldSkip(obj))
                continue;
            list.Add(obj);
        }

        Parallel.ForEach(list, parallelOpts, obj => SimulateWork(obj));

        var any = list.Count > 0;
        return any ? cache.Count : 0;
    }

    // ============ 优化 B:实化到 IPooledList + 顺序 foreach(对比基线,验证 Parallel 是否值得) ============

    [Benchmark]
    public int Optimized_PooledListSequential()
    {
        using var list = ObjectPool.GetPooledList<FakeObj>();
        for (var i = 0; i < all.Length; i++)
        {
            var obj = all[i];
            if (ShouldSkip(obj))
                continue;
            list.Add(obj);
        }

        for (var i = 0; i < list.Count; i++)
            SimulateWork(list[i]);

        var any = list.Count > 0;
        return any ? cache.Count : 0;
    }

    // ============ 优化 C:阈值切换 - 小集合走顺序避免 Parallel 启动开销,大集合并行 ============

    private const int ParallelThreshold = 1024;

    [Benchmark]
    public int Optimized_ThresholdSwitch()
    {
        using var list = ObjectPool.GetPooledList<FakeObj>();
        for (var i = 0; i < all.Length; i++)
        {
            var obj = all[i];
            if (ShouldSkip(obj))
                continue;
            list.Add(obj);
        }

        if (list.Count < ParallelThreshold)
        {
            for (var i = 0; i < list.Count; i++)
                SimulateWork(list[i]);
        }
        else
        {
            Parallel.ForEach(list, parallelOpts, obj => SimulateWork(obj));
        }

        var any = list.Count > 0;
        return any ? cache.Count : 0;
    }
}
