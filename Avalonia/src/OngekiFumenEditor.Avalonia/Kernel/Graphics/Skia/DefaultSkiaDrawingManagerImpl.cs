using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.BeamDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.CircleDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.TextureDrawing;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

[RegisterSingleton<IRenderManagerImpl>]
public class DefaultSkiaDrawingManagerImpl : IRenderManagerImpl
{
    private readonly TaskCompletionSource initTaskSource = new();

    public string Name { get; } = "Skia";

    public ICircleDrawing CircleDrawing { get; }
    public ILineDrawing LineDrawing { get; }
    public ISimpleLineDrawing SimpleLineDrawing { get; }
    public IStaticVBODrawing StaticVBODrawing { get; }
    public IStringDrawing StringDrawing { get; }
    public ISvgDrawing SvgDrawing { get; }
    public ITextureDrawing TextureDrawing { get; }
    public IBatchTextureDrawing BatchTextureDrawing { get; }
    public IHighlightBatchTextureDrawing HighlightBatchTextureDrawing { get; }
    public IPolygonDrawing PolygonDrawing { get; }
    public IBeamDrawing BeamDrawing { get; }

    public DefaultSkiaDrawingManagerImpl()
    {
        CircleDrawing = new DefaultSkiaCircleDrawing(this);
        LineDrawing = new DefaultSkiaLineDrawing(this);
        SimpleLineDrawing = new DefaultSkiaLineDrawing(this);
        StaticVBODrawing = (IStaticVBODrawing)SimpleLineDrawing;
        StringDrawing = new DefaultSkiaStringDrawing(this);
        SvgDrawing = new UnsupportedSkiaSvgDrawing();
        TextureDrawing = new DefaultSkiaBatchTextureDrawing(this);
        BatchTextureDrawing = (IBatchTextureDrawing)TextureDrawing;
        HighlightBatchTextureDrawing = new DefaultSkiaHighlightBatchTextureDrawing(this);
        PolygonDrawing = new Drawing.PolygonDrawing.DefaultSkiaPolygonDrawing(this);
        BeamDrawing = new DefaultSkiaBeamDrawing(this);
    }

    public Task WaitForInitializationIsDone(CancellationToken cancellation = default)
    {
        return initTaskSource.Task;
    }

    public Task InitializeRenderControl(Control renderControl, CancellationToken cancellation = default)
    {
        if (renderControl is not AvaloniaSkiaRenderControl)
            throw new ArgumentException("The render control must be an Avalonia Skia render control.", nameof(renderControl));

        initTaskSource.TrySetResult();
        return Task.CompletedTask;
    }

    public Task<IRenderContext> GetRenderContext(Control renderControl, CancellationToken cancellation = default)
    {
        if (renderControl is not AvaloniaSkiaRenderControl skiaRenderControl)
            throw new ArgumentException("The render control must be an Avalonia Skia render control.", nameof(renderControl));

        return Task.FromResult<IRenderContext>(skiaRenderControl.RenderContext);
    }

    public IImage LoadImageFromStream(Stream stream)
    {
        var image = SKImage.FromEncodedData(stream);
        return new Base.SkiaImage(image);
    }

    public Control CreateRenderControl()
    {
        return new AvaloniaSkiaRenderControl();
    }

    private sealed class UnsupportedSkiaSvgDrawing : CommonDrawingBase, ISvgDrawing
    {
        public void Draw(
            IDrawingContext target,
            OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg.SvgPrefabBase svg,
            System.Numerics.Vector2 position)
        {
            throw new NotSupportedException("Direct SVG drawing is not supported by the Avalonia.Skia renderer.");
        }
    }
}
