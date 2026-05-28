using System.Numerics;
using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 对应 DrawPlayableAreaHelper 性能优化点 #7(子项):
/// `polylines.SortBy(x => x.Item1.Y)` 包装链路是
///   SortBy -&gt; Sort(Func) -&gt; new ComparerWrapper(compFunc) -&gt; List&lt;T&gt;.Sort(IComparer)
/// 每次调用都分配一个 ComparerWrapper(以及 SortBy 内嵌 lambda 的闭包/委托)。
///
/// 候选(全部对同一份 (Vector2, Vector2)[] 排序):
///  - SortByLambda             : 现状 `list.SortBy(x => x.Item1.Y)`
///  - IListSortFunc            : `list.Sort((x,y) => x.Item1.Y.CompareTo(y.Item1.Y))` 走扩展方法
///  - ListSortStaticComparison : `concreteList.Sort(static Comparison&lt;T&gt;)` 直接调用 List 原生 Sort
///  - ArraySortStaticComparison: `Array.Sort(arr, static Comparison&lt;T&gt;)` (假定数据可以放数组)
///
/// 测量目标: CPU + 分配,看 wrapper / lambda 闭包带来的常数差。
/// </summary>
[MemoryDiagnoser]
public class SortByVsSortComparisonBenchmarks
{
    [Params(8, 64, 512, 4096)]
    public int N;

    private (Vector2, Vector2)[] originalArray = Array.Empty<(Vector2, Vector2)>();
    private (Vector2, Vector2)[] workArray = Array.Empty<(Vector2, Vector2)>();
    private List<(Vector2, Vector2)> workList = new();

    private static readonly Comparison<(Vector2 a, Vector2 b)> StaticComparison =
        static (x, y) => x.a.Y.CompareTo(y.a.Y);

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(0xC0FFEE);
        originalArray = new (Vector2, Vector2)[N];
        for (var i = 0; i < N; i++)
        {
            var y0 = (float)rng.NextDouble() * 10_000f;
            var y1 = y0 + 100f;
            originalArray[i] = (new Vector2((float)rng.NextDouble() * 1000f, y0),
                                new Vector2((float)rng.NextDouble() * 1000f, y1));
        }
        workArray = new (Vector2, Vector2)[N];
        workList = new List<(Vector2, Vector2)>(N);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // 重置为乱序数据:每次基准都重新洗一份,避免上一轮已排序导致快速路径失真
        Array.Copy(originalArray, workArray, N);
        workList.Clear();
        for (var i = 0; i < N; i++) workList.Add(originalArray[i]);
    }

    [Benchmark(Baseline = true)]
    public void SortByLambda()
    {
        // 走 LinqExtensionMethod.SortBy -> Sort(Func) -> new ComparerWrapper(...)
        workList.SortBy(x => x.Item1.Y);
    }

    [Benchmark]
    public void IListSortFunc()
    {
        // 走 LinqExtensionMethod.Sort(IList<T>, Func<T,T,int>) -> new ComparerWrapper
        workList.Sort(static (x, y) => x.Item1.Y.CompareTo(y.Item1.Y));
    }

    [Benchmark]
    public void ListSortStaticComparison()
    {
        // 直接调用 List<T>.Sort(Comparison<T>) — 没有 ComparerWrapper 包装
        workList.Sort(StaticComparison);
    }

    [Benchmark]
    public void ArraySortStaticComparison()
    {
        Array.Sort(workArray, StaticComparison);
    }
}
