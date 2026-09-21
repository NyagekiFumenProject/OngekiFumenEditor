using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

/// <summary>
/// The single supported render surface for the editor.
/// Avalonia owns the native Skia surface; the editor only uses the leased canvas
/// while the custom draw operation is being rendered.
/// </summary>
internal sealed class AvaloniaSkiaRenderControl : Control
{
    private SkiaDrawOperation drawOperation;

    public AvaloniaSkiaRenderControl()
    {
        RenderContext = new DefaultSkiaRenderContext(InvalidateVisual);
        ClipToBounds = true;
        Focusable = true;
    }

    public DefaultSkiaRenderContext RenderContext { get; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        // Custom operations run on the compositor thread. Publish immutable bounds and
        // build commands here, while editor callbacks can still access UI-owned state.
        var bounds = new Rect(Bounds.Size);
        if (drawOperation is null || drawOperation.Bounds != bounds)
            drawOperation = new SkiaDrawOperation(RenderContext, bounds);
        RenderContext.PrepareFrame();
        context.Custom(drawOperation);

        if (RenderContext.IsRendering)
            RenderContext.RequestFrame();
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly DefaultSkiaRenderContext renderContext;

        public SkiaDrawOperation(DefaultSkiaRenderContext renderContext, Rect bounds)
        {
            this.renderContext = renderContext;
            Bounds = bounds;
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation other)
        {
            return ReferenceEquals(this, other);
        }

        public bool HitTest(Point p)
        {
            return Bounds.Contains(p);
        }

        public void Render(ImmediateDrawingContext context)
        {
            renderContext.RenderFrame(context, Bounds.Size);
        }
    }
}
