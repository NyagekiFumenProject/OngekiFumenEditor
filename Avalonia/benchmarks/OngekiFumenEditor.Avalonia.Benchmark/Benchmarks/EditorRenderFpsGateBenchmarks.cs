using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 审计项 RND-C2（P1）—— FPS 限帧闸门位于 CreateDrawCommandListBuilder() **之后**。
///
/// 对照现状：
///   src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs
///     修复前 :287  var builder = renderImpl?.CreateDrawCommandListBuilder();
///             :289-296  if (actualRenderInterval > 0) { if (ms &lt; actualRenderInterval) goto End; ... }
///     修复后      先做限帧判断，通过后才创建 builder（builder 在闸门前声明为 null）
///
/// 为什么慢：被限帧丢弃的帧不会生成任何绘制命令，却仍然完整构建了一次 builder：
///   - DrawCommandListBuilder ctor（Kernel/Graphics/DrawCommands/DrawCommandListBuilder.cs:37-47）
///     从对象池租借 2 个 PooledDictionary&lt;Type,int&gt; + 4 个 PooledList（commands / 三个矩阵栈）
///   - DefaultSkiaStringDrawing ctor（.../StringDrawing/DefaultSkiaStringDrawing.cs:27-29）
///     构造 3 个原生 Skia 对象：textPaint、decorationPaint（SKPaint）与 reusableFont（SKFont）
/// 被丢弃帧随后在 End: 处立刻 Dispose 掉这一切。
///
/// 优化做了什么：把闸门提到创建之前，被丢弃的帧直接 goto End，builder 保持 null，
/// 于是这些池租借与原生对象构造/析构全部消失。跳过的帧原本也不执行 ClearHitObjects() /
/// drawingContexts.Clear()（goto End 在它们之前），改动后保持同样行为。
///
/// 量纲说明（重要）：
///   报告值是「一次被丢弃帧的构建成本」（逐帧量纲），两种实现各报一次。
///   实际每秒浪费 = 被丢弃帧数 × 该成本；以 LimitFPS=60 而合成器 120Hz 回调为例，
///   约一半回调被丢弃。它**不是**整帧加速比 —— 出帧帧的成本两种实现相同，作差会抵消。
///   故基准只测「被丢弃帧」这一条路径。
///
/// 保真度说明：
///   走**真实生产路径**：真 DefaultSkiaDrawingManagerImpl.CreateDrawCommandListBuilder()，
///   即真的 DrawCommandListBuilder + 真的 DefaultSkiaStringDrawing（含其 3 个原生 Skia 对象），
///   不是合成复刻。唯一未覆盖的是 OnEditorRender 里其余与 builder 无关的前置工作
///   （ClearHitObjects / drawingContexts.Clear），它们在新旧实现里相同且不属于本项。
///   出帧帧的 builder 后续使用（命令写入、GetDrawCommandList、replay）不在本项范围内。
///
/// InProcess 原因：会构造原生 SkiaSharp 对象（SKPaint/SKFont），
/// SkiaSharp native 与 BDN 的 wrapper 进程兼容性不定，故保留 InProcessNoEmit（同既有 Skia 基准）。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class EditorRenderFpsGateBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Default));
        }
    }

    private readonly DefaultSkiaDrawingManagerImpl renderImpl = new();

    [GlobalSetup]
    public void Setup()
    {
        // 先热身一次，确保可能的静态初始化（如字体枚举）不计入被测区间。
        using var warmup = renderImpl.CreateDrawCommandListBuilder();
    }

    /// <summary>修复前：无论该帧是否会被丢弃，都先创建 builder。</summary>
    [Benchmark(Baseline = true)]
    public int Original_DiscardedFrame()
    {
        // 复刻修复前 :287：先创建
        var builder = renderImpl.CreateDrawCommandListBuilder();

        // :289-296 的限帧判断：这一帧被丢弃（模拟 ms < actualRenderInterval）
        try
        {
            return 0;
        }
        finally
        {
            // 复刻 End: 处的 builder?.Dispose()
            builder?.Dispose();
        }
    }

    /// <summary>修复后：闸门先判断，丢弃帧根本不创建 builder。</summary>
    [Benchmark]
    public int Optimized_DiscardedFrame()
    {
        // 先判断（修复后的顺序）：这一帧被丢弃，builder 保持 null
        IDrawCommandListBuilder builder = null;

        try
        {
            return 0;
        }
        finally
        {
            // 复刻 End: 处的 builder?.Dispose()：null 时是空操作
            builder?.Dispose();
        }
    }

    /// <summary>
    /// 隔离项：只量 DefaultSkiaStringDrawing 的构造（3 个原生 SKPaint/SKFont），
    /// 用于回答「成本里有多少来自原生对象」。
    /// </summary>
    [Benchmark]
    public int Isolate_NativeStringDrawingCtor()
    {
        using var drawing = new DefaultSkiaStringDrawing(renderImpl);
        return 0;
    }

    /// <summary>
    /// 隔离项：只量 DrawCommandListBuilder 的构造（6 次池租借），
    /// 与上一项相加约等于整体成本。
    /// </summary>
    [Benchmark]
    public int Isolate_PooledBuilderCtor()
    {
        using var builder = new DrawCommandListBuilder();
        return 0;
    }
}
