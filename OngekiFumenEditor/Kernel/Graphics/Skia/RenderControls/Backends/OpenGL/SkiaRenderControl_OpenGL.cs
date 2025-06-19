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
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String;
using NWaves.Utils;
using AngleSharp.Media;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU
{
    internal class SkiaRenderControl_OpenGL : SkiaRenderControlBase
    {
        private static GRContext grContext;
        private static GlContext oglContext;

        private const double BitmapDpi = 96.0;

        protected WriteableBitmap bitmap;
        private GRBackendTexture backendTexture;
        private GRGlTextureInfo? texture;

        private SKColorType colorType = SKColorType.Rgba8888;

        public SkiaRenderControl_OpenGL()
        {
            if (grContext is null)
            {
                oglContext = GlContext.Create();
                oglContext.MakeCurrent();
                grContext = GRContext.CreateGl();
            }
        }

        private void RecrateBitmap(SKImageInfo info, float scaleX, float scaleY)
        {
            if (texture is GRGlTextureInfo oldTextureInfo)
            {
                oglContext.DestroyTexture(oldTextureInfo.Id);
                Log.LogDebug($"deleted old backend texture: id:{oldTextureInfo.Id}");
            }
            backendTexture?.Dispose();
            var newTextureInfo = oglContext.CreateTexture(info.Size);
            backendTexture = new GRBackendTexture(info.Size.Width, info.Size.Height, false, newTextureInfo);
            texture = newTextureInfo;
            bitmap = new WriteableBitmap(info.Width, info.Height, BitmapDpi * scaleX, BitmapDpi * scaleY, PixelFormats.Pbgra32, null);
            Log.LogDebug($"created new backend texture: id:{newTextureInfo.Id}, size:{info.Size}, target:{newTextureInfo.Target}, format:{newTextureInfo.Format}, isProtected:{newTextureInfo.Protected}");
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

            if (bitmap == null || info.Width != bitmap.PixelWidth || info.Height != bitmap.PixelHeight)
                RecrateBitmap(info, scaleX, scaleY);

            bitmap.Lock();

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

            using var presentSurface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);
            presentSurface.Canvas.DrawSurface(renderSurface, 0, 0);

            // draw the bitmap to the screen
            bitmap.AddDirtyRect(new Int32Rect(0, 0, info.Width, size.Height));
            bitmap.Unlock();

            drawingContext.DrawImage(bitmap, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }
}
