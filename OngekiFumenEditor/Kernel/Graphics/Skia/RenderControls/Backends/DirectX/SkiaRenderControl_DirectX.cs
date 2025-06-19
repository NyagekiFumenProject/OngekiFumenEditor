using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Kernel.Graphics.Skia.D3dContexts;
using System.Windows.Interop;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU
{
    internal class SkiaRenderControl_DirectX : SkiaRenderControlBase
    {
        private const double BitmapDpi = 96.0;

        protected D3DImage d3dImage;
        private GRBackendTexture backendTexture;

        private SKColorType colorType = SKColorType.Rgba8888;
        private VorticeDirect3DContext d3dContext;
        private GRD3DBackendContext d3dBackendContext;
        private static GRContext grContext;
        private SKImageInfo prevInfo;

        public SkiaRenderControl_DirectX()
        {
            if (grContext is null)
            {
                //todo step1: 新建DX设备和初始化skia backend
                d3dContext = new VorticeDirect3DContext();
                d3dBackendContext = d3dContext.CreateBackendContext();
                grContext = GRContext.CreateDirect3D(d3dBackendContext);
            }
        }

        private void RecreateResources(SKImageInfo info, float scaleX, float scaleY)
        {
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (designMode)
                return;

            if (Visibility != Visibility.Visible || PresentationSource.FromVisual(this) == null)
                return;

            var size = CreateSize(out var unscaledSize, out var scaleX, out var scaleY);
            var userVisibleSize = IgnorePixelScaling ? unscaledSize : size;

            CanvasSize = userVisibleSize;

            if (size.Width <= 0 || size.Height <= 0)
                return;

            var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            if (d3dImage == null || info.Width != prevInfo.Width || info.Height != prevInfo.Height)
                RecreateResources(info, scaleX, scaleY);
            prevInfo = info;

            using var renderSurface = SKSurface.Create(grContext, backendTexture, colorType);

            if (IgnorePixelScaling)
            {
                var canvas = renderSurface.Canvas;
                canvas.Scale(scaleX, scaleY);
                canvas.Save();
            }

            CurrentRenderSurface = renderSurface;
            OnPaintSurface(new SKPaintSurfaceEventArgs(renderSurface, info.WithSize(userVisibleSize), info));
            CurrentRenderSurface = default;

            // 绘制d3dimage到WPF界面上
            drawingContext.DrawImage(d3dImage, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }
}
