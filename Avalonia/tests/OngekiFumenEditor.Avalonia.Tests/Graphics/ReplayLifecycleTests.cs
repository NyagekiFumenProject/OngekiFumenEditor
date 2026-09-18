using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using SkiaSharp;
using Xunit;
using Matrix4 = OpenTK.Mathematics.Matrix4;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

/// <summary>
/// PERF-RND-017 / RND-20 — replay 按 render context 缓存并跨帧复用后，
/// 固定两件事：(1) 生命周期契约（帧首必须 BeginFrame、帧末丢弃 canvas 引用、Dispose 幂等）；
/// (2) 复用后逐帧状态确实被重置 —— 同一实例连续 present 不同内容，第二帧不得残留第一帧的清屏色或几何。
/// </summary>
public sealed class ReplayLifecycleTests
{
    private const int Width = 96;
    private const int Height = 96;

    // ==================== 生命周期契约（不需要 headless） ====================

    [Fact]
    public void Replay_PresentBeforeBeginFrame_Throws()
    {
        using var replay = new SkiaDrawCommandListReplay(new DefaultSkiaDrawingManagerImpl(), new StubRenderContext());

        Assert.Throws<InvalidOperationException>(() => replay.Present(Array.Empty<DrawCommand>()));
    }

    [Fact]
    public void Replay_BeginFrame_RejectsNullCanvas()
    {
        using var replay = new SkiaDrawCommandListReplay(new DefaultSkiaDrawingManagerImpl(), new StubRenderContext());

        Assert.Throws<ArgumentNullException>(() => replay.BeginFrame(null!, CreateFrameState()));
    }

    [Fact]
    public void Replay_EndFrame_DropsCanvasReference()
    {
        var renderContext = new StubRenderContext();
        using var bitmap = new SKBitmap(8, 8);
        using var canvas = new SKCanvas(bitmap);
        using var replay = new SkiaDrawCommandListReplay(new DefaultSkiaDrawingManagerImpl(), renderContext);

        replay.BeginFrame(canvas, CreateFrameState());
        replay.Present(Array.Empty<DrawCommand>());
        replay.EndFrame();

        // EndFrame 必须清掉 canvas：replay 现在是长寿命对象，否则它会一直 root 住已失效的 lease。
        Assert.Throws<InvalidOperationException>(() => replay.Present(Array.Empty<DrawCommand>()));
    }

    [Fact]
    public void Replay_Dispose_IsIdempotent()
    {
        var replay = new SkiaDrawCommandListReplay(new DefaultSkiaDrawingManagerImpl(), new StubRenderContext());

        // 释放接线改由 context 销毁负责，重复释放（例如重复 Detach）不得抛。
        replay.Dispose();
        replay.Dispose();
    }

    // ==================== 跨帧复用重置（headless 端到端） ====================

    [AvaloniaFact]
    public async Task ReusedReplay_ResetsFrameStateBetweenFrames()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var renderControl = manager.CreateRenderControl();
        renderControl.HorizontalAlignment = HorizontalAlignment.Stretch;
        renderControl.VerticalAlignment = VerticalAlignment.Stretch;
        var window = new Window
        {
            Width = Width,
            Height = Height,
            Content = renderControl
        };
        IRenderContext? renderContext = null;
        Action<IRenderContext, TimeSpan>? renderFrame = null;

        // 每帧的内容由测试显式指定，不依赖渲染回调被调用的次数。
        var drawUpperHalf = true;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            renderContext = await manager.GetRenderContext(renderControl);
            var drawingContext = new TestDrawingContext(renderContext, Width, Height);

            renderFrame = (ctx, _) =>
            {
                var builder = manager.CreateDrawCommandListBuilder();
                try
                {
                    // 两帧用不同的清屏色与不同的线位置（视图原点在画布中心、Y 向上）：
                    // 蓝底 + 线在画布 y=32，或品红底 + 线在画布 y=64。
                    builder.SetCleanColor(drawUpperHalf ? new Vector4(0, 0, 1, 1) : new Vector4(1, 0, 1, 1));
                    builder.SetViewport(Width, Height);
                    builder.SetCurrentViewMatrix(drawingContext.CurrentDrawingTargetContext.ViewMatrix);
                    builder.SetCurrentProjectionMatrix(drawingContext.CurrentDrawingTargetContext.ProjectionMatrix);
                    builder.SetCurrentRect(drawingContext.CurrentDrawingTargetContext.ViewRelativeRect);
                    var viewY = drawUpperHalf ? 16 : -16;
                    builder.DrawSimpleLines(
                        [
                            new ILineDrawing.LineVertex(new Vector2(-40, viewY), new Vector4(1, 0, 0, 1), ILineDrawing.VertexDash.Solider),
                            new ILineDrawing.LineVertex(new Vector2(40, viewY), new Vector4(1, 0, 0, 1), ILineDrawing.VertexDash.Solider)
                        ], 8);
                    ctx.PostDrawCommandList(builder.GetDrawCommandList(), autoDispose: true);
                }
                finally
                {
                    builder.Dispose();
                }
            };
            renderContext.OnRender += renderFrame;
            renderContext.StartRendering();

            drawUpperHalf = true;
            var firstUpper = CaptureFrame(window);
            drawUpperHalf = false;
            var secondLower = CaptureFrame(window);
            drawUpperHalf = true;
            var thirdUpper = CaptureFrame(window);

            // 第一帧：蓝底 + 上线
            Assert.True(CountMatching(firstUpper, 0, Width, 0, Height, IsBlue) > Width * Height / 2,
                "Expected the first frame to be cleared to blue.");
            Assert.True(CountMatching(firstUpper, 4, Width - 4, 26, 38, IsRed) >= 400,
                "Expected the first frame to draw its line in the upper band.");
            Assert.Equal(0, CountMatching(firstUpper, 4, Width - 4, 58, 70, IsRed));

            // 第二帧：同一个 replay 实例，内容换成品红底 + 下线 —— 不得残留第一帧的任何结果
            Assert.True(CountMatching(secondLower, 0, Width, 0, Height, IsMagenta) > Width * Height / 2,
                "Expected the second frame to be cleared to magenta.");
            Assert.Equal(0, CountMatching(secondLower, 0, Width, 0, Height, IsBlue));
            Assert.True(CountMatching(secondLower, 4, Width - 4, 58, 70, IsRed) >= 400,
                "Expected the second frame to draw its line in the lower band.");
            Assert.Equal(0, CountMatching(secondLower, 4, Width - 4, 26, 38, IsRed));

            // 第三帧：内容回到第一帧 —— 复用同一实例的输出必须与第一帧逐像素一致
            Assert.True(firstUpper.Pixels.SequenceEqual(thirdUpper.Pixels),
                "Expected a reused replay to reproduce the original frame pixel for pixel.");
        }
        finally
        {
            if (renderContext is not null)
            {
                renderContext.StopRendering();
                if (renderFrame is not null)
                    renderContext.OnRender -= renderFrame;
            }

            window.Close();
        }
    }

    // ==================== helpers ====================

    private static SKBitmap CaptureFrame(Window window)
    {
        using var captured = window.CaptureRenderedFrame();
        Assert.NotNull(captured);

        using var encoded = new MemoryStream();
        captured!.Save(encoded);
        encoded.Position = 0;

        var bitmap = SKBitmap.Decode(encoded);
        Assert.NotNull(bitmap);
        return bitmap!;
    }

    private static DrawCommandListFrameState CreateFrameState() => new(
        cleanColor: null,
        viewWidth: 8,
        viewHeight: 8,
        renderScaleX: 1,
        renderScaleY: 1,
        modelMatrix: Matrix4.Identity,
        viewMatrix: Matrix4.Identity,
        projectionMatrix: Matrix4.Identity);

    private static bool IsRed(SKColor color) =>
        color.Alpha >= 200 && color.Red >= 180 && color.Green <= 80 && color.Blue <= 80;

    private static bool IsBlue(SKColor color) =>
        color.Alpha >= 250 && color.Red <= 15 && color.Green <= 15 && color.Blue >= 240;

    private static bool IsMagenta(SKColor color) =>
        color.Alpha >= 250 && color.Red >= 240 && color.Green <= 15 && color.Blue >= 240;

    private static int CountMatching(SKBitmap bitmap, int minX, int maxX, int minY, int maxY, Func<SKColor, bool> predicate)
    {
        var count = 0;
        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            if (predicate(bitmap.GetPixel(x, y)))
                count++;
        }

        return count;
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

    private sealed class TestDrawingContext : IDrawingContext
    {
        public TestDrawingContext(IRenderContext renderContext, float width, float height)
        {
            RenderContext = renderContext;
            CurrentDrawingTargetContext = new DrawingTargetContext
            {
                ViewMatrix = Matrix4.Identity,
                ProjectionMatrix = Matrix4.Identity,
                ViewWidth = width,
                ViewHeight = height
            };
        }

        public DrawingTargetContext CurrentDrawingTargetContext { get; }
        public IPerfomenceMonitor PerfomenceMonitor { get; } = new DummyPerformenceMonitor();
        public IRenderContext RenderContext { get; }

        public void Render(TimeSpan ts)
        {
        }
    }
}
