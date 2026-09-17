using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using SkiaSharp;
using System.Numerics;
using LineVertex = OngekiFumenEditor.Avalonia.Kernel.Graphics.ILineDrawing.LineVertex;
using VertexDash = OngekiFumenEditor.Avalonia.Kernel.Graphics.ILineDrawing.VertexDash;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-RND-010 / RND-11 — P3，绘制命令粒度。
/// 对照: src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/CommonLinesDrawTargetBase.cs:25-41
///       src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/SkiaDrawCommandListReplay.cs:136-148,193-198
///       src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/Drawing/LineDrawing/NewSkiaLineDrawing.cs:92-100,187-251
///
/// 现状: CommonLinesDrawTargetBase.DrawBatch 对每条 lane 单独 FillLine → builder.DrawSimpleLines，
/// 每帧每 lane 产生一个 DrawSimpleLinesCommand。replay 侧每条命令走
/// Draw → BeginSession(OnBegin: canvas.Save + 合成 MVP) → PrepareAndDraw(RentPath + 建路径 + DrawPath) → EndSession(OnEnd: canvas.Restore)。
///
/// 优化: 把同一次 DrawBatch 内的所有 lane 顶点先汇总成一份，再发一个 DrawSimpleLinesCommand，
/// 命令数从 O(lanes) 降到 O(1)，replay 侧只做一次 Begin/End + 一次绘制。
///
/// 量纲说明: 报告值是 <see cref="FramesPerInvoke"/> 帧的合计；单帧 = ÷FramesPerInvoke。
/// 报告里的倍率只是**这条审计项所辖那部分成本**的消除比例，**不是整帧加速比** ——
/// 顶点几何的产出（PERF-RND-001 / RND-01）与颜色分色产生的 run 数在两侧相同，不在此列。
///
/// 保真度:
///   - 构建侧用真实生产类型（IDrawCommandListBuilder / DrawSimpleLinesCommand / ObjectPool）。
///   - 重放侧用真实 DefaultSkiaRenderContext + 真实 SkiaDrawCommandListReplay（真实 NewSkiaLineDrawing），
///     画到 SKBitmap 上。DefaultSkiaRenderContext 构造函数 internal，这里用不初始化字段的
///     GetUninitializedObject 取实例后只装 Canvas，其余全部走生产代码路径。
///   - 两侧**画的是同一批顶点、跑的是同一套绘制代码**，唯一差别是命令切分粒度。因此这条基准
///     就是「只改粒度、达到与现状逐像素等价」的直接对照，不需要额外说明几何或颜色差异。
///   - 顶点颜色逐 lane 互不相同：这是「跨 lane 合并」最保守的形态 —— 即使聚合成 1 条命令，
///     后端仍会按颜色切成 LaneCount 个 run，所以测到的是命令粒度本身，而不是颜色去重带来的额外收益。
///
/// SkiaSharp native 版本与 BDN wrapper 进程不兼容，强制 InProcessNoEmit。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class LineCommandGranularityBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Default));
        }
    }

    private const int FramesPerInvoke = 32;
    private const int ViewWidth = 1920;
    private const int ViewHeight = 1080;
    private const float LineWidth = 2f;
    private const int PointsPerLane = 32;
    private const float LaneSpacing = 10f;
    private const float PointSpacingY = 28f;

    [Params(8, 32, 128)]
    public int LaneCount;

    private readonly List<LineVertex[]> lanes = new();
    private readonly List<DrawCommandList> fixedLists = new();

    private DefaultSkiaDrawingManagerImpl manager = null!;
    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;
    private DefaultSkiaRenderContext renderContext = null!;
    private SkiaDrawCommandListReplay replay = null!;
    private DrawCommandListFrameState frameState;

    /// <summary>逐帧构建的两条基准共用的聚合缓冲：按总顶点数一次 EnsureCapacity，跨帧复用（池化）。</summary>
    private IPooledList<LineVertex>? mergedBuffer;

    [GlobalSetup]
    public void Setup()
    {
        PrepareCanvasAndLanes();
        PrepareReplay();
    }

    /// <summary>
    /// 两条「纯重放」隔离基准的资源。BDN 的定向 GlobalSetup 会**替代**默认 GlobalSetup 而不是叠加，
    /// 所以这里自己把位图/画布/replay/顶点全部备齐。
    /// </summary>
    [GlobalSetup(Targets = new[] { nameof(Isolate_ReplayAggregatedCommand), nameof(Isolate_ReplayPerLaneCommands) })]
    public void ReplayOnlySetup()
    {
        PrepareCanvasAndLanes();
        PrepareReplay();

        using (var builder = manager.CreateDrawCommandListBuilder())
        {
            using var merged = ObjectPool.GetPooledList<LineVertex>();
            merged.EnsureCapacity(LaneCount * PointsPerLane);
            foreach (var lane in lanes)
                merged.AddRange(lane);

            builder.DrawSimpleLines(merged, LineWidth);
            fixedLists.Add(builder.GetDrawCommandList());
        }

        using (var builder = manager.CreateDrawCommandListBuilder())
        {
            foreach (var lane in lanes)
                builder.DrawSimpleLines(lane, LineWidth);

            fixedLists.Add(builder.GetDrawCommandList());
        }
    }

    [GlobalCleanup(Targets = new[] { nameof(Isolate_ReplayAggregatedCommand), nameof(Isolate_ReplayPerLaneCommands) })]
    public void ReplayOnlyCleanup()
    {
        foreach (var list in fixedLists)
            list.Dispose();
        fixedLists.Clear();

        ReleaseResources();
    }

    [GlobalCleanup]
    public void Cleanup() => ReleaseResources();

    // ========================================================================
    // 现状: 每 lane 一个 DrawSimpleLinesCommand（O(lanes) 命令）→ replay 每命令一次 Begin/End + 建路径
    // 等价于 CommonLinesDrawTargetBase.DrawBatch 的现行实现 + SkiaDrawCommandListReplay.Present。
    // ========================================================================

    [Benchmark(Baseline = true)]
    public void Original_PerLaneCommand()
    {
        for (var frame = 0; frame < FramesPerInvoke; frame++)
        {
            using var builder = manager.CreateDrawCommandListBuilder();
            foreach (var lane in lanes)
                builder.DrawSimpleLines(lane, LineWidth);

            PresentOnce(builder.GetDrawCommandList());
        }
    }

    // ========================================================================
    // 优化: 同一批 lane 聚合成一个命令（O(1) 命令）→ replay 一次 Begin/End
    // ========================================================================

    [Benchmark]
    public void Optimized_AggregatedSingleCommand()
    {
        EnsureMergedBuffer();

        for (var frame = 0; frame < FramesPerInvoke; frame++)
        {
            using var builder = manager.CreateDrawCommandListBuilder();

            // 生产里等价于 FillLine 不再各自 Submit，而是把顶点汇总后一次 DrawSimpleLines。
            // 汇总是必要的：一个 DrawSimpleLinesCommand 只携带一份顶点序列，
            // 若按 point-count 去重，相邻命令的共享端点会把线段打断，与现状不再逐像素等价。
            // 池化缓冲 + 精确容量，避免把「汇总」自身变成新的分配点。
            mergedBuffer!.Clear();
            foreach (var lane in lanes)
                mergedBuffer.AddRange(lane);

            builder.DrawSimpleLines(mergedBuffer, LineWidth);

            PresentOnce(builder.GetDrawCommandList());
        }
    }

    // ========================================================================
    // 成本来源隔离: 把 O(lanes) 命令的成本拆成「构建」与「重放」两半，确认瓶颈在哪一侧。
    // ========================================================================

    /// <summary>只构建不重放: O(lanes) 个命令的 builder + 命令对象 + 顶点拷贝成本。</summary>
    [Benchmark]
    public void Isolate_BuildPerLaneCommand()
    {
        for (var frame = 0; frame < FramesPerInvoke; frame++)
        {
            using var builder = manager.CreateDrawCommandListBuilder();
            foreach (var lane in lanes)
                builder.DrawSimpleLines(lane, LineWidth);

            builder.Clear();
        }
    }

    /// <summary>单条聚合命令的重放：量化一次 Begin/建路径/提交的固定开销。</summary>
    [Benchmark]
    public void Isolate_ReplayAggregatedCommand()
    {
        for (var frame = 0; frame < FramesPerInvoke; frame++)
            PresentRetained(fixedLists[0]);
    }

    /// <summary>N 条 lane 命令的重放：与上一条的差 = (LaneCount-1) × 每命令固定开销。</summary>
    [Benchmark]
    public void Isolate_ReplayPerLaneCommands()
    {
        for (var frame = 0; frame < FramesPerInvoke; frame++)
            PresentRetained(fixedLists[1]);
    }

    private void EnsureMergedBuffer()
    {
        if (mergedBuffer is null)
        {
            mergedBuffer = ObjectPool.GetPooledList<LineVertex>();
            mergedBuffer.EnsureCapacity(LaneCount * PointsPerLane);
        }
    }

    private void PresentOnce(DrawCommandList list)
    {
        PresentRetained(list);
        list.Dispose();
    }

    private void PresentRetained(DrawCommandList list)
    {
        replay.BeginFrame(canvas, list.FrameState);
        try
        {
            replay.Present(list.Commands);
        }
        finally
        {
            replay.EndFrame();
        }
    }

    /// <summary>
    /// 位图 + 真实 DefaultSkiaRenderContext 实例 + 顶点数据。
    /// DefaultSkiaRenderContext 的构造函数是 internal 且要 AvaloniaSkiaRenderControl，这里用
    /// 不初始化字段的 GetUninitializedObject 取得实例后只把 Canvas 装上，其余全走生产代码路径。
    /// </summary>
    private void PrepareCanvasAndLanes()
    {
        manager = new DefaultSkiaDrawingManagerImpl();
        bitmap = new SKBitmap(ViewWidth, ViewHeight);
        canvas = new SKCanvas(bitmap);

        var ctx = (DefaultSkiaRenderContext)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(DefaultSkiaRenderContext));
        typeof(DefaultSkiaRenderContext)
            .GetProperty(nameof(DefaultSkiaRenderContext.Canvas))!
            .SetValue(ctx, canvas);
        ctx.PerfomenceMonitor = DummyPerformenceMonitor.Instance;
        renderContext = ctx;

        frameState = new DrawCommandListFrameState(
            cleanColor: new Vector4(0, 0, 0, 1),
            viewWidth: ViewWidth,
            viewHeight: ViewHeight,
            renderScaleX: 1,
            renderScaleY: 1,
            modelMatrix: Matrix4.Identity,
            viewMatrix: Matrix4.CreateTranslation(-ViewWidth / 2f, -ViewHeight / 2f, 0),
            projectionMatrix: Matrix4.Identity);

        lanes.Clear();

        var rng = new Random(42);
        var lanesWidth = (LaneCount - 1) * LaneSpacing;
        var x0 = (ViewWidth - lanesWidth) / 2f;

        for (var lane = 0; lane < LaneCount; lane++)
        {
            var laneColor = new Vector4(
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                1f);
            var x = x0 + lane * LaneSpacing;

            var pts = new LineVertex[PointsPerLane];
            for (var i = 0; i < PointsPerLane; i++)
                pts[i] = new LineVertex(new Vector2(x, 60f + i * PointSpacingY), laneColor, VertexDash.Solider);

            lanes.Add(pts);
        }
    }

    /// <summary>跨帧复用（与生产一致），构造开销不计入逐帧测量。</summary>
    private void PrepareReplay() => replay = new SkiaDrawCommandListReplay(manager, renderContext);

    private void ReleaseResources()
    {
        replay?.Dispose();
        canvas?.Dispose();
        bitmap?.Dispose();
        mergedBuffer?.Dispose();
        mergedBuffer = null;
    }
}
