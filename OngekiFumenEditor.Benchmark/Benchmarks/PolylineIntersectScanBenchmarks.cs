using System.Numerics;
using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 评估 DrawPlayableAreaHelper.EnumeratePoints 步骤 (c) 中
/// 对 polyline 段两两求交点的循环,在不同数据规模下哪种剪枝/排序策略最优。
///
/// 候选策略:
///  - Brute              : 不排序,全平方扫描 (上界基线)
///  - CurrentPrune       : 当前线上实现 — 按 start.Y 排序,break 条件 a.end.Y &lt; b.start.Y
///  - NormalizedPrune    : 段方向规范化 (minY/maxY) 后按 minY 排序,break 条件 a.maxY &lt; b.minY
///  - MaxYRunningPrune   : 同 Normalized,但内层用"runningMaxY"在 break 之前再做一次范围预筛
///  - SweepLine          : 事件驱动 (端点开/闭事件),active set 内两两测交
///
/// 数据形态:多数 lane 是短斜段(Y 跨度窄),掺杂少量长跨段模拟反向墙跨视口。
/// 度量目标:CPU 时间 + 分配。
/// </summary>
[MemoryDiagnoser]
public class PolylineIntersectScanBenchmarks
{
    [Params(16, 64, 256, 1024, 4096)]
    public int N;

    /// <summary>
    /// "长跨段"占比(%)。这些段 Y 范围占整体 80%,会让 break 剪枝失效,
    /// 模拟最差情况下 sweep-line vs sort-based break 的差距。
    /// </summary>
    [Params(0, 5, 20)]
    public int LongSegPercent;

    private (Vector2 a, Vector2 b)[] segments = Array.Empty<(Vector2, Vector2)>();

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(0xC0FFEE);
        segments = new (Vector2, Vector2)[N];

        // 仿真:Y 总跨度 [0, 10_000],X 总跨度 [-5_000, 5_000];多数段 Y 跨 ~ 1% 整体。
        const float YMax = 10_000f;
        const float XHalf = 5_000f;

        var longBudget = N * LongSegPercent / 100;

        for (var i = 0; i < N; i++)
        {
            var isLong = i < longBudget;
            var ySpan = isLong ? YMax * 0.8f : YMax * 0.01f;
            var y0 = (float)(rng.NextDouble() * (YMax - ySpan));
            var y1 = y0 + ySpan;

            var x0 = (float)(rng.NextDouble() * 2 - 1) * XHalf;
            var x1 = (float)(rng.NextDouble() * 2 - 1) * XHalf;

            // 让一半的段方向"反"(end.Y &lt; start.Y),用于检验 NormalizedPrune
            // 相对 CurrentPrune 在 Y 顺序不规则时的鲁棒性。
            if ((rng.Next() & 1) == 0)
                segments[i] = (new Vector2(x0, y0), new Vector2(x1, y1));
            else
                segments[i] = (new Vector2(x0, y1), new Vector2(x1, y0));
        }
    }

    // ---- 候选 1: 不排序,全平方 ----
    [Benchmark]
    public int Brute()
    {
        var hit = 0;
        for (var r = 0; r < segments.Length; r++)
        {
            var a = segments[r];
            for (var t = r + 1; t < segments.Length; t++)
            {
                var b = segments[t];
                if (MathUtils.GetLinesIntersection(a.a, a.b, b.a, b.b) is not null)
                    hit++;
            }
        }
        return hit;
    }

    // ---- 候选 2: 当前实现 (按 start.Y 排,end.Y &lt; b.start.Y break) ----
    [Benchmark(Baseline = true)]
    public int CurrentPrune()
    {
        // 拷一份避免 setup-once 后被排序扰动
        var copy = new (Vector2 a, Vector2 b)[segments.Length];
        Array.Copy(segments, copy, segments.Length);
        Array.Sort(copy, static (x, y) => x.a.Y.CompareTo(y.a.Y));

        var hit = 0;
        for (var r = 0; r < copy.Length; r++)
        {
            var a = copy[r];
            for (var t = r + 1; t < copy.Length; t++)
            {
                var b = copy[t];
                if (a.b.Y < b.a.Y)
                    break;
                if (MathUtils.GetLinesIntersection(a.a, a.b, b.a, b.b) is not null)
                    hit++;
            }
        }
        return hit;
    }

    // ---- 候选 3: 规范化 minY/maxY,按 minY 排 ----
    [Benchmark]
    public int NormalizedPrune()
    {
        var n = segments.Length;
        var minY = new float[n];
        var maxY = new float[n];
        var idx = new int[n];
        for (var i = 0; i < n; i++)
        {
            var s = segments[i];
            if (s.a.Y <= s.b.Y) { minY[i] = s.a.Y; maxY[i] = s.b.Y; }
            else                { minY[i] = s.b.Y; maxY[i] = s.a.Y; }
            idx[i] = i;
        }
        var minYLocal = minY;
        Array.Sort(idx, (x, y) => minYLocal[x].CompareTo(minYLocal[y]));

        var hit = 0;
        for (var r = 0; r < n; r++)
        {
            var ai = idx[r];
            var a = segments[ai];
            var aMax = maxY[ai];
            for (var t = r + 1; t < n; t++)
            {
                var bi = idx[t];
                if (aMax < minY[bi])
                    break;
                var b = segments[bi];
                if (MathUtils.GetLinesIntersection(a.a, a.b, b.a, b.b) is not null)
                    hit++;
            }
        }
        return hit;
    }

    // ---- 候选 4: Normalized + runningMaxY 紧剪枝 ----
    // 在内层维护"已扫过的所有 a 段中 maxY 的最小值"--实际上对每个 r 来说就是 aMax 自己,
    // 这个变体测的是把 minY/maxY 从堆数组改成 ref struct 局部 + 紧凑二分的取舍。
    [Benchmark]
    public int MaxYRunningPrune()
    {
        var n = segments.Length;
        // 把段 + minY/maxY 打成一个 struct array,提升缓存局部性
        var packed = new SegEntry[n];
        for (var i = 0; i < n; i++)
        {
            var s = segments[i];
            var lo = Math.Min(s.a.Y, s.b.Y);
            var hi = Math.Max(s.a.Y, s.b.Y);
            packed[i] = new SegEntry(s.a, s.b, lo, hi);
        }
        Array.Sort(packed, static (x, y) => x.MinY.CompareTo(y.MinY));

        var hit = 0;
        for (var r = 0; r < n; r++)
        {
            var a = packed[r];
            for (var t = r + 1; t < n; t++)
            {
                var b = packed[t];
                if (a.MaxY < b.MinY)
                    break;
                if (MathUtils.GetLinesIntersection(a.A, a.B, b.A, b.B) is not null)
                    hit++;
            }
        }
        return hit;
    }

    // ---- 候选 5: 事件驱动 sweep-line ----
    [Benchmark]
    public int SweepLine()
    {
        var n = segments.Length;
        // events: (Y, kind: 0=open, 1=close, segIndex)
        var events = new SweepEvent[n * 2];
        var packed = new SegEntry[n];
        for (var i = 0; i < n; i++)
        {
            var s = segments[i];
            var lo = Math.Min(s.a.Y, s.b.Y);
            var hi = Math.Max(s.a.Y, s.b.Y);
            packed[i] = new SegEntry(s.a, s.b, lo, hi);
            events[i * 2 + 0] = new SweepEvent(lo, 0, i);
            events[i * 2 + 1] = new SweepEvent(hi, 1, i);
        }
        // 同一 Y 下 open 在 close 之前,保证瞬时贴接的段也能配对一次
        Array.Sort(events, static (x, y) =>
        {
            var c = x.Y.CompareTo(y.Y);
            return c != 0 ? c : x.Kind.CompareTo(y.Kind);
        });

        var active = new List<int>(capacity: 16);
        var hit = 0;
        for (var ei = 0; ei < events.Length; ei++)
        {
            var ev = events[ei];
            if (ev.Kind == 0)
            {
                var ai = ev.SegIndex;
                var a = packed[ai];
                for (var k = 0; k < active.Count; k++)
                {
                    var bi = active[k];
                    var b = packed[bi];
                    if (MathUtils.GetLinesIntersection(a.A, a.B, b.A, b.B) is not null)
                        hit++;
                }
                active.Add(ai);
            }
            else
            {
                // 线性删除. active 期望规模小,List<int> 比 HashSet 更快。
                var idx = active.IndexOf(ev.SegIndex);
                if (idx >= 0)
                {
                    var last = active.Count - 1;
                    active[idx] = active[last];
                    active.RemoveAt(last);
                }
            }
        }
        return hit;
    }

    private readonly struct SegEntry
    {
        public readonly Vector2 A;
        public readonly Vector2 B;
        public readonly float MinY;
        public readonly float MaxY;
        public SegEntry(Vector2 a, Vector2 b, float minY, float maxY)
        {
            A = a; B = b; MinY = minY; MaxY = maxY;
        }
    }

    private readonly struct SweepEvent
    {
        public readonly float Y;
        public readonly byte Kind;     // 0 = open, 1 = close
        public readonly int SegIndex;
        public SweepEvent(float y, byte kind, int segIndex)
        {
            Y = y; Kind = kind; SegIndex = segIndex;
        }
    }
}
