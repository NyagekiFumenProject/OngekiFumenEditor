using Avalonia.Controls;

using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
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

    /// <summary>
    /// Creates a new builder for collecting backend-independent draw commands.
    /// </summary>
    IDrawCommandListBuilder CreateDrawCommandListBuilder();

    /// <summary>
    /// Posts a command list to the back slot associated with the specified render context.
    /// </summary>
    void PostDrawCommandList(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true);

    /// <summary>
    /// Promotes the back slot to the front slot for the specified render context.
    /// </summary>
    bool SwapDrawCommandList(IRenderContext context);

    /// <summary>
    /// Presents the front slot associated with the specified render context.
    /// </summary>
    void PresentDrawCommandList(IRenderContext context);
}
