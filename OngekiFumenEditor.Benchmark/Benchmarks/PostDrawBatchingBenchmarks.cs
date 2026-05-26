using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 性能问题 #15 - PostDraw + SKPath 单次构造未批化
///
/// 当前实现（PI#4 优化后）在 DrawPath 中使用成员级 reusedPath：
///   path.Reset() → MoveTo → 逐点 LineTo → canvas.DrawPath(path, paint)
///
/// 替代方案:
///   - DrawPoints_Polygon: canvas.DrawPoints(SKPointMode.Polygon, points, paint)
///     直接提交折线点数组，跳过 SKPath 构建开销
///   - DrawPath_AddPoly: path.AddPoly(points) 替代 MoveTo + 逐点 LineTo
///     仍用 SKPath 但用 AddPoly 一次性填充
///   - DrawPoints_Lines: canvas.DrawPoints(SKPointMode.Lines, lines, paint)
///     逐对线条批量提交（模拟原始 DrawLine 语义但批化）
///
/// 使用真实 SKCanvas + SKBitmap 渲染，测量实际绘制耗时。
/// InProcessNoEmitToolchain 避免 native SkiaSharp 版本不匹配问题。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class PostDrawBatchingBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));
        }
    }

    [Params(8, 64, 256)]
    public int PointsPerSegment;

    [Params(10, 50, 200)]
    public int SegmentsPerFrame;

    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;
    private SKPaint paint = null!;
    private SKPoint[][] segmentPoints = null!;

    [GlobalSetup]
    public void Setup()
    {
        bitmap = new SKBitmap(1920, 1080);
        canvas = new SKCanvas(bitmap);
        paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = new SKColor(255, 100, 50, 255)
        };

        var rng = new Random(42);
        segmentPoints = new SKPoint[SegmentsPerFrame][];
        for (var s = 0; s < SegmentsPerFrame; s++)
        {
            var pts = new SKPoint[PointsPerSegment];
            for (var i = 0; i < PointsPerSegment; i++)
            {
                pts[i] = new SKPoint(
                    (float)rng.NextDouble() * 1920f,
                    (float)rng.NextDouble() * 1080f);
            }
            segmentPoints[s] = pts;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        paint.Dispose();
        canvas.Dispose();
        bitmap.Dispose();
    }

    // ========================================================================
    // Baseline: 当前实现 (reusedPath + MoveTo/LineTo + DrawPath)
    // ========================================================================

    private SKPath reusedPath = new();

    [Benchmark(Baseline = true)]
    public void Original_MoveToLineTo()
    {
        canvas.Save();
        try
        {
            for (var s = 0; s < segmentPoints.Length; s++)
            {
                var pts = segmentPoints[s];
                DrawPath_MoveToLineTo(pts);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawPath_MoveToLineTo(SKPoint[] pts)
    {
        var path = reusedPath;
        path.Reset();
        path.MoveTo(pts[0]);
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var cur = pts[i];
            var next = pts[i + 1];
            if (cur == next) continue;
            path.LineTo(next);
        }
        canvas.DrawPath(path, paint);
    }

    // ========================================================================
    // 优化 A: DrawPoints(SKPointMode.Polygon, ...)
    // 跳过 SKPath，直接提交折线点数组
    // ========================================================================

    [Benchmark]
    public void Optimized_DrawPoints_Polygon()
    {
        canvas.Save();
        try
        {
            for (var s = 0; s < segmentPoints.Length; s++)
                canvas.DrawPoints(SKPointMode.Polygon, segmentPoints[s], paint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    // ========================================================================
    // 优化 B: SKPath.AddPoly 替代 MoveTo + 逐点 LineTo
    // 仍用 SKPath 但一次性填充所有点
    // ========================================================================

    [Benchmark]
    public void Optimized_DrawPath_AddPoly()
    {
        canvas.Save();
        try
        {
            for (var s = 0; s < segmentPoints.Length; s++)
            {
                var pts = segmentPoints[s];
                DrawPath_AddPoly(pts);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawPath_AddPoly(SKPoint[] pts)
    {
        var path = reusedPath;
        path.Reset();
        path.AddPoly(pts, close: false);
        canvas.DrawPath(path, paint);
    }

    // ========================================================================
    // 优化 C: DrawPoints(SKPointMode.Lines, ...)
    // 逐对线条批量提交，模拟原始 DrawLine 语义但批化
    // 需要将折线点转换为线段点对
    // ========================================================================

    private SKPoint[] linePointsBuffer = Array.Empty<SKPoint>();

    [GlobalSetup(Target = nameof(Optimized_DrawPoints_Lines))]
    public void SetupLinesBuffer()
    {
        Setup();
        var maxPairs = PointsPerSegment - 1;
        linePointsBuffer = new SKPoint[maxPairs * 2];
    }

    [Benchmark]
    public void Optimized_DrawPoints_Lines()
    {
        canvas.Save();
        try
        {
            for (var s = 0; s < segmentPoints.Length; s++)
            {
                var pts = segmentPoints[s];
                DrawPoints_Lines(pts);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawPoints_Lines(SKPoint[] pts)
    {
        var count = 0;
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var cur = pts[i];
            var next = pts[i + 1];
            if (cur == next) continue;
            linePointsBuffer[count++] = cur;
            linePointsBuffer[count++] = next;
        }
        if (count > 0)
            canvas.DrawPoints(SKPointMode.Lines, linePointsBuffer[..count], paint);
    }

    // ========================================================================
    // 优化 D: DrawPoints + 去重优化
    // 与 Polygon 类似但预处理去重
    // ========================================================================

    [Benchmark]
    public void Optimized_DrawPoints_Polygon_Dedup()
    {
        canvas.Save();
        try
        {
            for (var s = 0; s < segmentPoints.Length; s++)
                DrawPoints_Polygon_Dedup(segmentPoints[s]);
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawPoints_Polygon_Dedup(SKPoint[] pts)
    {
        var count = 0;
        for (var i = 0; i < pts.Length; i++)
        {
            if (count > 0 && pts[i] == pts[count - 1]) continue;
            pts[count++] = pts[i];
        }
        if (count >= 2)
            canvas.DrawPoints(SKPointMode.Polygon, pts[..count], paint);
    }
}
