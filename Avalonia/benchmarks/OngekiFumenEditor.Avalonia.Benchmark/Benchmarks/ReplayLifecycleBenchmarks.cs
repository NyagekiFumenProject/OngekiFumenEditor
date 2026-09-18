using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;
using SkiaSharp;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-RND-017 / RND-20 — P1，replay engine 每帧创建。
/// 对照: src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs
///       src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/SkiaDrawCommandListReplay.cs
///
/// 修复前: PresentDrawCommandList 每次 present 都 new SkiaDrawCommandListReplay(...) 并在 finally 里 Dispose。
/// 渲染控件自我 invalidate 形成持续合成循环，DefaultSkiaRenderContext.RenderFrame 每次回调都走一遍，
/// 所以这是一次「逐帧固定成本」：
///   - 构造 targetContext + ReplayDrawingContext；
///   - 构造 8 个 backend 绘制对象，其中 NewSkiaLineDrawing 自带 List&lt;LineVertex&gt;(1024)
///     （LineVertex = Vector2 + Vector4 + VertexDash = 32 B，故仅该缓冲就是 32 KB/帧）、
///     Stack&lt;SKPath&gt; 池、Dictionary&lt;VertexDash, SKPathEffect&gt; 与 2 个 SKPaint，
///     DefaultSkiaStringDrawing 自带 SKFont + 2 个 SKPaint；
///   - 构造 3 个 Stack&lt;Matrix4&gt;。
/// 帧末 Dispose 又把这套 native 对象和池全部释放，于是上一轮为跨帧复用做的实例级缓存
/// （PERF-RND-011 的 path 池 / dash effect 缓存、PERF-RND-014 的字体 / paint 复用）在每一帧都被重置为冷态：
/// 构造时从空开始、帧末清空，跨帧记忆为零。RND-005 属单条命令内部的摊销，与实例是否跨帧无关，不在其列。
///
/// 修复后: replay 按 render context 缓存（DefaultSkiaDrawingManagerImpl.replayCache），每帧只走
/// SkiaDrawCommandListReplay.BeginFrame(canvas, frameState) → Present(commands) → EndFrame()，
/// native 与池在 context 销毁时才释放。
///
/// 量纲说明: 该成本与命令数无关，命令重放在新旧两侧完全相同（同一份 replay 代码、同一批命令），
/// 作差时抵消，因此本基准只测「replay 生命周期」这一项差异，不重放命令。
/// 报告值为 FramesPerInvoke 帧的合计，除以 FramesPerInvoke 即单帧成本。
///
/// 保真度: 两侧都直接使用生产类型（通过 InternalsVisibleTo 可见）。现状侧复刻的是**修复前**的生产行为
/// （每帧 new + Dispose），作为基线保留；优化侧调用的是修复后的**真实生产 API** BeginFrame/EndFrame，
/// 不是建模。SkiaSharp native 版本与 BDN wrapper 进程不兼容，强制 InProcessNoEmit。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class ReplayLifecycleBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Default));
        }
    }

    private const int FramesPerInvoke = 64;
    private const int ViewWidth = 1920;
    private const int ViewHeight = 1080;

    private DefaultSkiaDrawingManagerImpl manager = null!;
    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;
    private StubRenderContext renderContext = null!;

    // 修复后: 一次构造、跨帧存活的那一份 replay。
    private SkiaDrawCommandListReplay reusableReplay = null!;
    private DrawCommandListFrameState frameState;

    [GlobalSetup]
    public void Setup()
    {
        manager = new DefaultSkiaDrawingManagerImpl();
        bitmap = new SKBitmap(ViewWidth, ViewHeight);
        canvas = new SKCanvas(bitmap);
        renderContext = new StubRenderContext
        {
            PerfomenceMonitor = DummyPerformenceMonitor.Instance
        };

        // 这一份是「每个 render context 缓存的那个 replay」，构造开销不计入逐帧测量。
        reusableReplay = new SkiaDrawCommandListReplay(manager, renderContext);

        frameState = new DrawCommandListFrameState(
            cleanColor: new Vector4(0, 0, 0, 1),
            viewWidth: ViewWidth,
            viewHeight: ViewHeight,
            renderScaleX: 1,
            renderScaleY: 1,
            modelMatrix: Matrix4.Identity,
            viewMatrix: Matrix4.CreateTranslation(-ViewWidth / 2f, -ViewHeight / 2f, 0),
            projectionMatrix: Matrix4.Identity);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        reusableReplay.Dispose();
        canvas.Dispose();
        bitmap.Dispose();
    }

    // ========================================================================
    // 修复前: 每帧 new SkiaDrawCommandListReplay + Dispose（历史生产行为，作为基线）
    // ========================================================================

    [Benchmark(Baseline = true)]
    public void Original_NewReplayPerFrame()
    {
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            var replay = new SkiaDrawCommandListReplay(manager, renderContext);
            replay.Dispose();
        }
    }

    // ========================================================================
    // 修复后: 复用同一个 replay，每帧只走真实的 BeginFrame/EndFrame（不分配）
    // ========================================================================

    [Benchmark]
    public void Optimized_ReusedReplayBeginFramePerFrame()
    {
        for (var i = 0; i < FramesPerInvoke; i++)
        {
            reusableReplay.BeginFrame(canvas, frameState);
            reusableReplay.EndFrame();
        }
    }

    // ========================================================================
    // 成本来源隔离: 8 个 backend 对象里最重的两个（各自带 native 对象）
    // ========================================================================

    /// <summary>
    /// NewSkiaLineDrawing 的构造项: List&lt;LineVertex&gt;(1024) = 32 KB、Stack&lt;SKPath&gt;、
    /// Dictionary&lt;VertexDash, SKPathEffect&gt;、2 个 SKPaint。逐帧重建时这个 32 KB 缓冲每帧都重新申请。
    /// </summary>
    [Benchmark]
    public void Isolate_LineDrawingGraphPerFrame()
    {
        for (var i = 0; i < FramesPerInvoke; i++)
            new NewSkiaLineDrawing(manager).Dispose();
    }

    /// <summary>
    /// DefaultSkiaStringDrawing 的构造项: SKFont + 2 个 SKPaint（native），managed 分配很小，
    /// 但每帧创建又销毁 3 个 native 对象。列表里的 Allocated 只统计托管内存，不含这部分。
    /// </summary>
    [Benchmark]
    public void Isolate_StringDrawingGraphPerFrame()
    {
        for (var i = 0; i < FramesPerInvoke; i++)
            new DefaultSkiaStringDrawing(manager).Dispose();
    }

    private sealed class StubRenderContext : IRenderContext
    {
        public event Action<IRenderContext, TimeSpan>? OnRender
        {
            add { }
            remove { }
        }

        public IPerfomenceMonitor PerfomenceMonitor { get; set; } = DummyPerformenceMonitor.Instance;
        public string Name { get; set; } = string.Empty;

        public void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true)
        {
        }

        public void StartRendering()
        {
        }

        public void StopRendering()
        {
        }
    }
}
