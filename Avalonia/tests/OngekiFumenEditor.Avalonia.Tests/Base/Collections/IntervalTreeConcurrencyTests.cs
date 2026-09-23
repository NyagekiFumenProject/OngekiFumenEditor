#nullable enable

using System.Collections.Concurrent;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.Collections.Base.RangeTree;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

/// <summary>
/// RND-008 §8 的并发验收：区间树的写入只改私有 staging、重建后整体原子发布，故
/// ① 脏树 + N 个读者并发查询：不抛异常，且每个读者都看到完整索引（旧实现会并发 Release 旧节点 → NRE）；
/// ② 一边写一边读：不抛异常，且任何一次读都只能看到"写之前的完整集合"或"写之后的完整集合"。
/// </summary>
public sealed class IntervalTreeConcurrencyTests
{
    private static readonly int Dop = Math.Max(2, Environment.ProcessorCount - 2);

    [Fact]
    public void DirtyTree_WithConcurrentReaders_EveryReaderSeesTheCompleteIndex()
    {
        var tree = new IntervalTree<int, string>();
        for (var i = 0; i < 512; i++)
            tree.Add(i * 4, i * 4 + 2, $"item{i}");

        var before = CountQuery(tree);
        tree.Add(100_000, 100_000, "probe");

        var observed = new int[Dop];
        Parallel.For(0, Dop, i => observed[i] = CountQuery(tree));

        //每个读者都会自己触发一次重建（第一次进 rebuildGate 的负责建，其余等门后直接用新树）。
        Assert.All(observed, static count => Assert.Equal(513, count));
        Assert.Equal(512, before);
    }

    [Fact]
    public void ConcurrentWriterAndReaders_NeverThrowAndOnlySeeCompleteStates()
    {
        var tree = new IntervalTree<int, int>();
        for (var i = 0; i < 256; i++)
            tree.Add(i * 4, i * 4 + 2, i);

        var expected = CountQuery(tree);
        var failures = new ConcurrentQueue<string>();
        using var stop = new CancellationTokenSource();

        var readers = Enumerable.Range(0, Dop).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    var count = CountQuery(tree);
                    if (count != expected && count != expected + 1)
                        failures.Enqueue($"partial view: {count} (expected {expected} or {expected + 1})");
                }
                catch (Exception e)
                {
                    failures.Enqueue($"reader threw: {e.GetType().Name}: {e.Message}");
                }
            }
        })).ToArray();

        try
        {
            for (var i = 0; i < 20_000; i++)
            {
                tree.Add(50_000, 50_000, -1);
                tree.Remove(-1);
            }
        }
        finally
        {
            stop.Cancel();
            Task.WaitAll(readers);
        }

        Assert.Empty(failures);
        Assert.Equal(expected, CountQuery(tree));
    }

    [Fact]
    public void RebuildAfterInterleavedWrites_MatchesLinearScan()
    {
        var random = new Random(20260923);
        var tree = new IntervalTree<int, int>();
        var expected = new List<(int From, int To, int Value)>();

        for (var round = 0; round < 40; round++)
        {
            for (var i = 0; i < 12; i++)
            {
                var from = random.Next(0, 200);
                var to = from + random.Next(0, 20);
                var value = round * 100 + i;
                tree.Add(from, to, value);
                expected.Add((from, to, value));
            }

            //随机删除若干已写入项（按值删，可能一次删多条）
            for (var i = 0; i < 5; i++)
            {
                var victim = expected[random.Next(expected.Count)];
                tree.Remove(victim.Value);
                expected.RemoveAll(x => x.Value == victim.Value);
            }

            if (round % 7 == 3)
                tree.NotifyDirty();

            var queryFrom = random.Next(0, 100);
            var queryTo = queryFrom + random.Next(5, 120);

            var actual = tree.Query(queryFrom, queryTo).OrderBy(x => x).ToArray();
            var naive = expected
                .Where(x => x.To >= queryFrom && x.From <= queryTo)
                .Select(x => x.Value)
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(naive, actual);
            Assert.Equal(expected.Count, tree.Count);
        }

        //Values / GetEnumerator 必须反映同一批内容（快照与索引不能各说各话）
        Assert.Equal(expected.Count, tree.Values.Count());
        Assert.Equal(expected.Count, tree.Count());
    }

    [Fact]
    public void EnsureInSync_MakesPendingWritesVisibleWithoutAnotherQuery()
    {
        var tree = new IntervalTree<int, int>();
        tree.Add(10, 20, 1);

        tree.EnsureInSync();
        //同步后再写入：一次查询必须同时看到"已发布"与"待重建"的内容
        tree.Add(30, 40, 2);

        Assert.Equal(new[] { 1, 2 }, tree.Query(0, 100).OrderBy(x => x));
    }

    [Fact]
    public void Clear_PublishesAnEmptyTreeAndKeepsTheOldSnapshotReadable()
    {
        var tree = new IntervalTree<int, int>();
        for (var i = 0; i < 8; i++)
            tree.Add(i, i + 1, i);

        //枚举在 Clear 之前开始、之后继续：旧代码会因 List 被就地替换/清空而抛或读出错值
        var enumeration = tree.Values.GetEnumerator();
        Assert.True(enumeration.MoveNext());

        tree.Clear();

        Assert.Equal(0, tree.Count);
        Assert.Empty(tree.Query(0, 100));
        Assert.Equal(0, tree.Min);
        Assert.Equal(0, tree.Max);

        //已开始的枚举继续走完，不受写入影响
        var rest = 1;
        while (enumeration.MoveNext())
            rest++;
        enumeration.Dispose();
        Assert.Equal(8, rest);
    }

    [Fact]
    public void Wrapper_QueryInRangeInto_AppendsTheSameMatchesAsQueryInRange()
    {
        var wrapper = new IntervalTreeWrapper<int, Item>(
            x => new IntervalTreeWrapper<int, Item>.KeyRange { Min = x.Min, Max = x.Max },
            enableSwap: true);

        for (var i = 0; i < 32; i++)
            wrapper.Add(new Item { Id = i, Min = i * 3, Max = i * 3 + 2 });

        var expected = wrapper.QueryInRange(10, 40).OrderBy(x => x.Id).ToArray();

        var output = new List<Item>();
        wrapper.QueryInRangeInto(10, 40, output);

        Assert.Equal(expected, output.OrderBy(x => x.Id));
        Assert.Equal(expected.Length, output.Count);
    }

    private static int CountQuery<TValue>(IntervalTree<int, TValue> tree)
    {
        var count = 0;
        foreach (var _ in tree.Query(int.MinValue, int.MaxValue))
            count++;
        return count;
    }

    private sealed class Item : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; init; }
        public int Min { get; init; }
        public int Max { get; init; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
