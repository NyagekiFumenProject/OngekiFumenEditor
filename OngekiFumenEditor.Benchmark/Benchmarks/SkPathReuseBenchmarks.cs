using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 性能热点 #4 - DefaultSkiaLineDrawing.DrawPath 每次 `using var path = new SKPath()`
/// 对照: OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/LineDrawing/DefaultSkiaLineDrawing.cs:153-167
///
/// 原版: `private void DrawPath(IList&lt;SKPoint&gt; points, SKPaint paint)` 内
///   using var path = new SKPath();
///   path.MoveTo / LineTo ...
///   canvas.DrawPath(path, paint);
/// 每次 PostDraw 一段同色 + 同 dash 的线就 new/Dispose 一次 SKPath（非托管）。
///
/// 三种方案:
///   - Original_NewSKPath:        每次 new SKPath() + Dispose（baseline，模拟当前实现）
///   - Optimized_FieldReuse:      成员级 SKPath，每次 path.Reset() 复用
///   - Optimized_PooledStack:     与 NewSkiaLineDrawing 相同的小型 Stack&lt;SKPath&gt; pool（容量 4）
///
/// 不实际渲染，只构造 path（MoveTo/LineTo），这正是 #4 关注的点：SKPath 实例本身的生命周期成本。
/// 用 [Params(SegmentsPerPath, PathsPerFrame)] 模拟"每帧多次调用 DrawPath"的现实场景。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class SkPathReuseBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            var job = Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance);
            AddJob(job);
        }
    }

    [Params(8, 64, 256)]
    public int SegmentsPerPath;

    [Params(50, 500)]
    public int PathsPerFrame;

    private SKPoint[] points = Array.Empty<SKPoint>();

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        points = new SKPoint[SegmentsPerPath];
        for (var i = 0; i < SegmentsPerPath; i++)
            points[i] = new SKPoint((float)rng.NextDouble() * 1920f, (float)rng.NextDouble() * 1080f);
    }

    // ============ Original ============

    [Benchmark(Baseline = true)]
    public int Original_NewSKPath()
    {
        var bounds = 0;
        for (var f = 0; f < PathsPerFrame; f++)
            bounds += BuildPath_New(points);
        return bounds;
    }

    private static int BuildPath_New(SKPoint[] pts)
    {
        using var path = new SKPath();
        path.MoveTo(pts[0]);
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var cur = pts[i];
            var next = pts[i + 1];
            if (cur == next) continue;
            path.LineTo(next);
        }
        return path.PointCount;
    }

    // ============ Optimized A: FieldReuse ============

    private SKPath? reusedPath;

    [GlobalSetup(Target = nameof(Optimized_FieldReuse))]
    public void SetupReuse()
    {
        Setup();
        reusedPath = new SKPath();
    }

    [GlobalCleanup(Target = nameof(Optimized_FieldReuse))]
    public void CleanupReuse()
    {
        reusedPath?.Dispose();
        reusedPath = null;
    }

    [Benchmark]
    public int Optimized_FieldReuse()
    {
        var bounds = 0;
        for (var f = 0; f < PathsPerFrame; f++)
            bounds += BuildPath_Reuse(reusedPath!, points);
        return bounds;
    }

    private static int BuildPath_Reuse(SKPath path, SKPoint[] pts)
    {
        path.Reset();
        path.MoveTo(pts[0]);
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var cur = pts[i];
            var next = pts[i + 1];
            if (cur == next) continue;
            path.LineTo(next);
        }
        return path.PointCount;
    }

    // ============ Optimized B: PooledStack ============

    private const int MaxPooledPaths = 4;
    private Stack<SKPath>? pathPool;

    [GlobalSetup(Target = nameof(Optimized_PooledStack))]
    public void SetupPool()
    {
        Setup();
        pathPool = new Stack<SKPath>();
    }

    [GlobalCleanup(Target = nameof(Optimized_PooledStack))]
    public void CleanupPool()
    {
        if (pathPool is null) return;
        while (pathPool.Count > 0)
            pathPool.Pop().Dispose();
        pathPool = null;
    }

    [Benchmark]
    public int Optimized_PooledStack()
    {
        var bounds = 0;
        for (var f = 0; f < PathsPerFrame; f++)
        {
            var path = RentPath();
            try
            {
                path.MoveTo(points[0]);
                for (var i = 0; i < points.Length - 1; i++)
                {
                    var cur = points[i];
                    var next = points[i + 1];
                    if (cur == next) continue;
                    path.LineTo(next);
                }
                bounds += path.PointCount;
            }
            finally
            {
                ReturnPath(path);
            }
        }
        return bounds;
    }

    private SKPath RentPath()
    {
        if (pathPool!.Count > 0)
            return pathPool.Pop();
        return new SKPath();
    }

    private void ReturnPath(SKPath path)
    {
        path.Reset();
        if (pathPool!.Count >= MaxPooledPaths)
        {
            path.Dispose();
            return;
        }
        pathPool.Push(path);
    }
}
