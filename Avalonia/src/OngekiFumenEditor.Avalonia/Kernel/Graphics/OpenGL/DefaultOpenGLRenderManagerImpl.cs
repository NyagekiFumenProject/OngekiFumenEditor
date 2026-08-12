using Avalonia.Controls;
using Injectio.Attributes;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.OpenGL;

[RegisterSingleton<IRenderManagerImpl>]
public class DefaultOpenGLRenderManagerImpl : IRenderManagerImpl
{
    private readonly TaskCompletionSource initTaskSource = new();
    private readonly Dictionary<Control, IRenderContext> cachedRenderControlMap = [];

    public string Name { get; } = "OpenGL";

    public ICircleDrawing CircleDrawing { get; } = new NoopCircleDrawing();
    public ILineDrawing LineDrawing { get; } = new NoopLineDrawing();
    public ISimpleLineDrawing SimpleLineDrawing { get; } = new NoopSimpleLineDrawing();
    public IStaticVBODrawing StaticVBODrawing { get; } = new NoopStaticVBODrawing();
    public IStringDrawing StringDrawing { get; } = new NoopStringDrawing();
    public ISvgDrawing SvgDrawing { get; } = new NoopSvgDrawing();
    public ITextureDrawing TextureDrawing { get; } = new NoopTextureDrawing();
    public IBatchTextureDrawing BatchTextureDrawing { get; } = new NoopBatchTextureDrawing();
    public IHighlightBatchTextureDrawing HighlightBatchTextureDrawing { get; } = new NoopHighlightBatchTextureDrawing();
    public IPolygonDrawing PolygonDrawing { get; } = new NoopPolygonDrawing();
    public IBeamDrawing BeamDrawing { get; } = new NoopBeamDrawing();

    public Task WaitForInitializationIsDone(CancellationToken cancellation = default)
    {
        return initTaskSource.Task;
    }

    public Task InitializeRenderControl(Control renderControl, CancellationToken cancellation = default)
    {
        initTaskSource.TrySetResult();
        return Task.CompletedTask;
    }

    public Task<IRenderContext> GetRenderContext(Control renderControl, CancellationToken cancellation = default)
    {
        if (!cachedRenderControlMap.TryGetValue(renderControl, out var renderContext))
            renderContext = cachedRenderControlMap[renderControl] = new DefaultOpenGLRenderContext();
        return Task.FromResult(renderContext);
    }

    public IImage LoadImageFromStream(Stream stream)
    {
        var image = SKImage.FromEncodedData(stream);
        return new Skia.Base.SkiaImage(image);
    }

    public Control CreateRenderControl()
    {
        return new Panel();
    }

    public void ReleaseRenderControl(Control renderControl)
    {
        if (!cachedRenderControlMap.Remove(renderControl, out var renderContext))
            return;

        renderContext.StopRendering();
        if (renderContext is IDisposable disposable)
            disposable.Dispose();
    }
}
