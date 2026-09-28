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
        /// Removes the specified render context from the render manager cache.
        /// </summary>
        bool RemoveRenderContext(IRenderContext renderContext);

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

        /// <summary>
        /// 创建离屏渲染目标（固定尺寸/格式、不参与控件绘制周期）。返回的上下文由调用方负责 Dispose。
        /// OpenGL 后端要求存在处于渲染中的活动 GL 控件，否则抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        IOffscreenRenderContext CreateOffscreenToImage(OffscreenRenderOptions options);

        /// <summary>创建离屏渲染目标（平台默认像素格式 + sRGB + Premul 的便捷重载）。</summary>
        IOffscreenRenderContext CreateOffscreenToImage(int width, int height);

        /// <summary>
        /// 关闭本后端持有的后台资源（Skia 的离屏渲染通道线程、OpenGL 的排队请求与延迟删除队列）。
        /// <b>同步完成保证</b>：实现必须同步执行完全部清理；返回的 Task 完成时（同步实现即返回前），
        /// 所有未完成的 <see cref="IOffscreenRenderContext.RenderToImageAsync"/> 任务都必须已结束（结果/异常/取消皆可）。
        /// 由 AppBootstrapper.OnExit 在第一个 await 之前调用。
        /// </summary>
        Task Term();
    }
}
