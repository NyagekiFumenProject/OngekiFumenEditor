using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using OngekiFumenEditor.Avalonia.Utils;
using SkiaSharp;
using System.Numerics;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// PERF-RND-005 / RND-06 — 逐实例纹理绘制。
/// 对照: OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultTextureDrawing.cs:23-57
///       OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultSkiaBatchTextureDrawing.cs:45-67
///
/// 原实现 DefaultSkiaTextureDrawing.Draw 对每个实例调用私有 Draw，每个实例都：
///   OnBegin()（canvas.Save + 一次完整 MVP 矩阵合成 —— SKMatrix44 在 SkiaSharp 3.x 是 struct，
///              无堆分配，但每次都要重复 Matrix4 乘法 + CreateScale/CreateTranslation/Concat 计算 + Concat）
///   canvas.Save/Translate/Rotate/Scale
///   new SKPaint()   ← 每实例一次托管分配（实测 88 B/实例）
///   DrawImage
///   canvas.Restore + OnEnd()（canvas.Restore）
///
/// 新实现（方案 A，已落地于 DefaultSkiaTextureDrawing）：把 OnBegin/OnEnd 与 SKPaint 提到循环外，
/// 与既有 batch 路径（DefaultSkiaBatchTextureDrawing，同文件已实现）语义一致：每实例只剩一次
/// Save/变换/DrawImage/Restore。
///
/// DefaultSkiaTextureDrawing 是 internal 且强依赖 DefaultSkiaRenderContext/DrawingTargetContext，
/// 无法在 benchmark 进程直接 new；这里复制其核心循环（仅 MVP 输入用固定 model/view/viewport，
/// 绘制行为与像素结果等价），用真实 SKBitmap/SKCanvas 渲染，对比 wall-clock 与分配。
///
/// SkiaSharp native 版本与 BDN wrapper 进程不兼容，强制 InProcessNoEmit。
/// </summary>
[MemoryDiagnoser]
[Config(typeof(InProcessConfig))]
public class SkiaTextureDrawingBenchmarks
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

    [Params(64, 512, 2048)]
    public int InstanceCount;

    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;
    private SKImage texture = null!;
    private (Vector2 size, Vector2 position, float rotation, Vector4 color)[] instances = Array.Empty<(Vector2, Vector2, float, Vector4)>();

    private Matrix4 modelMatrix;
    private Matrix4 viewMatrix;

    [GlobalSetup]
    public void Setup()
    {
        bitmap = new SKBitmap(ViewWidth, ViewHeight);
        canvas = new SKCanvas(bitmap);

        using (var textureBitmap = new SKBitmap(24, 24))
        {
            using (var textureCanvas = new SKCanvas(textureBitmap))
                textureCanvas.Clear(new SKColor(255, 128, 0, 255));
            texture = SKImage.FromBitmap(textureBitmap);
        }

        // 与编辑器 de-globalized view matrix 同形，仅用于复现 OnBegin 的 MVP 合成成本。
        modelMatrix = Matrix4.Identity;
        viewMatrix = Matrix4.CreateTranslation(-ViewWidth / 2f, -ViewHeight / 2f, 0);

        var rng = new Random(42);
        instances = new (Vector2, Vector2, float, Vector4)[InstanceCount];
        for (var i = 0; i < instances.Length; i++)
        {
            var position = new Vector2(
                (float)rng.NextDouble() * ViewWidth,
                (float)rng.NextDouble() * ViewHeight);
            instances[i] = (new Vector2(24, 24), position, 0f, Vector4.One);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        texture.Dispose();
        canvas.Dispose();
        bitmap.Dispose();
    }

    // ============ Original: DefaultSkiaTextureDrawing（逐实例 OnBegin/new SKPaint） ============

    [Benchmark(Baseline = true)]
    public int Original_PerInstanceArtist()
    {
        var calls = 0;
        foreach (var instance in instances)
            calls += DrawPerInstance(instance);
        return calls;
    }

    private int DrawPerInstance((Vector2 size, Vector2 position, float rotation, Vector4 color) instance)
    {
        ArtistBegin();

        canvas.Save();
        ApplyTransform(instance);
        using (var paint = new SKPaint())
        {
            paint.Color = instance.color.ToSKColor();
            canvas.DrawImage(texture, GetRect(instance), paint);
        }
        canvas.Restore();

        ArtistEnd();
        return 1;
    }

    // ============ Optimized: 方案 A（OnBegin/OnEnd 与 SKPaint 提到循环外） ============

    [Benchmark]
    public int Optimized_HoistedArtist()
    {
        ArtistBegin();
        try
        {
            var calls = 0;
            using (var paint = new SKPaint())
            {
                foreach (var instance in instances)
                {
                    paint.Color = instance.color.ToSKColor();

                    canvas.Save();
                    ApplyTransform(instance);
                    canvas.DrawImage(texture, GetRect(instance), paint);
                    canvas.Restore();

                    calls++;
                }
            }
            return calls;
        }
        finally
        {
            ArtistEnd();
        }
    }

    // ============ Reference: 既有 batch 路径 DefaultSkiaBatchTextureDrawing ============

    private readonly List<(Vector2 size, Vector2 position, float rotation, Vector4 color)> batchList = new();

    [Benchmark]
    public int Reference_ExistingBatchPose()
    {
        batchList.Clear();
        batchList.AddRange(instances);

        ArtistBegin();
        try
        {
            var calls = 0;
            using (var paint = new SKPaint())
            {
                foreach (var (size, position, rotation, color) in batchList)
                {
                    paint.Color = color.ToSKColor();

                    var adjustSize = new Vector2(Math.Abs(size.X), Math.Abs(size.Y));
                    canvas.Save();
                    canvas.Translate(position.X, position.Y);
                    canvas.RotateRadians(rotation);
                    canvas.Scale(Math.Sign(size.X), -1 * Math.Sign(size.Y));
                    canvas.DrawImage(texture, SKRect.Create(-adjustSize.X / 2, -adjustSize.Y / 2, adjustSize.X, adjustSize.Y), paint);
                    canvas.Restore();

                    calls++;
                }
            }
            return calls;
        }
        finally
        {
            ArtistEnd();
        }
    }

    // ============ 复刻 CommonSkiaDrawingBase.OnBegin/OnEnd（DefaultTextureDrawing 每实例调用） ============

    private void ArtistBegin()
    {
        canvas.Save();

        var mvp = (modelMatrix * viewMatrix).ToSkiaMatrix44();
        var flip = SKMatrix44.CreateScale(1, -1, 1);
        var translation = SKMatrix44.CreateTranslation(ViewWidth / 2f, ViewHeight / 2f, 0);
        var mvpWithFlip = SKMatrix44.Concat(mvp, flip);
        var adjustMVP = SKMatrix44.Concat(mvpWithFlip, translation);
        var adjustMatrix = adjustMVP.Matrix;
        canvas.Concat(ref adjustMatrix);
    }

    private void ArtistEnd() => canvas.Restore();

    private void ApplyTransform((Vector2 size, Vector2 position, float rotation, Vector4 color) instance)
    {
        canvas.Translate(instance.position.X, instance.position.Y);
        canvas.RotateRadians(instance.rotation);
        canvas.Scale(Math.Sign(instance.size.X), -1 * Math.Sign(instance.size.Y));
    }

    private static SKRect GetRect((Vector2 size, Vector2 position, float rotation, Vector4 color) instance)
    {
        var adjustSize = new Vector2(Math.Abs(instance.size.X), Math.Abs(instance.size.Y));
        return SKRect.Create(-adjustSize.X / 2, -adjustSize.Y / 2, adjustSize.X, adjustSize.Y);
    }
}
