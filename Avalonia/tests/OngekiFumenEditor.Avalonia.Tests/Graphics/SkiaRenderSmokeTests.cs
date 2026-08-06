using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using SkiaSharp;
using Xunit;
using Matrix4 = OpenTK.Mathematics.Matrix4;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

public sealed class SkiaRenderSmokeTests
{
    [AvaloniaFact]
    public void SkiaRenderControl_PointerInput_ReachesRenderSurface()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var renderControl = manager.CreateRenderControl();
        var pressedCount = 0;
        var wheelCount = 0;
        renderControl.PointerPressed += (_, _) => pressedCount++;
        renderControl.PointerWheelChanged += (_, _) => wheelCount++;
        var window = new Window
        {
            Width = 96,
            Height = 64,
            Content = renderControl
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();

            window.MouseMove(new Point(32, 24), RawInputModifiers.None);
            window.MouseDown(new Point(32, 24), MouseButton.Left, RawInputModifiers.None);
            window.MouseWheel(new Point(32, 24), new global::Avalonia.Vector(0, 1), RawInputModifiers.None);
            window.MouseUp(new Point(32, 24), MouseButton.Left, RawInputModifiers.None);

            Assert.True(renderControl.Focusable);
            Assert.Equal(1, pressedCount);
            Assert.Equal(1, wheelCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RenderManager_RejectsControlsThatDoNotOwnItsSkiaContext()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var unrelatedControl = new Border();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await manager.InitializeRenderControl(unrelatedControl));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await manager.GetRenderContext(unrelatedControl));
    }

    [AvaloniaFact]
    public async Task SkiaRenderControl_CleanFrame_ProducesExpectedNonTransparentPixels()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var renderControl = manager.CreateRenderControl();
        renderControl.HorizontalAlignment = HorizontalAlignment.Stretch;
        renderControl.VerticalAlignment = VerticalAlignment.Stretch;
        var window = new Window
        {
            Width = 96,
            Height = 64,
            Content = renderControl
        };
        IRenderContext? renderContext = null;
        Action<TimeSpan>? renderFrame = null;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            await manager.WaitForInitializationIsDone();
            renderContext = await manager.GetRenderContext(renderControl);
            renderFrame = _ => renderContext.CleanRender(null!, new Vector4(1, 0, 1, 1));
            renderContext.OnRender += renderFrame;
            renderContext.StartRendering();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var capturedFrame = frame!;
            Assert.Equal(new PixelSize(96, 64), capturedFrame.PixelSize);

            using var encodedFrame = new MemoryStream();
            capturedFrame.Save(encodedFrame);
            encodedFrame.Position = 0;
            using var bitmap = SKBitmap.Decode(encodedFrame);
            Assert.NotNull(bitmap);
            Assert.Equal(96, bitmap.Width);
            Assert.Equal(64, bitmap.Height);

            var targetColorPixels = bitmap.Pixels.Count(static color =>
                color.Alpha >= 250 && color.Red >= 240 && color.Green <= 15 && color.Blue >= 240);
            var nonTransparentPixels = bitmap.Pixels.Count(static color => color.Alpha > 0);
            Assert.True(targetColorPixels > bitmap.Width * bitmap.Height / 2,
                $"Expected an opaque magenta frame, but found only {targetColorPixels} target pixels.");
            Assert.True(nonTransparentPixels > 0, "The product Skia lease path rendered a blank frame.");
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

    [AvaloniaFact]
    public async Task SkiaRenderControl_CleanFrame_DoesNotOverwriteSiblingControl()
    {
        const int width = 120;
        const int height = 64;
        const int siblingWidth = 40;
        var manager = new DefaultSkiaDrawingManagerImpl();
        var renderControl = manager.CreateRenderControl();
        var sibling = new Border
        {
            Background = Brushes.Lime
        };
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(siblingWidth) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        content.Children.Add(sibling);
        content.Children.Add(renderControl);
        Grid.SetColumn(renderControl, 1);

        var window = new Window
        {
            Width = width,
            Height = height,
            Content = content
        };
        IRenderContext? renderContext = null;
        Action<TimeSpan>? renderFrame = null;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            renderContext = await manager.GetRenderContext(renderControl);
            renderFrame = _ => renderContext.CleanRender(null!, new Vector4(1, 0, 1, 1));
            renderContext.OnRender += renderFrame;
            renderContext.StartRendering();

            using var capturedFrame = window.CaptureRenderedFrame();
            Assert.NotNull(capturedFrame);
            using var encodedFrame = new MemoryStream();
            capturedFrame!.Save(encodedFrame);
            encodedFrame.Position = 0;
            using var bitmap = SKBitmap.Decode(encodedFrame);
            Assert.NotNull(bitmap);

            var siblingPixels = CountMatchingPixels(
                bitmap,
                4,
                siblingWidth - 4,
                4,
                height - 4,
                static color => color.Alpha >= 250 && color.Red <= 15 && color.Green >= 240 && color.Blue <= 15);
            var editorPixels = CountMatchingPixels(
                bitmap,
                siblingWidth + 4,
                width - 4,
                4,
                height - 4,
                static color => color.Alpha >= 250 && color.Red >= 240 && color.Green <= 15 && color.Blue >= 240);

            Assert.True(siblingPixels > (siblingWidth - 8) * (height - 8) * 3 / 4,
                $"Expected the sibling control to remain lime, but found only {siblingPixels} matching pixels.");
            Assert.True(editorPixels > (width - siblingWidth - 8) * (height - 8) * 3 / 4,
                $"Expected the editor surface to be magenta, but found only {editorPixels} matching pixels.");
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

    [AvaloniaFact]
    public async Task SkiaStringDrawing_RendersAsymmetricGlyphUpright()
    {
        const int width = 96;
        const int height = 96;
        var manager = new DefaultSkiaDrawingManagerImpl();
        var renderControl = manager.CreateRenderControl();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = renderControl
        };
        IRenderContext? renderContext = null;
        Action<TimeSpan>? renderFrame = null;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            renderContext = await manager.GetRenderContext(renderControl);
            var drawingContext = new TestDrawingContext(renderContext, width, height);
            renderFrame = elapsed =>
            {
                renderContext.CleanRender(drawingContext, new Vector4(0, 0, 0, 1));
                manager.StringDrawing.Draw(
                    "F",
                    new Vector2(-12, 0),
                    Vector2.One,
                    40,
                    0,
                    new Vector4(1, 1, 1, 1),
                    new Vector2(0, 1),
                    IStringDrawing.StringStyle.Normal,
                    drawingContext,
                    null!,
                    out _);
            };
            renderContext.OnRender += renderFrame;
            renderContext.StartRendering();

            using var capturedFrame = window.CaptureRenderedFrame();
            Assert.NotNull(capturedFrame);
            using var encodedFrame = new MemoryStream();
            capturedFrame!.Save(encodedFrame);
            encodedFrame.Position = 0;
            using var bitmap = SKBitmap.Decode(encodedFrame);
            Assert.NotNull(bitmap);

            var litPixels = Enumerable.Range(0, bitmap.Height)
                .SelectMany(y => Enumerable.Range(0, bitmap.Width)
                    .Where(x => bitmap.GetPixel(x, y).Red >= 128)
                    .Select(x => (x, y)))
                .ToArray();
            Assert.NotEmpty(litPixels);

            var minY = litPixels.Min(static pixel => pixel.y);
            var maxY = litPixels.Max(static pixel => pixel.y);
            var middleY = (minY + maxY) / 2d;
            var upperPixels = litPixels.Count(pixel => pixel.y <= middleY);
            var lowerPixels = litPixels.Count(pixel => pixel.y > middleY);

            Assert.True(upperPixels > lowerPixels,
                $"Expected an upright, top-heavy 'F', but upper/lower pixel counts were {upperPixels}/{lowerPixels}.");
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

    private static int CountMatchingPixels(
        SKBitmap bitmap,
        int minX,
        int maxX,
        int minY,
        int maxY,
        Func<SKColor, bool> predicate)
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
}
