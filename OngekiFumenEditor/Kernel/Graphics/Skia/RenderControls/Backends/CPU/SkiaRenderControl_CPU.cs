using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU
{
    internal class SkiaRenderControl_CPU : SkiaRenderControlBase
    {
        private const double BitmapDpi = 96.0;

        protected WriteableBitmap bitmap;

        public SkiaRenderControl_CPU()
        {

        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (designMode)
                return;

            if (Visibility != Visibility.Visible || PresentationSource.FromVisual(this) == null)
                return;

            var pixelSize = CreateSize(out var logicalSize, out var scaleX, out var scaleY);
            if (IgnorePixelScaling)
                pixelSize = logicalSize;

            CanvasSize = logicalSize;

            if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
                return;

            var info = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            // reset the bitmap if the size has changed
            if (bitmap == null || info.Width != bitmap.PixelWidth || info.Height != bitmap.PixelHeight)
                bitmap = new WriteableBitmap(info.Width, info.Height, BitmapDpi * scaleX, BitmapDpi * scaleY, PixelFormats.Pbgra32, null);

            // draw on the bitmap
            bitmap.Lock();

            using var surface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);

            if (!IgnorePixelScaling)
            {
                var canvas = surface.Canvas;
                canvas.Save();
                canvas.Scale(scaleX, scaleY);
            }

            CurrentRenderSurface = surface;
            OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info.WithSize(logicalSize), info));
            CurrentRenderSurface = default;

            // draw the bitmap to the screen
            bitmap.AddDirtyRect(new Int32Rect(0, 0, info.Width, info.Height));
            bitmap.Unlock();

            drawingContext.DrawImage(bitmap, new Rect(0, 0, logicalSize.Width, logicalSize.Height));
        }
    }
}
