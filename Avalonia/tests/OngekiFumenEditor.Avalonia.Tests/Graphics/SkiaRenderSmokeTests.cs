using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using SkiaSharp;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

public sealed class SkiaRenderSmokeTests
{
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
}
