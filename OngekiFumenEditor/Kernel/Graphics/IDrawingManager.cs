using OpenTK.Wpf;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IDrawingManager
    {
        /// <summary>
        /// 等待渲染环境初始化完成
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        Task WaitForGraphicsInitializationDone(CancellationToken cancellation = default);

        /// <summary>
        /// 等待渲染环境初始化完成
        /// </summary>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        Task CreateGraphicsContext(GLWpfControl glView, CancellationToken cancellation = default);

        /// <summary>
        /// 检查并试图初始化渲染环境
        /// </summary>
        /// <returns></returns>
        Task CheckOrInitGraphics();

        IImage LoadImageFromStream(Stream stream);

        void BeforeRender(IDrawingContext context);
        void AfterRender(IDrawingContext context);

        void CleanRender(Vector4 cleanColor);

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
