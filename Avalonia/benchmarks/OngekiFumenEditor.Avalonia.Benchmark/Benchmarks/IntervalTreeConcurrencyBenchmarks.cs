using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Avalonia.Base.Collections.Base.RangeTree;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// RND-008 §8 的并发验收基准（区间树层，与谱面无关）：
///   ① 脏树 + DOP 个读者并发查询：异常必须为 0，且每个读者都要看到完整索引（旧实现会并发 Release 旧节点 → NRE）；
///   ② 一边写一边读（1 写线程 + DOP 读线程）：异常必须为 0，且每次读只能看到"写前的完整集合"或"写后的完整集合"；
///   ③ 「一轮脏风暴的总分配量 ÷ 单次重建的分配量」≈ 1：证明重建被 <c>rebuildGate</c> 收敛成了一次。
/// ①② 的正确性在 <see cref="GlobalSetup"/> 里做成硬断言（非 0 直接抛），计数与比值在 <see cref="GlobalCleanup"/> 打印；
/// ③ 由基准自身返回的字节数给出（分配量不是耗时，故直接在方法内量，不依赖 MemoryDiagnoser 的跨线程口径）。
/// </summary>
[MemoryDiagnoser]
public class IntervalTreeConcurrencyBenchmarks
{
    [Params(512, 8192)]
    public int Items;

    private IntervalTree<int, int> tree = null!;
    private int dop;
    private int expectedCount;
    private int stormExceptions;
    private int churnReaderExceptions;
    private int partialViews;
    private int writerExceptions;
    private long singleRebuildBytes;
    private long stormBytes;

    [GlobalSetup]
    public void Setup()
    {
        dop = Math.Max(2, Environment.ProcessorCount - 2);

        tree = new IntervalTree<int, int>();
        for (var i = 0; i < Items; i++)
            tree.Add(i * 4, i * 4 + 2, i);

        expectedCount = CountRange(0, int.MaxValue);
        if (expectedCount != Items)
            throw new InvalidOperationException($"expected {Items} items, got {expectedCount}.");

        //① 脏树 + 并行读者：每个读者都必须看到包含探头项的完整索引
        tree.Add(ProbeKey, ProbeKey, ProbeValue);
        var observed = new int[dop];
        Parallel.For(0, dop, i => observed[i] = CountRange(0, int.MaxValue));
        if (observed.Any(x => x != Items + 1))
            throw new InvalidOperationException($"dirty storm readers disagreed: [{string.Join(", ", observed)}] (expected {Items + 1}).");
        tree.Remove(ProbeValue);

        //② 一边写一边读：每次都必须是完整视图（Items 或 Items + 1）
        var stop = false;
        var readers = Enumerable.Range(0, dop).Select(_ => Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                try
                {
                    var count = CountRange(0, int.MaxValue);
                    if (count != Items && count != Items + 1)
                        Interlocked.Increment(ref partialViews);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref writerExceptions);
                }
            }
        })).ToArray();

        for (var i = 0; i < 2_000; i++)
        {
            tree.Add(ProbeKey, ProbeKey, ProbeValue);
            tree.Remove(ProbeValue);
        }

        Volatile.Write(ref stop, true);
        Task.WaitAll(readers);

        if (partialViews != 0 || writerExceptions != 0)
            throw new InvalidOperationException($"write-while-read: partialViews={partialViews}, readerExceptions={writerExceptions}.");

        if (CountRange(0, int.MaxValue) != Items)
            throw new InvalidOperationException("tree left inconsistent after the churn phase.");

        singleRebuildBytes = MeasureSingleRebuildBytes();
        stormBytes = 0;
        stormExceptions = 0;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Console.WriteLine($"[items={Items}] 脏风暴并发读者: {stormExceptions} 次异常（要求 0）  " +
                          $"边写边读: {churnReaderExceptions} 次读异常（要求 0）  " +
                          $"一轮风暴分配={stormBytes} B  单次重建分配={singleRebuildBytes} B  " +
                          $"比值={(singleRebuildBytes == 0 ? double.NaN : (double)stormBytes / singleRebuildBytes):F2}（要求 ≈ 1，旧实现为 DOP−1）");
    }

    // =====================================================================

    /// <summary>干净树的读吞吐：一次全量查询（读者快路径 = 两次 volatile 读 + 遍历）。</summary>
    [Benchmark]
    public int Query_SyncedTree() => CountRange(0, int.MaxValue);

    /// <summary>脏树 + DOP 个并发读者：只允许一次重建，其余读者等在闸门后。</summary>
    [Benchmark]
    public int DirtyStorm_ParallelReaders()
    {
        tree.Add(ProbeKey, ProbeKey, ProbeValue);
        var last = 0;
        Parallel.For(0, dop, _ =>
        {
            try
            {
                Interlocked.Exchange(ref last, CountRange(0, int.MaxValue));
            }
            catch (Exception)
            {
                Interlocked.Increment(ref stormExceptions);
                throw;
            }
        });
        tree.Remove(ProbeValue);
        return last;
    }

    /// <summary>一轮脏风暴（含重建）的总分配：与单次重建分配之比应为 ≈ 1。</summary>
    [Benchmark]
    public long DirtyStorm_ParallelReaders_AllocatedBytes()
    {
        tree.Add(ProbeKey, ProbeKey, ProbeValue);

        long total = 0;
        Parallel.For(0, dop,
            () => 0L,
            (_, _, local) =>
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                _ = CountRange(0, int.MaxValue);
                return local + (GC.GetAllocatedBytesForCurrentThread() - before);
            },
            local => Interlocked.Add(ref total, local));

        tree.Remove(ProbeValue);
        stormBytes = total;
        return total;
    }

    /// <summary>单次重建的分配（同线程量）：作为上面比值的分母。</summary>
    [Benchmark]
    public long SingleRebuild_AllocatedBytes()
    {
        tree.Add(ProbeKey, ProbeKey, ProbeValue);
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = CountRange(0, int.MaxValue);
        var delta = GC.GetAllocatedBytesForCurrentThread() - before;
        tree.Remove(ProbeValue);
        return delta;
    }

    /// <summary>1 个写线程 + DOP 个读线程并发：异常计数必须保持 0（安全性由 §8 结构保证）。</summary>
    [Benchmark]
    public int WriteWhileReading_Churn()
    {
        var stop = false;
        var readerExceptions = 0;
        var readers = Enumerable.Range(0, dop).Select(_ => Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                try
                {
                    _ = CountRange(0, int.MaxValue);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref readerExceptions);
                }
            }
        })).ToArray();

        for (var i = 0; i < Items; i++)
        {
            tree.Add(ProbeKey, ProbeKey, ProbeValue);
            tree.Remove(ProbeValue);
        }

        Volatile.Write(ref stop, true);
        Task.WaitAll(readers);
        Interlocked.Add(ref churnReaderExceptions, readerExceptions);
        return readerExceptions;
    }

    // =====================================================================

    private const int ProbeKey = int.MaxValue / 2;
    private const int ProbeValue = int.MinValue;

    private int CountRange(int from, int to)
    {
        var count = 0;
        foreach (var _ in tree.Query(from, to))
            count++;
        return count;
    }

    private long MeasureSingleRebuildBytes()
    {
        tree.Add(ProbeKey, ProbeKey, ProbeValue);
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = CountRange(0, int.MaxValue);
        var delta = GC.GetAllocatedBytesForCurrentThread() - before;
        tree.Remove(ProbeValue);
        return Math.Max(1, delta);
    }
}
