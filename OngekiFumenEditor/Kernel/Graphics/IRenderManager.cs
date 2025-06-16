using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IRenderManager
    {
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

        Task<IRenderContext> GetRenderContext(FrameworkElement renderControl, CancellationToken cancellation = default);

        IImage LoadImageFromStream(Stream stream);

        #region Drawings

        ICircleDrawing CircleDrawing { get; }
        ILineDrawing LineDrawing { get; }
        ISimpleLineDrawing SimpleLineDrawing { get; }
        IStaticVBODrawing StaticVBODrawing { get; }
        IStringDrawing StringDrawing { get; }
        ISvgDrawing SvgDrawing { get; }
        ITextureDrawing TextureDrawing { get; }
        IBatchTextureDrawing BatchTextureDrawing { get; }
        IHighlightBatchTextureDrawing HighlightBatchTextureDrawing { get; }
        IPolygonDrawing PolygonDrawing { get; }

        IBeamDrawing BeamDrawing { get; }

        #endregion
    }
}
