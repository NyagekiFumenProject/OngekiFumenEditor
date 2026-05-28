using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 对应 DrawPlayableAreaHelper 性能优化点 #6:
/// `EnumeratePoints` 内层每个 range 末尾的"收集 lane 起点 Y / 末 child Y"代码模式。
///
/// 候选(语义全部等价于:对每个 lane 取 起点 Y 和 末 child 的 Y,落在 [min,max] 的加进 sink):
///  - LinqOriginal       : 现网原版 — Select.Concat(Select(LastOrDefault).FilterNull().Select).Where + AddRange
///  - ForLoopLastOrDefault : 已落地版 — for 循环 + lane.Children.LastOrDefault()
///  - ForLoopEnumerated  : for 循环 + foreach 遍历到尾(无 LINQ enumerator 分配)
///
/// 数据用合成 `Lane` 类型,完全独立于谱面,以避免 BenchmarkRuntime 的环境依赖。
/// 关键变量: lane 数量 N、平均 children 数(决定 LastOrDefault 内部枚举开销)。
/// </summary>
[MemoryDiagnoser]
public class LaneEndPointsCollectBenchmarks
{
    [Params(8, 32, 128, 512)]
    public int LaneCount;

    [Params(1, 4, 16)]
    public int AvgChildrenPerLane;

    private Lane[] lanes = Array.Empty<Lane>();
    private float rangeMin;
    private float rangeMax;
    private List<float> sink = new();

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(0xC0FFEE);
        lanes = new Lane[LaneCount];
        for (var i = 0; i < LaneCount; i++)
        {
            var startY = (float)rng.NextDouble() * 10_000f;
            var children = new List<Child>(AvgChildrenPerLane);
            // 大约一半 lane 没有 child,模拟真实分布
            var n = (rng.Next() & 1) == 0 ? 0 : AvgChildrenPerLane;
            for (var c = 0; c < n; c++)
                children.Add(new Child { Y = startY + 100f * (c + 1) });
            lanes[i] = new Lane { StartY = startY, Children = children };
        }

        rangeMin = 2_000f;
        rangeMax = 8_000f;
        sink = new List<float>(capacity: LaneCount * 2);
    }

    // ---- 候选 1: 现网原始 LINQ 链 ----
    [Benchmark(Baseline = true)]
    public int LinqOriginal()
    {
        sink.Clear();
        sink.AddRange(lanes
            .Select(x => x.StartY)
            .Concat(lanes.Select(x => x.Children.LastOrDefault())
                         .FilterNull()
                         .Select(x => x!.Y))
            .Where(x => rangeMin <= x && x <= rangeMax));
        return sink.Count;
    }

    // ---- 候选 2: 已落地版 — for + LastOrDefault ----
    [Benchmark]
    public int ForLoopLastOrDefault()
    {
        sink.Clear();
        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];
            var y1 = lane.StartY;
            if (rangeMin <= y1 && y1 <= rangeMax)
                sink.Add(y1);

            var lastChild = lane.Children.LastOrDefault();
            if (lastChild is not null)
            {
                var y2 = lastChild.Y;
                if (rangeMin <= y2 && y2 <= rangeMax)
                    sink.Add(y2);
            }
        }
        return sink.Count;
    }

    // ---- 候选 3: for + foreach 取尾(无 LINQ) ----
    [Benchmark]
    public int ForLoopEnumerated()
    {
        sink.Clear();
        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];
            var y1 = lane.StartY;
            if (rangeMin <= y1 && y1 <= rangeMax)
                sink.Add(y1);

            Child? lastChild = null;
            foreach (var c in lane.Children)
                lastChild = c;
            if (lastChild is not null)
            {
                var y2 = lastChild.Y;
                if (rangeMin <= y2 && y2 <= rangeMax)
                    sink.Add(y2);
            }
        }
        return sink.Count;
    }

    // ---- 候选 4: 直接强转 IList<Child> 用索引(模拟"如果 Children 是 IList") ----
    // 真实代码里 Children 是 IEnumerable,但底层是 List。如果 API 暴露成 IList/IReadOnlyList,
    // 末尾元素就是 O(1) 索引,这是最优上限。
    [Benchmark]
    public int ForLoopIndexer()
    {
        sink.Clear();
        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];
            var y1 = lane.StartY;
            if (rangeMin <= y1 && y1 <= rangeMax)
                sink.Add(y1);

            var children = lane.Children;  // 已是 List<Child>
            if (children.Count > 0)
            {
                var y2 = children[children.Count - 1].Y;
                if (rangeMin <= y2 && y2 <= rangeMax)
                    sink.Add(y2);
            }
        }
        return sink.Count;
    }

    private sealed class Lane
    {
        public float StartY;
        public List<Child> Children = new();
    }

    private sealed class Child
    {
        public float Y;
    }
}
