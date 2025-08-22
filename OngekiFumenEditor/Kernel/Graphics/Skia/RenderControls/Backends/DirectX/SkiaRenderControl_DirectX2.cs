using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Kernel.Graphics.Skia.D3dContexts;
using System.Windows.Interop;
using Vortice.Direct3D12;
using Vortice.Direct3D11;
using Microsoft.Wpf.Interop.DirectX;
using System;
using Vortice.DXGI;
using NWaves.Utils;
using System.Drawing;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU
{
    internal class SkiaRenderControl_DirectX2 : SkiaRenderControlBase
    {
        private static VorticeDirect3DContext d3dContext;
        private static GRD3DBackendContext d3dBackendContext;
        private static GRContext grContext;

        private const double BitmapDpi = 96.0;

        protected D3D11Image d3dImage;
        private GRBackendTexture backendTexture;
        private GRBackendRenderTarget backendRenderTarget;
        private SKImageInfo prevInfo;

        private ID3D11Texture2D d11Texture2D;
        private ID3D11Resource d11Texture2DResource;
        private ID3D12Resource d12Texture2DResource;
        private SKImageInfo info;

        public SkiaRenderControl_DirectX2()
        {
            if (grContext is null)
            {
                d3dContext = new VorticeDirect3DContext();
                d3dBackendContext = d3dContext.CreateBackendContext();
                grContext = GRContext.CreateDirect3D(d3dBackendContext);
            }

            d3dImage = new D3D11Image();
            d3dImage.WindowOwner = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            d3dImage.OnRender = OnD3D11ImageRender;
        }

        private void OnD3D11ImageRender(nint dxgiSurfacePtr, bool isNewSurfaceCreated)
        {
            if (isNewSurfaceCreated)
            {
                //todo清除老的资源
                backendRenderTarget?.Dispose();
                backendTexture?.Dispose();
                d12Texture2DResource?.Release();
                d11Texture2DResource?.Release();
                d11Texture2D?.Release();

                //新建DX11 Surface
                var r = new IDXGISurface(dxgiSurfacePtr);
                var d3d11Texture2d = r.QueryInterface<ID3D11Texture2D>();

                d11Texture2D = d3dContext.Device11.CreateTexture2D(d3d11Texture2d.Description);
                d11Texture2DResource = d11Texture2D.QueryInterface<ID3D11Resource>();
                d12Texture2DResource = d3dContext.Device11On12.UnwrapUnderlyingResource<ID3D12Resource>(d11Texture2DResource, d3dContext.Queue);

                backendTexture = new((int)d3d11Texture2d.Description.Width, (int)d3d11Texture2d.Description.Height,
                    new GRVorticeD3DTextureResourceInfo()
                    {
                        Format = d12Texture2DResource.Description.Format,
                        Resource = d12Texture2DResource,
                    });

                backendRenderTarget = new GRBackendRenderTarget((int)d3d11Texture2d.Description.Width, (int)d3d11Texture2d.Description.Height,
                    new GRVorticeD3DTextureResourceInfo()
                    {
                        Format = d12Texture2DResource.Description.Format,
                        Resource = d12Texture2DResource,
                    });

                d3d11Texture2d.Release();
            }
            var size = CreateSize(out var unscaledSize, out var scaleX, out var scaleY);
            var userVisibleSize = IgnorePixelScaling ? unscaledSize : size;

            CanvasSize = userVisibleSize;

            if (size.Width <= 0 || size.Height <= 0)
                return;

            var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

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

            var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            if (info.Width != d3dImage.PixelWidth || info.Height != d3dImage.PixelHeight)
                d3dImage.SetPixelSize(info.Width, info.Height);

            d3dImage.RequestRender();
            drawingContext.DrawImage(d3dImage, new(0, 0, d3dImage.PixelWidth, d3dImage.PixelHeight));
        }

    }
}
