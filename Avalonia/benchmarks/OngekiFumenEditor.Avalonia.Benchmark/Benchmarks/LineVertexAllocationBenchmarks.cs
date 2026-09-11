using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-RND-006 / RND-07 — 顶点堆分配。
/// 对照: OngekiFumenEditor/Kernel/Graphics/ILineDrawing.cs:8-13
///       OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/VisibleLineVerticesQuery.cs:27-156
///       调用方: CommonLinesDrawTargetBase.cs:25-39、Holds/HoldDrawingTarget.cs:129-162
///
/// 当前实现把 LineVertex 与 VertexDash 都定义为 record class。VisibleLineVerticesQuery.PostPoint2
/// 对每个 lane child / curve point 执行 `new LineVertex(...)`；query 的 “optimize vertices”（:131-152）
/// 与 HoldDrawingTarget 只以引用搬动这些对象。调用方用 ObjectPool.GetPooledList&lt;LineVertex&gt;()
/// 复用的是 List 的底层数组，不是元素本身，因此每帧可见 lane/hold/beam 的每个顶点都是一次堆分配，
/// 经 DrawSimpleLines/DrawLines（DrawCommandListBuilder.cs:190-220）再引用进命令缓冲。
///
/// 预计新实现把 LineVertex（及 VertexDash）改为 readonly record struct：值类型内联存放在池化
/// List/数组/命令缓冲里；record 的值相等与 with 语法均保留（DurationSoflanDrawingTarget.PushLine
/// :62-75 使用 `start with { ... }`）。
///
/// 本 benchmark 复刻“逐点 new + optimize vertices 去重”流程，对比 class / struct 两条路径的
/// wall-clock 与分配；struct 另给预分配数组缓冲变体。SkiaSharp native 与 BDN wrapper 进程不兼容的
/// 约束沿用仓内约定，强制 InProcessNoEmit。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class LineVertexAllocationBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Default));
        }
    }

    [Params(256, 2048)]
    public int VertexCount;

    // ===== 当前实现：record class（对应 ILineDrawing.cs:8-13） =====
    public sealed record VertexDashClass(int DashSize, int GapSize);
    public sealed record LineVertexClass(Vector2 Point, Vector4 Color, VertexDashClass Dash);

    // ===== 预计新实现：readonly record struct =====
    public readonly record struct VertexDashStruct(int DashSize, int GapSize);
    public readonly record struct LineVertexStruct(Vector2 Point, Vector4 Color, VertexDashStruct Dash);

    private static readonly Vector4 VertexColor = new(1f, 0.5f, 0.25f, 1f);

    private Vector2[] positions = Array.Empty<Vector2>();
    private VertexDashClass dashClass = null!;
    private VertexDashStruct dashStruct;

    private readonly List<LineVertexClass> classBuild = new();
    private readonly List<LineVertexClass> classOut = new();
    private readonly List<LineVertexStruct> structBuild = new();
    private readonly List<LineVertexStruct> structOut = new();
    private LineVertexStruct[] structBuffer = Array.Empty<LineVertexStruct>();

    [GlobalSetup]
    public void Setup()
    {
        dashClass = new VertexDashClass(100, 0);
        dashStruct = new VertexDashStruct(100, 0);
        structBuffer = new LineVertexStruct[VertexCount];

        var rng = new Random(42);
        positions = new Vector2[VertexCount];
        for (var i = 0; i < positions.Length; i++)
        {
            // 落在小网格上制造重复点，让 “optimize vertices” 分支真实执行。
            positions[i] = new Vector2(rng.Next(0, 32) * 8f, rng.Next(0, 32) * 8f);
        }
    }

    [Benchmark(Baseline = true)]
    public int Original_ClassVertices()
    {
        var build = classBuild;
        build.Clear();
        for (var i = 0; i < positions.Length; i++)
            build.Add(new LineVertexClass(positions[i], VertexColor, dashClass));

        return DedupClass(build, classOut);
    }

    [Benchmark]
    public int New_StructVertices()
    {
        var build = structBuild;
        build.Clear();
        for (var i = 0; i < positions.Length; i++)
            build.Add(new LineVertexStruct(positions[i], VertexColor, dashStruct));

        return DedupStruct(build, structOut);
    }

    [Benchmark]
    public int New_StructReusedBuffer()
    {
        var buffer = structBuffer;
        var count = positions.Length;
        for (var i = 0; i < count; i++)
            buffer[i] = new LineVertexStruct(positions[i], VertexColor, dashStruct);

        return DedupStruct(buffer, count, structOut);
    }

    // DurationSoflanDrawingTarget.PushLine 的 `start with { ... }` 路径：
    // class 的 with 每次产生新对象，struct 为值拷贝。
    [Benchmark]
    public LineVertexClass Original_ClassWithExpression()
        => new LineVertexClass(positions[0], VertexColor, dashClass) with { Color = new Vector4(0, 1, 0, 1) };

    [Benchmark]
    public LineVertexStruct New_StructWithExpression()
        => new LineVertexStruct(positions[0], VertexColor, dashStruct) with { Color = new Vector4(0, 1, 0, 1) };

    // ===== 复刻 VisibleLineVerticesQuery.cs:131-156 的 “optimize vertices” =====

    private static int DedupClass(List<LineVertexClass> src, List<LineVertexClass> dst)
    {
        dst.Clear();
        var idx = 0;
        for (; idx < src.Count - 3; idx++)
        {
            var a1 = src[idx];
            var a2 = src[idx + 1];
            var b1 = src[idx + 2];
            var b2 = src[idx + 3];

            if (!(a1 == b1 && a2 == b2))
                dst.Add(a1);

            if ((a1.Point.X == a2.Point.X && a2.Point.X == b1.Point.X) || (a1.Point.Y == a2.Point.Y && a2.Point.Y == b1.Point.Y))
            {
                dst.Add(b1);
                idx += 2;
            }
            else if (a1.Point == a2.Point)
            {
                idx += 1;
            }
        }

        for (; idx < src.Count; idx++)
            dst.Add(src[idx]);

        return dst.Count;
    }

    private static int DedupStruct(IReadOnlyList<LineVertexStruct> src, List<LineVertexStruct> dst)
    {
        dst.Clear();
        var count = src.Count;
        var idx = 0;
        for (; idx < count - 3; idx++)
        {
            var a1 = src[idx];
            var a2 = src[idx + 1];
            var b1 = src[idx + 2];
            var b2 = src[idx + 3];

            if (!(a1 == b1 && a2 == b2))
                dst.Add(a1);

            if ((a1.Point.X == a2.Point.X && a2.Point.X == b1.Point.X) || (a1.Point.Y == a2.Point.Y && a2.Point.Y == b1.Point.Y))
            {
                dst.Add(b1);
                idx += 2;
            }
            else if (a1.Point == a2.Point)
            {
                idx += 1;
            }
        }

        for (; idx < count; idx++)
            dst.Add(src[idx]);

        return dst.Count;
    }

    private static int DedupStruct(LineVertexStruct[] src, int count, List<LineVertexStruct> dst)
    {
        dst.Clear();
        var idx = 0;
        for (; idx < count - 3; idx++)
        {
            var a1 = src[idx];
            var a2 = src[idx + 1];
            var b1 = src[idx + 2];
            var b2 = src[idx + 3];

            if (!(a1 == b1 && a2 == b2))
                dst.Add(a1);

            if ((a1.Point.X == a2.Point.X && a2.Point.X == b1.Point.X) || (a1.Point.Y == a2.Point.Y && a2.Point.Y == b1.Point.Y))
            {
                dst.Add(b1);
                idx += 2;
            }
            else if (a1.Point == a2.Point)
            {
                idx += 1;
            }
        }

        for (; idx < count; idx++)
            dst.Add(src[idx]);

        return dst.Count;
    }
}
