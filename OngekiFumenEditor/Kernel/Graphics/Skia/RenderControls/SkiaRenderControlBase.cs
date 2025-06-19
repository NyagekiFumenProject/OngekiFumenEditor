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

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls
{
    [DefaultEvent("PaintSurface")]
    [DefaultProperty("Name")]
    public abstract class SkiaRenderControlBase : FrameworkElement, INotifyPropertyChanged
    {
        private bool ignorePixelScaling;

        public SkiaRenderControlBase()
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
        }

        private void OnCompTargetRender(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        public SKSize CanvasSize { get; protected set; }

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
        protected bool designMode;

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

        protected SKSizeI CreateSize(out SKSizeI unscaledSize, out float scaleX, out float scaleY)
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
