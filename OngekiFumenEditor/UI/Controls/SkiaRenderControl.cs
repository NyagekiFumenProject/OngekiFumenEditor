using SkiaSharp.Views.Desktop;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using OngekiFumenEditor.Kernel.Graphics.Skia;
using SkiaSharp.Tests;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.UI.Controls
{
    [DefaultEvent("PaintSurface")]
    [DefaultProperty("Name")]
    public class SkiaRenderControl : FrameworkElement, INotifyPropertyChanged
    {
        private const double BitmapDpi = 96.0;

        private readonly bool designMode;
        private WriteableBitmap bitmap;
        private bool ignorePixelScaling;

        public SkiaRenderControl(RenderBackendType renderBackendType)
        {
            designMode = DesignerProperties.GetIsInDesignMode(this);

            IsVisibleChanged += delegate (object _, DependencyPropertyChangedEventArgs args)
            {
                if ((bool)args.NewValue)
                {
                    CompositionTarget.Rendering += OnCompTargetRender;
                }
                else
                {
                    CompositionTarget.Rendering -= OnCompTargetRender;
                }
            };

            this.renderBackendType = renderBackendType;
            switch (renderBackendType)
            {
                case RenderBackendType.OpenGL:
                    {
                        if (grContext is null)
                        {
                            oglContext = GlContext.Create();
                            oglContext.MakeCurrent();
                            grContext = GRContext.CreateGl();
                        }

                        var colorType = SKColorType.Rgba8888;

                        onSizeChanged = (info) =>
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
                            Log.LogDebug($"created new backend texture: id:{newTextureInfo.Id}, size:{info.Size}, target:{newTextureInfo.Target}, format:{newTextureInfo.Format}, isProtected:{newTextureInfo.Protected}");
                        };
                        surfaceCreateFunc = (info) =>
                        {
                            return SKSurface.Create(grContext, backendTexture, colorType);
                        };
                    }
                    break;
                case RenderBackendType.CPU:
                    {
                        onSizeChanged = default;
                        surfaceCreateFunc = (info) =>
                        {
                            return SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);
                        };
                        break;
                    }
                default:
                    break;
            }
        }

        private void OnCompTargetRender(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        public SKSize CanvasSize { get; private set; }

        public bool IgnorePixelScaling
        {
            get => ignorePixelScaling;
            set
            {
                ignorePixelScaling = value;
                InvalidateVisual();
            }
        }

        private SKSurface currentRenderSurface;
        private Func<SKImageInfo, SKSurface> surfaceCreateFunc;
        private static GRContext grContext;
        private static GlContext oglContext;
        private Action<SKImageInfo> onSizeChanged;
        private RenderBackendType renderBackendType;
        private GRBackendTexture backendTexture;
        private GRGlTextureInfo? texture;
        private SKSurface bitmapSurface;

        public SKSurface CurrentRenderSurface
        {
            get => currentRenderSurface;
            set
            {
                currentRenderSurface = value;
                PropertyChanged?.Invoke(this, new(nameof(CurrentRenderSurface)));
            }
        }

        [Category("Appearance")]
        public event EventHandler<SKPaintSurfaceEventArgs> PaintSurface;
        public event PropertyChangedEventHandler PropertyChanged;

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
            {
                bitmap = new WriteableBitmap(info.Width, info.Height, BitmapDpi * scaleX, BitmapDpi * scaleY, PixelFormats.Pbgra32, null);
                onSizeChanged?.Invoke(info);
                bitmapSurface?.Dispose();
                bitmapSurface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);
            }

            // draw on the bitmap
            bitmap.Lock();

            using var surface = surfaceCreateFunc(info);

            if (IgnorePixelScaling)
            {
                var canvas = surface.Canvas;
                canvas.Scale(scaleX, scaleY);
                canvas.Save();
            }

            CurrentRenderSurface = surface;
            OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info.WithSize(userVisibleSize), info));
            CurrentRenderSurface = default;

            if (renderBackendType != RenderBackendType.CPU)
                bitmapSurface.Canvas.DrawSurface(surface, 0, 0);

            // draw the bitmap to the screen
            bitmap.AddDirtyRect(new Int32Rect(0, 0, info.Width, size.Height));
            bitmap.Unlock();

            drawingContext.DrawImage(bitmap, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        protected virtual void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            // invoke the event
            PaintSurface?.Invoke(this, e);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            InvalidateVisual();
        }

        private SKSizeI CreateSize(out SKSizeI unscaledSize, out float scaleX, out float scaleY)
        {
            unscaledSize = SKSizeI.Empty;
            scaleX = 1.0f;
            scaleY = 1.0f;

            var w = ActualWidth;
            var h = ActualHeight;

            if (!IsPositive(w) || !IsPositive(h))
                return SKSizeI.Empty;

            unscaledSize = new SKSizeI((int)w, (int)h);

            var m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            scaleX = (float)m.M11;
            scaleY = (float)m.M22;
            return new SKSizeI((int)(w * scaleX), (int)(h * scaleY));

            bool IsPositive(double value)
            {
                return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
            }
        }
    }
}
