using Avalonia.Controls;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics;

public interface IRenderManagerImpl
{
    string Name { get; }

    Task WaitForInitializationIsDone(CancellationToken cancellation = default);
    Task InitializeRenderControl(Control renderControl, CancellationToken cancellation = default);
    Task<IRenderContext> GetRenderContext(Control renderControl, CancellationToken cancellation = default);

    IImage LoadImageFromStream(Stream stream);
    Control CreateRenderControl();
    void ReleaseRenderControl(Control renderControl)
    {
    }

    ICircleDrawing CircleDrawing { get; }
    ILineDrawing LineDrawing { get; }
    ISimpleLineDrawing SimpleLineDrawing { get; }
    IStaticVBODrawing StaticVBODrawing { get; }
    IStringDrawing StringDrawing { get; }
    ITextureDrawing TextureDrawing { get; }
    IBatchTextureDrawing BatchTextureDrawing { get; }
    IHighlightBatchTextureDrawing HighlightBatchTextureDrawing { get; }
    IPolygonDrawing PolygonDrawing { get; }
    IBeamDrawing BeamDrawing { get; }
    ISvgDrawing SvgDrawing { get; }
}
