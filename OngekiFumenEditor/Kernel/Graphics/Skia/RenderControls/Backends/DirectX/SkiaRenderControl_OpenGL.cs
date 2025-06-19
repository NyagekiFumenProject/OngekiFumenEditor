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

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU
{
	internal class SkiaRenderControl_DirectX : SkiaRenderControlBase
	{
		private const double BitmapDpi = 96.0;

		protected D3DImage d3dImage;
		private GRBackendTexture backendTexture;
		private ID3D11Texture2D d11Texture2D;
		private ID3D11Resource d11Texture2DResource;
		private ID3D12Resource d12Texture2DResource;
		private SKColorType colorType = SKColorType.Rgba8888;
		private VorticeDirect3DContext d3dContext;
		private GRD3DBackendContext d3dBackendContext;
		private static GRContext grContext;
		private SKImageInfo prevInfo;

		public SkiaRenderControl_DirectX()
		{
			if (grContext is null)
			{
				d3dContext = new VorticeDirect3DContext();
				d3dBackendContext = d3dContext.CreateBackendContext();
				grContext = GRContext.CreateDirect3D(d3dBackendContext);
			}
		}

		private void RecreateResources(SKImageInfo info, float scaleX, float scaleY)
		{
			//todo step2: 删除旧的资源, 比如dx纹理
			if (d11Texture2D is not null)
			{
				d11Texture2D.Dispose();
				d11Texture2DResource.Dispose();
				d12Texture2DResource.Dispose();
			}
			//todo step3: 创建新的资源, 比如dx纹理, 以及d3dImage
			d11Texture2D = d3dContext.Device11.CreateTexture2D(new Vortice.Direct3D11.Texture2DDescription
			{
				Width = (uint)info.Width,
				Height = (uint)info.Height,
				ArraySize = 1,
				MipLevels = 1,
				Format = Vortice.DXGI.Format.R8G8B8A8_UNorm,
				SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
				CPUAccessFlags = CpuAccessFlags.None,
				MiscFlags = ResourceOptionFlags.None,
			});
			d11Texture2DResource = d11Texture2D.QueryInterface<ID3D11Resource>();
			d12Texture2DResource = d3dContext.Device11On12.UnwrapUnderlyingResource<ID3D12Resource>(d11Texture2DResource, d3dContext.Queue);
			backendTexture = new(info.Width, info.Height, new GRD3DTextureResourceInfo() { Format = (uint)Vortice.DXGI.Format.R8G8B8A8_UNorm, SampleCount = 1, SampleQualityPattern = 0, Resource = d12Texture2DResource.NativePointer, ResourceState = (uint)Vortice.Direct3D12.ResourceStates.Common, Protected = false, LevelCount = 0 });
			d3dImage = new D3DImage();
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
