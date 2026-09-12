using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using OngekiFumenEditor.Avalonia.Utils;
using SkiaSharp;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-RND-014 / RND-17 — 文字绘制高频 native 分配。
/// 对照: src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/Drawing/StringDrawing/DefaultSkiaStringDrawing.cs
///       （修复前 :46-71 MeasureString / :73-136 Draw；修复后为静态 SKTypeface 缓存 + 实例级复用 SKFont/SKPaint）
///
/// 修复前每次 Measure/Draw 都构造:
///   new SKPaint()            ← native 后端对象（纹理基准实测 ~88 B/次）
///   new SKFont()             ← native 后端对象
///   SKTypeface.FromFamilyName(...) ← 字体匹配 + 新包装 + 引用计数（同 (family, bold, italic) 每帧重复）
/// 调用点（每帧、逐字符串）: CommonHorizonalDrawingTarget.cs:161-184、DurationSoflanDrawingTarget.cs:217-230、
/// DrawTimeSignatureHelper.cs:155,172、DrawXGridHelper.cs:60、LaneJudgeLine/PlayerLocation/Projectile 等。
///
/// 修复后同样的 (family, bold, italic) 只解析一次 typeface，SKFont/SKPaint 实例级复用，稳定态零 native 分配。
///
/// 该类是 internal 且 Draw 强依赖 DefaultSkiaRenderContext（internal ctor + private-set Canvas），
/// 无法在 benchmark 进程直接 new，因此这里逐行复刻两个实现的核心循环（与 SkiaTextureDrawingBenchmarks
/// 的做法一致），用真实 SKBitmap/SKCanvas 渲染。像素等价由 headless 测试固定（SkiaRenderSmokeTests）。
///
/// OnBegin/OnEnd（canvas.Save + MVP 合成）在修复前后相同，两个变体都保留，以便时间反映真实绘制路径；
/// PerfomenceMonitor.CountDrawCall 未复刻（两变体相同，且不属于本项）。
///
/// SkiaSharp native 版本与 BDN wrapper 进程兼容性不定，保留 InProcessNoEmit（同既有多媒体/skia 基准）。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class SkiaStringDrawingBenchmarks
{
    private sealed class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Default));
        }
    }

    private const int ViewWidth = 1920;
    private const int ViewHeight = 1080;

    [Params(16, 64, 256)]
    public int StringCount;

    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;

    private string[] texts = Array.Empty<string>();
    private int[] fontSizes = Array.Empty<int>();
    private bool[] bolds = Array.Empty<bool>();
    private SKPoint[] positions = Array.Empty<SKPoint>();

    private string defaultFamilyName = string.Empty;

    // ============ 修复后实现的状态（生产类里是实例字段 + 静态缓存） ============

    private static readonly ConcurrentDictionary<(string Family, bool Bold, bool Italic), SKTypeface> TypefaceCache = new();

    private readonly SKPaint optimizedTextPaint = new();
    private readonly SKFont optimizedFont = new();

    private SKFont ConfigureOptimizedFont(int fontSize, bool bold)
    {
        optimizedFont.Typeface = TypefaceCache.GetOrAdd(
            (defaultFamilyName, bold, false),
            static key => SKTypeface.FromFamilyName(
                key.Family.Length == 0 ? null : key.Family,
                key.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                key.Italic ? SKFontStyleSlant.Oblique : SKFontStyleSlant.Upright));
        optimizedFont.Size = fontSize;
        return optimizedFont;
    }

    [GlobalSetup]
    public void Setup()
    {
        bitmap = new SKBitmap(ViewWidth, ViewHeight);
        canvas = new SKCanvas(bitmap);
        defaultFamilyName = SKTypeface.Default.FamilyName ?? string.Empty;

        var rng = new Random(42);
        texts = new string[StringCount];
        fontSizes = new int[StringCount];
        bolds = new bool[StringCount];
        positions = new SKPoint[StringCount];

        for (var i = 0; i < StringCount; i++)
        {
            // 复刻真实调用点的字符串形态（见 CommonHorizonalDrawingTarget / DurationSoflanDrawingTarget）。
            texts[i] = (i % 6) switch
            {
                0 => $"BPM:{120 + i % 200}",
                1 => $"MET:{1 + i % 7}/{2 + i % 8}",
                2 => $"[{i % 8}]{(1.0f + i % 50 / 10f):F2}x",
                3 => "/",
                4 => $"SFL:{i % 8}",
                _ => $"{i % 24}"
            };
            fontSizes[i] = (i & 1) == 0 ? 16 : 15;
            bolds[i] = (i & 1) != 0;
            positions[i] = new SKPoint(
                (float)rng.NextDouble() * ViewWidth,
                (float)rng.NextDouble() * ViewHeight);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        optimizedFont.Dispose();
        optimizedTextPaint.Dispose();
        canvas.Dispose();
        bitmap.Dispose();
    }

    // ============ 修复前: DefaultSkiaStringDrawing（每 Measure/Draw 新建 paint/font/typeface） ============

    [Benchmark(Baseline = true)]
    public int Original_Measure()
    {
        var sink = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            using var paint = new SKPaint();
            using var font = new SKFont();
            using var typeface = SKTypeface.FromFamilyName(
                defaultFamilyName.Length == 0 ? null : defaultFamilyName,
                bolds[i] ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright);

            font.Typeface = typeface;
            font.Size = fontSizes[i];
            font.MeasureText(MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var bounds, paint);
            sink += (int)bounds.Width;
        }

        return sink;
    }

    [Benchmark]
    public int Original_Draw()
    {
        var sink = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];

            ArtistBegin();
            using var paint = new SKPaint { ColorF = new SKColorF(1, 1, 1, 1) };
            using var font = new SKFont();
            using var typeface = SKTypeface.FromFamilyName(
                defaultFamilyName.Length == 0 ? null : defaultFamilyName,
                bolds[i] ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright);

            font.Typeface = typeface;
            font.Size = fontSizes[i];
            font.MeasureText(MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var bounds, paint);

            canvas.Save();
            canvas.Translate(positions[i].X - bounds.Width / 2, positions[i].Y - bounds.Height);
            canvas.Scale(1, -1);
            canvas.DrawText(text, 0, 0, font, paint);
            canvas.Restore();

            ArtistEnd();
            sink++;
        }

        return sink;
    }

    [Benchmark]
    public int Original_Frame()
    {
        // 生产一帧 = 构建期 MeasureString（builder 的 measurer 实例）+ replay 期 Draw（replay 实例）。
        var sink = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            using (var paint = new SKPaint())
            using (var font = new SKFont())
            using (var typeface = SKTypeface.FromFamilyName(
                defaultFamilyName.Length == 0 ? null : defaultFamilyName,
                bolds[i] ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright))
            {
                font.Typeface = typeface;
                font.Size = fontSizes[i];
                font.MeasureText(MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var measureBounds, paint);
                sink += (int)measureBounds.Width;
            }
        }

        return sink + Original_Draw();
    }

    // ============ 修复后: 静态 typeface 缓存 + 实例级复用 font/paint ============

    [Benchmark]
    public int Optimized_Measure()
    {
        var sink = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var font = ConfigureOptimizedFont(fontSizes[i], bolds[i]);
            font.MeasureText(MemoryMarshal.Cast<char, ushort>(texts[i].AsSpan()), out var bounds, optimizedTextPaint);
            sink += (int)bounds.Width;
        }

        return sink;
    }

    [Benchmark]
    public int Optimized_Draw()
    {
        var sink = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];

            ArtistBegin();
            var font = ConfigureOptimizedFont(fontSizes[i], bolds[i]);
            optimizedTextPaint.ColorF = new SKColorF(1, 1, 1, 1);
            font.MeasureText(MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out var bounds, optimizedTextPaint);

            canvas.Save();
            canvas.Translate(positions[i].X - bounds.Width / 2, positions[i].Y - bounds.Height);
            canvas.Scale(1, -1);
            canvas.DrawText(text, 0, 0, font, optimizedTextPaint);
            canvas.Restore();

            ArtistEnd();
            sink++;
        }

        return sink;
    }

    [Benchmark]
    public int Optimized_Frame()
    {
        var sink = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var font = ConfigureOptimizedFont(fontSizes[i], bolds[i]);
            font.MeasureText(MemoryMarshal.Cast<char, ushort>(texts[i].AsSpan()), out var bounds, optimizedTextPaint);
            sink += (int)bounds.Width;
        }

        return sink + Optimized_Draw();
    }

    // ============ 复刻 CommonSkiaDrawingBase.OnBegin/OnEnd（修复前后相同） ============

    private void ArtistBegin()
    {
        canvas.Save();

        var mvp = (Matrix4.Identity * Matrix4.CreateTranslation(-ViewWidth / 2f, -ViewHeight / 2f, 0)).ToSkiaMatrix44();
        var flip = SKMatrix44.CreateScale(1, -1, 1);
        var translation = SKMatrix44.CreateTranslation(ViewWidth / 2f, ViewHeight / 2f, 0);
        var mvpWithFlip = SKMatrix44.Concat(mvp, flip);
        var adjustMVP = SKMatrix44.Concat(mvpWithFlip, translation);
        var adjustMatrix = adjustMVP.Matrix;
        canvas.Concat(ref adjustMatrix);
    }

    private void ArtistEnd() => canvas.Restore();
}
