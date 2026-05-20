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
    internal sealed class SkiaRenderControl_DirectX : SkiaRenderControlBase
    {
        private static VorticeDirect3DContext d3dContext;
        private static GRD3DBackendContext d3dBackendContext;
        private static GRContext grContext;

        private const double BitmapDpi = 96.0;

        private WriteableBitmap bitmap;

        private SKSurface cachedRenderSurface;
        private SKSurface cachedPresentSurface;
        private SKImageInfo cachedInfo;
        private int cachedPixelWidth = -1;
        private int cachedPixelHeight = -1;
        private float cachedScaleX;
        private float cachedScaleY;

        public SkiaRenderControl_DirectX()
        {
            if (grContext is null)
            {
                d3dContext = new VorticeDirect3DContext();
                d3dBackendContext = d3dContext.CreateBackendContext();
                grContext = GRContext.CreateDirect3D(d3dBackendContext);
            }

            Unloaded += (_, _) => ReleaseCachedSurfaces();
        }

        private void ReleaseCachedSurfaces()
        {
            cachedPresentSurface?.Dispose();
            cachedPresentSurface = null;
            cachedRenderSurface?.Dispose();
            cachedRenderSurface = null;
            cachedPixelWidth = -1;
            cachedPixelHeight = -1;
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

            var sizeChanged = size.Width != cachedPixelWidth
                           || size.Height != cachedPixelHeight
                           || scaleX != cachedScaleX
                           || scaleY != cachedScaleY;

            if (sizeChanged)
            {
                cachedInfo = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                cachedPixelWidth = size.Width;
                cachedPixelHeight = size.Height;
                cachedScaleX = scaleX;
                cachedScaleY = scaleY;

                bitmap = new WriteableBitmap(cachedInfo.Width, cachedInfo.Height, BitmapDpi * scaleX, BitmapDpi * scaleY, PixelFormats.Pbgra32, null);

                cachedRenderSurface?.Dispose();
                cachedRenderSurface = SKSurface.Create(grContext, true, cachedInfo);

                //BackBuffer 指针随 bitmap 重建会变化，置 null 让下面在 Lock 后重建
                cachedPresentSurface?.Dispose();
                cachedPresentSurface = null;
            }

            bitmap.Lock();

            if (cachedPresentSurface is null)
                cachedPresentSurface = SKSurface.Create(cachedInfo, bitmap.BackBuffer, bitmap.BackBufferStride);

            var canvas = cachedRenderSurface.Canvas;
            canvas.Save();
            try
            {
                if (!IgnorePixelScaling)
                    canvas.Scale(scaleX, scaleY);

                CurrentRenderSurface = cachedRenderSurface;
                OnPaintSurface(new SKPaintSurfaceEventArgs(cachedRenderSurface, cachedInfo.WithSize(userVisibleSize), cachedInfo));
                CurrentRenderSurface = default;
            }
            finally
            {
                canvas.Restore();
            }

            //显式提交 GPU 命令；下面 DrawSurface 会负责必要的同步
            cachedRenderSurface.Flush(submit: true, synchronous: false);

            //渲染结果复制到bitmap上（隐式 GPU→CPU 同步）
            cachedPresentSurface.Canvas.DrawSurface(cachedRenderSurface, 0, 0);

            bitmap.AddDirtyRect(new Int32Rect(0, 0, cachedInfo.Width, cachedInfo.Height));
            bitmap.Unlock();

            drawingContext.DrawImage(bitmap, new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));
        }
    }
}
