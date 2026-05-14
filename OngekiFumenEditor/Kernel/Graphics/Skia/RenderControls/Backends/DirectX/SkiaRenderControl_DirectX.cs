using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using OngekiFumenEditor.Kernel.Graphics.Skia.D3dContexts;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.DirectX
{
    internal class SkiaRenderControl_DirectX : SkiaRenderControlBase
    {
        private static VorticeDirect3DContext d3dContext;
        private static GRD3DBackendContext d3dBackendContext;
        private static GRContext grContext;

        private const double BitmapDpi = 96.0;

        protected WriteableBitmap bitmap;

        public SkiaRenderControl_DirectX()
        {
            if (grContext is null)
            {
                d3dContext = new VorticeDirect3DContext();
                d3dBackendContext = d3dContext.CreateBackendContext();
                grContext = GRContext.CreateDirect3D(d3dBackendContext);
            }
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

            // reset the bitmap if the size has changed
            if (bitmap == null || info.Width != bitmap.PixelWidth || info.Height != bitmap.PixelHeight)
                bitmap = new WriteableBitmap(info.Width, info.Height, BitmapDpi * scaleX, BitmapDpi * scaleY, PixelFormats.Pbgra32, null);

            // draw on the bitmap
            bitmap.Lock();

            // 新建一个DX渲染的Surface
            using var renderSurface = SKSurface.Create(grContext, true, info);

            if (!IgnorePixelScaling)
            {
                var canvas = renderSurface.Canvas;
                canvas.Scale(scaleX, scaleY);
                canvas.Save();
            }

            CurrentRenderSurface = renderSurface;
            OnPaintSurface(new SKPaintSurfaceEventArgs(renderSurface, info.WithSize(userVisibleSize), info));
            CurrentRenderSurface = default;

            //渲染结果复制到bitmap上
            using var presentSurface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);
            presentSurface.Canvas.DrawSurface(renderSurface, 0, 0);

            // draw the bitmap to the screen
            bitmap.AddDirtyRect(new Int32Rect(0, 0, info.Width, size.Height));
            bitmap.Unlock();

            drawingContext.DrawImage(bitmap, new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));
        }
    }
}
