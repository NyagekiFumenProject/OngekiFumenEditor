using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    [Fact]
    public void SkiaRenderManager_ProvidesStringMeasurerToCommandBuilder()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        using var builder = manager.CreateDrawCommandListBuilder();

        var size = builder.MeasureString(
            "F",
            Vector2.One,
            40,
            IStringDrawing.StringStyle.Normal,
            null!);

        Assert.True(size.X > 0);
        Assert.True(size.Y > 0);
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
        Action<IRenderContext, TimeSpan>? renderFrame = null;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            await manager.WaitForInitializationIsDone();
            renderContext = await manager.GetRenderContext(renderControl);
            renderFrame = (ctx, _) =>
            {
                var builder = manager.CreateDrawCommandListBuilder();
                builder.SetCleanColor(new Vector4(1, 0, 1, 1));
                ctx.PostDrawCommandList(builder.GetDrawCommandList(), autoDispose: true);
                builder.Dispose();
            };
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
        Action<IRenderContext, TimeSpan>? renderFrame = null;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            renderContext = await manager.GetRenderContext(renderControl);
            renderFrame = (ctx, _) =>
            {
                var builder = manager.CreateDrawCommandListBuilder();
                builder.SetCleanColor(new Vector4(1, 0, 1, 1));
                ctx.PostDrawCommandList(builder.GetDrawCommandList(), autoDispose: true);
                builder.Dispose();
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
        Action<IRenderContext, TimeSpan>? renderFrame = null;

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            renderContext = await manager.GetRenderContext(renderControl);
            var drawingContext = new TestDrawingContext(renderContext, width, height);
            renderFrame = (ctx, _) =>
            {
                var builder = manager.CreateDrawCommandListBuilder();
                try
                {
                    builder.SetCleanColor(new Vector4(0, 0, 0, 1));
                    builder.SetViewport(width, height);
                    builder.SetCurrentViewMatrix(drawingContext.CurrentDrawingTargetContext.ViewMatrix);
                    builder.SetCurrentProjectionMatrix(drawingContext.CurrentDrawingTargetContext.ProjectionMatrix);
                    builder.SetCurrentRect(drawingContext.CurrentDrawingTargetContext.ViewRelativeRect);
                    builder.DrawString(
                        "F",
                        new Vector2(-12, 0),
                        Vector2.One,
                        40,
                        0,
                        new Vector4(1, 1, 1, 1),
                        new Vector2(0, 1),
                        IStringDrawing.StringStyle.Normal,
                        null!);
                    renderContext.PostDrawCommandList(builder.GetDrawCommandList(), autoDispose: true);
                }
                finally
                {
                    builder.Dispose();
                }
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

    [AvaloniaFact]
    public async Task SkiaTextureDrawing_ReusesArtistAcrossInstancesWithoutChangingPixels()
    {
        const int width = 96;
        const int height = 96;
        var manager = new DefaultSkiaDrawingManagerImpl();
        var renderControl = manager.CreateRenderControl();
        renderControl.HorizontalAlignment = HorizontalAlignment.Stretch;
        renderControl.VerticalAlignment = VerticalAlignment.Stretch;
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = renderControl
        };
        IRenderContext? renderContext = null;
        Action<IRenderContext, TimeSpan>? renderFrame = null;

        // Opaque white texture so the per-instance paint color fully determines the result.
        using var textureBitmap = new SKBitmap(16, 16);
        using (var textureCanvas = new SKCanvas(textureBitmap))
            textureCanvas.Clear(SKColors.White);
        using var png = new MemoryStream();
        using (var encoded = SKImage.FromBitmap(textureBitmap).Encode(SKEncodedImageFormat.Png, 100))
            encoded.SaveTo(png);
        png.Position = 0;
        var texture = manager.LoadImageFromStream(png);

        try
        {
            window.Show();
            window.UpdateLayout();
            await manager.InitializeRenderControl(renderControl);
            renderContext = await manager.GetRenderContext(renderControl);
            var drawingContext = new TestDrawingContext(renderContext, width, height);
            var instances = new (Vector2 size, Vector2 position, float rotation, Vector4 color)[]
            {
                // View origin maps to the canvas center (48,48) with Y up; these land the
                // sprites at canvas (24,48) and (72,48).
                (new Vector2(16, 16), new Vector2(-24, 0), 0f, new Vector4(1, 0, 0, 1)),
                (new Vector2(16, 16), new Vector2(24, 0), 0f, new Vector4(0, 0, 1, 1)),
            };
            renderFrame = (ctx, _) =>
            {
                var builder = manager.CreateDrawCommandListBuilder();
                try
                {
                    builder.SetCleanColor(new Vector4(0, 0, 0, 1));
                    builder.SetViewport(width, height);
                    builder.SetCurrentViewMatrix(drawingContext.CurrentDrawingTargetContext.ViewMatrix);
                    builder.SetCurrentProjectionMatrix(drawingContext.CurrentDrawingTargetContext.ProjectionMatrix);
                    builder.SetCurrentRect(drawingContext.CurrentDrawingTargetContext.ViewRelativeRect);
                    builder.DrawTexture(texture, instances);
                    ctx.PostDrawCommandList(builder.GetDrawCommandList(), autoDispose: true);
                }
                finally
                {
                    builder.Dispose();
                }
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

            // SkiaSharp 3.x does not tint DrawImage with SKPaint.Color, so the sprites keep the
            // source texture pixels (white). This guards that the shared-artist refactor still
            // draws every instance at its own position with a balanced canvas save stack.
            var leftSpritePixels = CountMatchingPixels(bitmap, 0, width / 2, 0, height,
                static color => color.Alpha >= 200 && color.Red >= 200 && color.Green >= 200 && color.Blue >= 200);
            var rightSpritePixels = CountMatchingPixels(bitmap, width / 2, width, 0, height,
                static color => color.Alpha >= 200 && color.Red >= 200 && color.Green >= 200 && color.Blue >= 200);
            var backgroundPixels = CountMatchingPixels(bitmap, 0, width, 0, height,
                static color => color.Alpha >= 250 && color.Red <= 15 && color.Green <= 15 && color.Blue <= 15);

            Assert.True(leftSpritePixels >= 200,
                $"Expected a sprite in the left half, but found {leftSpritePixels} lit pixels.");
            Assert.True(rightSpritePixels >= 200,
                $"Expected a sprite in the right half, but found {rightSpritePixels} lit pixels.");
            Assert.True(backgroundPixels > width * height / 2,
                $"Expected the clean color to fill the frame, but found only {backgroundPixels} background pixels.");
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
