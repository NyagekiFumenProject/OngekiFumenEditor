using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// Provides backend-specific render management and draw-command list integration.
    /// </summary>
    public interface IRenderManagerImpl
    {
        string Name { get; }

        /// <summary>
        /// 等待渲染环境初始化完成
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        Task WaitForInitializationIsDone(CancellationToken cancellation = default);

        /// <summary>
        /// 初始化渲染控件和环境
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        Task InitializeRenderControl(FrameworkElement renderControl, CancellationToken cancellation = default);

        /// <summary>
        /// Gets or creates the render context associated with the specified render control.
        /// </summary>
        Task<IRenderContext> GetOrCreateRenderContext(FrameworkElement renderControl, CancellationToken cancellation = default);

        /// <summary>
        /// Gets the render contexts currently cached by this render manager.
        /// </summary>
        IReadOnlyList<IRenderContext> GetRenderContexts();

        /// <summary>
        /// Loads an image resource from the provided stream.
        /// </summary>
        IImage LoadImageFromStream(Stream stream);

        /// <summary>
        /// Creates a render control instance for this backend.
        /// </summary>
        FrameworkElement CreateRenderControl();

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
}
