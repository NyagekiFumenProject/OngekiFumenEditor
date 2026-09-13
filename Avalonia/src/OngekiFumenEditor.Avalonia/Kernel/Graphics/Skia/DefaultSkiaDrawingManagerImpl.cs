using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.BeamDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.CircleDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.TextureDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.SvgDrawing;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

[RegisterSingleton<IRenderManagerImpl>]
public class DefaultSkiaDrawingManagerImpl : IRenderManagerImpl
{
    private readonly TaskCompletionSource initTaskSource = new();
    private readonly DrawCommandListContextSlots drawCommandListContextSlots = new();

    /// <summary>
    /// One replay per render context, kept for the lifetime of that context (PERF-RND-017 / RND-20).
    /// The replay owns the backend drawings together with their pools, dash effect cache and native
    /// paints; rebuilding it every frame used to throw all of that away, so it is reset per frame
    /// through <see cref="SkiaDrawCommandListReplay.BeginFrame"/> instead.
    /// </summary>
    private readonly Dictionary<IRenderContext, SkiaDrawCommandListReplay> replayCache = new();

    public string Name { get; } = "Skia";

    public DefaultSkiaDrawingManagerImpl()
    {
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

    public void ReleaseRenderControl(Control renderControl)
    {
        if (renderControl is AvaloniaSkiaRenderControl skiaRenderControl)
        {
            var renderContext = skiaRenderControl.RenderContext;
            renderContext.StopRendering();
            drawCommandListContextSlots.Remove(renderContext);

            // The replay outlives individual frames, so it must be released explicitly here;
            // otherwise its pools, dash effect cache and native paints would leak past the context.
            if (replayCache.Remove(renderContext, out var replay))
                replay.Dispose();
        }
    }

    public IDrawCommandListBuilder CreateDrawCommandListBuilder()
    {
        return new DrawCommandListBuilder(new DefaultSkiaStringDrawing(this));
    }

    public void PostDrawCommandList(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true)
    {
        drawCommandListContextSlots.Post(context, drawCommandList, autoDispose);
    }

    public Control CreateRenderControl()
    {
        var control = new AvaloniaSkiaRenderControl();
        control.RenderContext.AttachManager(this);
        return control;
    }

    public bool SwapDrawCommandList(IRenderContext context)
    {
        return drawCommandListContextSlots.Swap(context);
    }

    public void PresentDrawCommandList(IRenderContext context)
    {
        //the canvas is only valid during the lease of the current frame presentation
        if (context is not DefaultSkiaRenderContext { Canvas: { } canvas })
            return;

        drawCommandListContextSlots.Present(context, list =>
        {
            var replay = GetOrCreateReplay(context);
            replay.BeginFrame(canvas, list.FrameState);
            try
            {
                replay.Present(list.Commands);
            }
            finally
            {
                replay.EndFrame();
            }
        });
    }

    private SkiaDrawCommandListReplay GetOrCreateReplay(IRenderContext context)
    {
        if (!replayCache.TryGetValue(context, out var replay))
            replay = replayCache[context] = new SkiaDrawCommandListReplay(this, context);

        return replay;
    }
}
