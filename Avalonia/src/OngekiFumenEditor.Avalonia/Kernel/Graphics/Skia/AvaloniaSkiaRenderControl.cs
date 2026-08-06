using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;

/// <summary>
/// The single supported render surface for the editor.
/// Avalonia owns the native Skia surface; the editor only uses the leased canvas
/// while the custom draw operation is being rendered.
/// </summary>
internal sealed class AvaloniaSkiaRenderControl : Control
{
    private readonly SkiaDrawOperation drawOperation;

    public AvaloniaSkiaRenderControl()
    {
        RenderContext = new DefaultSkiaRenderContext(this);
        drawOperation = new SkiaDrawOperation(RenderContext);
        ClipToBounds = true;
        Focusable = true;
    }

    public DefaultSkiaRenderContext RenderContext { get; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        drawOperation.Bounds = new Rect(Bounds.Size);
        context.Custom(drawOperation);

        if (RenderContext.IsRendering)
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly DefaultSkiaRenderContext renderContext;

        public SkiaDrawOperation(DefaultSkiaRenderContext renderContext)
        {
            this.renderContext = renderContext;
        }

        public Rect Bounds { get; set; }

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
            renderContext.RenderFrame(context);
        }
    }
}
