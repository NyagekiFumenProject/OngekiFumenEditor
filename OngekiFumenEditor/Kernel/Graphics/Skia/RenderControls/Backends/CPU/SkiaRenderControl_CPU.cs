using SkiaSharp;
using SkiaSharp.Tests;
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

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU
{
    internal class SkiaRenderControl_CPU : SkiaRenderControlBase
    {
        private const double BitmapDpi = 96.0;

        private readonly bool designMode;
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

            using var surface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);

            if (IgnorePixelScaling)
            {
                var canvas = surface.Canvas;
                canvas.Scale(scaleX, scaleY);
                canvas.Save();
            }

            CurrentRenderSurface = surface;
            OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info.WithSize(userVisibleSize), info));
            CurrentRenderSurface = default;

            // draw the bitmap to the screen
            bitmap.AddDirtyRect(new Int32Rect(0, 0, info.Width, size.Height));
            bitmap.Unlock();

            drawingContext.DrawImage(bitmap, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }
}
