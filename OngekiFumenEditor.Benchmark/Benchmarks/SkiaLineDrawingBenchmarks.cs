using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using SkiaSharp;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// DefaultSkiaLineDrawing vs NewSkiaLineDrawing 性能对比。
///
/// 两个实现均位于 OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/LineDrawing/，都是 internal sealed 且
/// 强依赖 DefaultSkiaRenderContext/DrawingTargetContext，无法在 benchmark 进程中直接 new。本 benchmark
/// 复制两者 PostDraw/PrepareAndDraw 的核心循环（仅剥离 OnBegin/OnEnd 的 MVP+cast 段，绘制行为等价），
/// 用真实 SKBitmap 创建的 SKCanvas 渲染，对比构建路径与提交 draw call 的 wall-clock。
///
/// 对照的 commit:
///   - DefaultSkiaLineDrawing: 已采用 reusedPath（PI#4 优化后）
///   - NewSkiaLineDrawing:     已有 Stack&lt;SKPath&gt; 池 + Run/Segment 合并 + Mesh fallback
///
/// 输入用 [Params(SegmentCount, RunPattern)] 模拟典型谱面线段：
///   - Solid:    全部 solid 同色 — NewSkia 会合并成 1 条 polyline
///   - Dashed:   全部 dashed 同色 — NewSkia 用 dashPathEffect cache
///   - Mixed:    交替不同 color — NewSkia 退化为多个 Run 但仍批
///   - Gradient: start/end color 不同 — NewSkia 走 DrawGradientRun，DefaultSkia 走逐段 DrawLine
///
/// SkiaSharp native 版本与 BDN wrapper 进程不兼容，强制 InProcessNoEmit。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class SkiaLineDrawingBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));
        }
    }

    public enum LinePattern { Solid, Dashed, Mixed, Gradient }

    [Params(50, 500, 2000)]
    public int SegmentCount;

    [Params(LinePattern.Solid, LinePattern.Dashed, LinePattern.Mixed, LinePattern.Gradient)]
    public LinePattern Pattern;

    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;
    private LineVertex[] vertices = Array.Empty<LineVertex>();
    private const float LineWidth = 2f;

    // ========= Default 实现的资源 =========
    private SKPaint defaultPaint = null!;
    private SKPath defaultReusedPath = null!;
    private readonly Dictionary<VertexDash, SKPathEffect> dashCache = new();

    // ========= New 实现的资源 =========
    private SKPaint newStrokePaint = null!;
    private SKPaint newMeshPaint = null!;
    private readonly Stack<SKPath> newPathPool = new();
    private SKPoint[] newMeshPointsBuffer = Array.Empty<SKPoint>();
    private SKColor[] newMeshColorsBuffer = Array.Empty<SKColor>();

    [GlobalSetup]
    public void Setup()
    {
        bitmap = new SKBitmap(1920, 1080);
        canvas = new SKCanvas(bitmap);

        defaultPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        defaultReusedPath = new SKPath();

        newStrokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        newMeshPaint = new SKPaint { IsAntialias = true, Color = SKColors.White };

        var rng = new Random(42);
        vertices = new LineVertex[SegmentCount + 1];

        var dashed = Pattern == LinePattern.Dashed;
        var dash = dashed ? new VertexDash(8, 4) : new VertexDash(100, 0);

        for (var i = 0; i < vertices.Length; i++)
        {
            var x = (float)rng.NextDouble() * 1920f;
            var y = (float)rng.NextDouble() * 1080f;
            Vector4 color = Pattern switch
            {
                LinePattern.Solid or LinePattern.Dashed => new Vector4(1f, 0.4f, 0.2f, 1f),
                LinePattern.Mixed => (i % 4) switch
                {
                    0 => new Vector4(1f, 0f, 0f, 1f),
                    1 => new Vector4(0f, 1f, 0f, 1f),
                    2 => new Vector4(0f, 0f, 1f, 1f),
                    _ => new Vector4(1f, 1f, 0f, 1f),
                },
                LinePattern.Gradient => new Vector4(i / (float)vertices.Length, 0.5f, 1f - i / (float)vertices.Length, 1f),
                _ => new Vector4(1, 1, 1, 1),
            };
            vertices[i] = new LineVertex(new Vector2(x, y), color, dash);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        defaultPaint.Dispose();
        defaultReusedPath.Dispose();
        newStrokePaint.Dispose();
        newMeshPaint.Dispose();
        canvas.Dispose();
        bitmap.Dispose();
        while (newPathPool.Count > 0)
            newPathPool.Pop().Dispose();
        foreach (var e in dashCache.Values)
            e?.Dispose();
        dashCache.Clear();
    }

    // ========================================================================
    // DefaultSkiaLineDrawing 等价实现
    // 对应 PostDraw + DrawPath（PI#4 后版本：reusedPath 复用）。
    // 原版按 (color,dash) 边变化分块，每块构造一条 SKPath，SetMatrix 那段已剥离。
    // ========================================================================
    [Benchmark(Baseline = true)]
    public void Default_Skia()
    {
        var pts = vertices;
        if (pts.Length < 2) return;

        canvas.Save();
        try
        {
            var prev = pts[0];
            (Vector4, VertexDash)? prevParam = null;
            using var pointsBuf = new PooledList<SKPoint>();
            pointsBuf.Add(ToSk(prev.Point));

            for (var i = 1; i < pts.Length; i++)
            {
                var cur = pts[i];
                var curParam = (cur.Color, cur.Dash);
                if (prevParam == null || curParam == prevParam)
                {
                    pointsBuf.Add(ToSk(cur.Point));
                }
                else
                {
                    UpdateDefaultPaint(prev.Color, prev.Dash, LineWidth);
                    DefaultDrawPath(pointsBuf.Span, defaultPaint);
                    pointsBuf.Clear();
                    pointsBuf.Add(ToSk(prev.Point));
                    pointsBuf.Add(ToSk(cur.Point));
                }
                prevParam = curParam;
                prev = cur;
            }
            if (pointsBuf.Count > 0)
            {
                UpdateDefaultPaint(prev.Color, prev.Dash, LineWidth);
                DefaultDrawPath(pointsBuf.Span, defaultPaint);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void UpdateDefaultPaint(Vector4 color, VertexDash dash, float lineWidth)
    {
        defaultPaint.Color = ToSkColor(color);
        defaultPaint.StrokeWidth = lineWidth;
        defaultPaint.PathEffect = GetCachedDash(dash);
    }

    private void DefaultDrawPath(ReadOnlySpan<SKPoint> points, SKPaint paint)
    {
        var path = defaultReusedPath;
        path.Reset();
        path.MoveTo(points[0]);
        for (var i = 0; i < points.Length - 1; i++)
        {
            var c = points[i];
            var n = points[i + 1];
            if (c == n) continue;
            path.LineTo(n);
        }
        canvas.DrawPath(path, paint);
    }

    // ========================================================================
    // NewSkiaLineDrawing 等价实现
    // 对应 PrepareAndDraw + BuildSegmentsAndRuns + DrawRun。
    // ========================================================================
    private const int MeshGradientSegmentThreshold = 256;
    private const float MeshAntialiasRadius = 1.5f;
    private const int MaxPooledPaths = 32;
    private const int StackPolyThreshold = 64;
    private const int StackSegmentThreshold = 32;

    [Benchmark]
    public void New_Skia()
    {
        var pts = vertices.AsSpan();
        if (pts.Length < 2) return;

        canvas.Save();
        try
        {
            var maxSegments = pts.Length - 1;
            if (maxSegments <= StackSegmentThreshold)
            {
                Span<LineSegment> segBuf = stackalloc LineSegment[StackSegmentThreshold];
                Span<Run> runBuf = stackalloc Run[StackSegmentThreshold];
                NewPrepareAndDrawCore(pts, segBuf, runBuf, LineWidth);
            }
            else
            {
                var segBuf = ArrayPool<LineSegment>.Shared.Rent(maxSegments);
                var runBuf = ArrayPool<Run>.Shared.Rent(maxSegments);
                try
                {
                    NewPrepareAndDrawCore(pts, segBuf, runBuf, LineWidth);
                }
                finally
                {
                    ArrayPool<LineSegment>.Shared.Return(segBuf, clearArray: false);
                    ArrayPool<Run>.Shared.Return(runBuf, clearArray: false);
                }
            }
        }
        finally
        {
            newStrokePaint.PathEffect = null;
            newStrokePaint.Shader = null;
            canvas.Restore();
        }
    }

    private void NewPrepareAndDrawCore(ReadOnlySpan<LineVertex> pts, Span<LineSegment> segs, Span<Run> runs, float lineWidth)
    {
        BuildSegmentsAndRuns(pts, segs, runs, out var segCount, out var runCount, out var gradCount);
        if (segCount == 0) return;
        ReadOnlySpan<LineSegment> segSpan = segs[..segCount];
        ReadOnlySpan<Run> runSpan = runs[..runCount];

        if (gradCount > MeshGradientSegmentThreshold)
        {
            DrawMeshFallback(segSpan, lineWidth);
            return;
        }
        for (var i = 0; i < runCount; i++)
            DrawRun(runSpan[i], segSpan, lineWidth);
    }

    private static void BuildSegmentsAndRuns(
        ReadOnlySpan<LineVertex> pts, Span<LineSegment> segs, Span<Run> runs,
        out int segCount, out int runCount, out int gradCount)
    {
        segCount = 0; runCount = 0; gradCount = 0;
        Run cur = default; var hasCur = false;

        for (var i = 0; i < pts.Length - 1; i++)
        {
            if (!LineSegment.TryCreate(pts[i], pts[i + 1], out var seg)) continue;
            segs[segCount] = seg;
            var idx = segCount++;
            if (seg.IsGradient) gradCount++;
            var dash = seg.Dash;
            var color = seg.StartColor;
            var isGrad = seg.IsGradient;

            if (hasCur && !isGrad && !cur.HasGradient && cur.Color == color && cur.Dash == dash)
            {
                ref var r = ref runs[runCount - 1];
                r.IsContinuousPolyline = r.IsContinuousPolyline
                    && segs[r.StartIndex + r.Count - 1].EndPoint == seg.StartPoint;
                r.Count += 1;
                continue;
            }
            cur = new Run(idx, 1, color, dash, isGrad, !isGrad);
            runs[runCount++] = cur;
            hasCur = !isGrad;
        }
    }

    private void DrawRun(Run run, ReadOnlySpan<LineSegment> segs, float lineWidth)
    {
        var sub = segs.Slice(run.StartIndex, run.Count);
        if (run.HasGradient) { DrawGradientRun(sub, lineWidth); return; }
        DrawPolylineOrSegmentsRun(sub, run.Color, run.Dash, run.IsContinuousPolyline, IsDashed(run.Dash), lineWidth);
    }

    private void DrawGradientRun(ReadOnlySpan<LineSegment> segs, float lineWidth)
    {
        for (var i = 0; i < segs.Length; i++)
        {
            var s = segs[i];
            var path = RentNewPath();
            try
            {
                path.MoveTo(ToSk(s.StartPoint));
                path.LineTo(ToSk(s.EndPoint));
                using var shader = SKShader.CreateLinearGradient(
                    ToSk(s.StartPoint), ToSk(s.EndPoint),
                    new[] { ToSkColor(s.StartColor), ToSkColor(s.EndColor) },
                    new[] { 0f, 1f }, SKShaderTileMode.Clamp);
                newStrokePaint.StrokeWidth = lineWidth;
                newStrokePaint.PathEffect = GetCachedDash(s.Dash);
                newStrokePaint.Shader = shader;
                newStrokePaint.Color = SKColors.White;
                canvas.DrawPath(path, newStrokePaint);
            }
            finally
            {
                newStrokePaint.Shader = null;
                ReturnNewPath(path);
            }
        }
    }

    private void DrawPolylineOrSegmentsRun(ReadOnlySpan<LineSegment> segs, Vector4 color, VertexDash dash,
        bool isContinuous, bool dashed, float lineWidth)
    {
        var path = RentNewPath();
        try
        {
            if (isContinuous && !dashed)
            {
                BuildContinuousPolyline(path, segs);
            }
            else
            {
                var prevEnd = new Vector2(float.NaN, float.NaN);
                for (var i = 0; i < segs.Length; i++)
                {
                    var s = segs[i];
                    if (dashed || s.StartPoint != prevEnd) path.MoveTo(ToSk(s.StartPoint));
                    path.LineTo(ToSk(s.EndPoint));
                    prevEnd = s.EndPoint;
                }
            }
            newStrokePaint.StrokeWidth = lineWidth;
            newStrokePaint.PathEffect = GetCachedDash(dash);
            newStrokePaint.Shader = null;
            newStrokePaint.Color = ToSkColor(color);
            canvas.DrawPath(path, newStrokePaint);
        }
        finally
        {
            ReturnNewPath(path);
        }
    }

    private static void BuildContinuousPolyline(SKPath path, ReadOnlySpan<LineSegment> segs)
    {
        var n = segs.Length + 1;
        if (n <= StackPolyThreshold)
        {
            Span<SKPoint> buf = stackalloc SKPoint[StackPolyThreshold];
            var slice = buf[..n];
            slice[0] = ToSk(segs[0].StartPoint);
            for (var i = 0; i < segs.Length; i++) slice[i + 1] = ToSk(segs[i].EndPoint);
            path.AddPoly(slice, close: false);
        }
        else
        {
            var rent = ArrayPool<SKPoint>.Shared.Rent(n);
            try
            {
                var slice = rent.AsSpan(0, n);
                slice[0] = ToSk(segs[0].StartPoint);
                for (var i = 0; i < segs.Length; i++) slice[i + 1] = ToSk(segs[i].EndPoint);
                path.AddPoly(slice, close: false);
            }
            finally { ArrayPool<SKPoint>.Shared.Return(rent, clearArray: false); }
        }
    }

    private void DrawMeshFallback(ReadOnlySpan<LineSegment> segs, float lineWidth)
    {
        var maxV = EstimateMeshVertexCount(segs);
        if (maxV == 0) return;

        if (newMeshPointsBuffer.Length < maxV)
        {
            newMeshPointsBuffer = new SKPoint[maxV];
            newMeshColorsBuffer = new SKColor[maxV];
        }
        else
        {
            var tail = newMeshPointsBuffer.Length - maxV;
            if (tail > 0)
            {
                Array.Clear(newMeshPointsBuffer, maxV, tail);
                Array.Clear(newMeshColorsBuffer, maxV, tail);
            }
        }

        var w = 0;
        for (var i = 0; i < segs.Length; i++)
        {
            var s = segs[i];
            AddSegmentMesh(newMeshPointsBuffer, newMeshColorsBuffer, ref w,
                s.StartPoint, s.EndPoint, s.StartColor, s.EndColor, s.Length, lineWidth);
        }
        if (w == 0) return;
        canvas.DrawVertices(SKVertexMode.Triangles, newMeshPointsBuffer, newMeshColorsBuffer, newMeshPaint);
    }

    private static int EstimateMeshVertexCount(ReadOnlySpan<LineSegment> segs)
    {
        var c = 0;
        for (var i = 0; i < segs.Length; i++) c += 18;
        return c;
    }

    private static void AddSegmentMesh(SKPoint[] points, SKColor[] colors, ref int written,
        Vector2 sp, Vector2 ep, Vector4 sc, Vector4 ec, float length, float lineWidth)
    {
        if (!(length > 0)) return;
        var d = ep - sp;
        var n = new Vector2(-d.Y, d.X) / length;
        var hw = lineWidth * 0.5f;
        var inner = n * hw;
        var outer = n * (hw + MeshAntialiasRadius);

        var siA = ToSk(sp - inner); var siB = ToSk(sp + inner);
        var eiA = ToSk(ep - inner); var eiB = ToSk(ep + inner);
        var soA = ToSk(sp - outer); var soB = ToSk(sp + outer);
        var eoA = ToSk(ep - outer); var eoB = ToSk(ep + outer);

        var skS = ToSkColor(sc); var skE = ToSkColor(ec);
        var skTS = skS.WithAlpha(0); var skTE = skE.WithAlpha(0);

        AddQuad(points, colors, ref written, soA, eoA, eiA, siA, skTS, skTE, skE, skS);
        AddQuad(points, colors, ref written, siA, eiA, eiB, siB, skS, skE, skE, skS);
        AddQuad(points, colors, ref written, siB, eiB, eoB, soB, skS, skE, skTE, skTS);
    }

    private static void AddQuad(SKPoint[] points, SKColor[] colors, ref int written,
        SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3,
        SKColor c0, SKColor c1, SKColor c2, SKColor c3)
    {
        AddVertex(points, colors, ref written, p0, c0);
        AddVertex(points, colors, ref written, p1, c1);
        AddVertex(points, colors, ref written, p2, c2);
        AddVertex(points, colors, ref written, p0, c0);
        AddVertex(points, colors, ref written, p2, c2);
        AddVertex(points, colors, ref written, p3, c3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddVertex(SKPoint[] points, SKColor[] colors, ref int written, SKPoint p, SKColor c)
    {
        points[written] = p; colors[written] = c; written++;
    }

    private SKPath RentNewPath()
    {
        if (newPathPool.Count > 0) return newPathPool.Pop();
        return new SKPath();
    }

    private void ReturnNewPath(SKPath path)
    {
        path.Reset();
        if (newPathPool.Count >= MaxPooledPaths) { path.Dispose(); return; }
        newPathPool.Push(path);
    }

    private SKPathEffect? GetCachedDash(VertexDash dash)
    {
        if (!IsDashed(dash)) return null;
        if (!dashCache.TryGetValue(dash, out var fx))
        {
            fx = SKPathEffect.CreateDash(new[] { (float)dash.DashSize, (float)dash.GapSize }, 0);
            dashCache[dash] = fx;
        }
        return fx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDashed(VertexDash d) => d.DashSize > 0 && d.GapSize > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPoint ToSk(Vector2 v) => new(v.X, v.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKColor ToSkColor(Vector4 c) => new((byte)(c.X * 255), (byte)(c.Y * 255), (byte)(c.Z * 255), (byte)(c.W * 255));

    public readonly record struct VertexDash(int DashSize, int GapSize);

    public readonly record struct LineVertex(Vector2 Point, Vector4 Color, VertexDash Dash);

    private record struct Run(int StartIndex, int Count, Vector4 Color, VertexDash Dash, bool HasGradient, bool IsContinuousPolyline);

    private readonly record struct LineSegment(Vector2 StartPoint, Vector2 EndPoint,
        Vector4 StartColor, Vector4 EndColor, VertexDash Dash, float Length)
    {
        public bool IsGradient => StartColor != EndColor;

        public static bool TryCreate(LineVertex s, LineVertex e, out LineSegment seg)
        {
            seg = default;
            var len = Vector2.Distance(s.Point, e.Point);
            if (!(len > 0)) return false;
            var d = IsDashed(s.Dash) ? s.Dash : new VertexDash(100, 0);
            seg = new LineSegment(s.Point, e.Point, s.Color, e.Color, d, len);
            return true;
        }
    }

    /// <summary>Default 实现里 ObjectPool.GetPooledList 在 OngekiFumenEditor 项目中——这里用最朴素 List 替代以避免引用主项目。</summary>
    private struct PooledList<T> : IDisposable
    {
        private T[] _array;
        private int _count;
        public PooledList()
        {
            _array = ArrayPool<T>.Shared.Rent(64);
            _count = 0;
        }
        public int Count => _count;
        public Span<T> Span => _array.AsSpan(0, _count);
        public void Add(T v)
        {
            if (_count == _array.Length)
            {
                var bigger = ArrayPool<T>.Shared.Rent(_array.Length * 2);
                Array.Copy(_array, bigger, _count);
                ArrayPool<T>.Shared.Return(_array, clearArray: false);
                _array = bigger;
            }
            _array[_count++] = v;
        }
        public void Clear() => _count = 0;
        public void Dispose()
        {
            if (_array != null)
            {
                ArrayPool<T>.Shared.Return(_array, clearArray: false);
                _array = null!;
            }
        }
    }
}
