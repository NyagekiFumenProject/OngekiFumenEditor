using OpenTK.Wpf.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OpenTK.Wpf
{
	public class DComp
	{
		private Vortice.Direct3D11.ID3D11Device _device = null;
		private Vortice.Direct3D11.ID3D11DeviceContext _deviceContext = null;
		private Vortice.Direct3D11.ID3D11Texture2D _texture2D = null;
		private Vortice.Direct3D11.ID3D11Texture2D _texture2DDepth = null;
		private Vortice.DXGI.IDXGISurface1 _t2dSurface = null;
		private Vortice.Direct2D1.ID2D1Bitmap _t2dBitmap = null;

		private Vortice.Direct3D12.ID3D12Device _device12 = null;
		private Vortice.Direct3D12.ID3D12CommandQueue _commandQueue12 = null;

		private Vortice.DXGI.IDXGIFactory2 _DXGIFactory2 = null;
		private Vortice.DXGI.IDXGIDevice1 _DXGIDevice1 = null;
		private Vortice.DXGI.IDXGIOutput _DXGIOutput = null;
		private Vortice.DXGI.IDXGISwapChain1 _DXGISwapChain1 = null;
		private Vortice.DXGI.IDXGISurface1 _DXGISwapChainSurface = null;

		private Vortice.DirectComposition.IDCompositionDevice _DcompDevice = null;
		private Vortice.DirectComposition.IDCompositionTarget _DcompTarget = null;
		private Vortice.DirectComposition.IDCompositionVisual _DcompVisual = null;
		//private Vortice.DirectComposition.IDCompositionSurface _DcompSurface = null;

		private Vortice.Direct2D1.ID2D1Factory1 _factory2D = null;
		private Vortice.Direct2D1.ID2D1Device _device2D = null;
		private Vortice.Direct2D1.ID2D1DeviceContext _deviceContext2D = null;
		private Vortice.Direct2D1.ID2D1RenderTarget _D2D1RenderTarget = null;

		private DpiScale currentDpi;
		private IntPtr hwndHost;
		private int hostWidth;
		private int hostHeight;

		public DComp(double ControlWidth, double ControlHeight)
		{
			double dpi = DXInterop.GetDpiForSystem() / 96d;
			currentDpi = new(dpi, dpi);
			hostWidth = (int)(ControlWidth * currentDpi.DpiScaleX);
			hostHeight = (int)(ControlHeight * currentDpi.DpiScaleY);
		}

		public DComp(int PixelWidth, int PixelHeight)
		{
			double dpi = DXInterop.GetDpiForSystem() / 96d;
			currentDpi = new(dpi, dpi);
			hostWidth = PixelWidth;
			hostHeight = PixelHeight;
		}

		public void Resize(double ControlWidth, double ControlHeight)
		{
			double dpi = DXInterop.GetDpiForSystem() / 96d;
			currentDpi = new(dpi, dpi);
			hostWidth = (int)(ControlWidth * currentDpi.DpiScaleX);
			hostHeight = (int)(ControlHeight * currentDpi.DpiScaleY);
		}

		public void Resize(int PixelWidth, int PixelHeight)
		{
			double dpi = DXInterop.GetDpiForSystem() / 96d;
			currentDpi = new(dpi, dpi);
			hostWidth = PixelWidth;
			hostHeight = PixelHeight;
		}

		public void SetWindow(IntPtr hWnd)
		{
			hwndHost = hWnd;
		}

		public Vortice.Direct3D11.ID3D11Device GetD3D11Device()
		{
			return _device;
		}

		public Vortice.Direct3D11.ID3D11Texture2D GetD3D11Texture2D()
		{
			return _texture2D;
		}

		public Vortice.Direct3D11.ID3D11Texture2D GetD3D11Texture2DDepth()
		{
			return _texture2DDepth;
		}

		public void InitDirect3D(bool UseDirect3D12)
		{
			if (UseDirect3D12)
			{
				var hr = Vortice.Direct3D12.D3D12.D3D12CreateDevice(null, out _device12);
				_commandQueue12 = _device12.CreateCommandQueue(Vortice.Direct3D12.CommandListType.Direct);
				var hr1 = Vortice.Direct3D11on12.Apis.D3D11On12CreateDevice(_device12, Vortice.Direct3D11.DeviceCreationFlags.BgraSupport, [Vortice.Direct3D.FeatureLevel.Level_11_1], [_commandQueue12], 0, out _device, out _deviceContext, out _);
			}
			else
			{
				var hr = Vortice.Direct3D11.D3D11.D3D11CreateDevice(null, Vortice.Direct3D.DriverType.Hardware, Vortice.Direct3D11.DeviceCreationFlags.BgraSupport, [Vortice.Direct3D.FeatureLevel.Level_11_1], out _device);
				_deviceContext = _device.CreateDeferredContext();
			}
			_DXGIDevice1 = _device.QueryInterface<Vortice.DXGI.IDXGIDevice1>();
			_DXGIDevice1.SetMaximumFrameLatency(1);
			var adapter = _DXGIDevice1.GetAdapter();
			adapter.EnumOutputs(0, out _DXGIOutput);
			adapter.Dispose();
			_DXGIFactory2 = Vortice.DXGI.DXGI.CreateDXGIFactory2<Vortice.DXGI.IDXGIFactory2>(true);
			//_DXGIFactory2 = _DXGIDevice1.GetParent<Vortice.DXGI.IDXGIFactory2>();
			_factory2D = Vortice.Direct2D1.D2D1.D2D1CreateFactory<Vortice.Direct2D1.ID2D1Factory1>(Vortice.Direct2D1.FactoryType.MultiThreaded, Vortice.Direct2D1.DebugLevel.None);
			_device2D = _factory2D.CreateDevice(_DXGIDevice1);
			_deviceContext2D = _device2D.CreateDeviceContext(Vortice.Direct2D1.DeviceContextOptions.EnableMultithreadedOptimizations);
			var hr2 = Vortice.DirectComposition.DComp.DCompositionCreateDevice2(_device2D, out _DcompDevice);
			var hr4 = _DcompDevice.CreateVisual(out _DcompVisual);
			_DXGISwapChain1 = _DXGIFactory2.CreateSwapChainForComposition(_device, new(1, 1, Vortice.DXGI.Format.R8G8B8A8_UNorm, swapEffect: Vortice.DXGI.SwapEffect.FlipDiscard, alphaMode: Vortice.DXGI.AlphaMode.Premultiplied, flags: Vortice.DXGI.SwapChainFlags.AllowTearing));
			_DXGISwapChain1.BackgroundColor = new(16f / 256f, 16f / 256f, 16f / 256f, 1f);
			_DcompVisual.SetContent(_DXGISwapChain1);
			_DcompVisual.SetOffsetX(0);
			_DcompVisual.SetOffsetY(0);
		}

		public void CreateRenderResources()
		{
			_texture2D = _device.CreateTexture2D(Vortice.DXGI.Format.R8G8B8A8_UNorm, (uint)hostWidth, (uint)hostHeight, bindFlags: Vortice.Direct3D11.BindFlags.RenderTarget | Vortice.Direct3D11.BindFlags.ShaderResource);
			_texture2DDepth = _device.CreateTexture2D(Vortice.DXGI.Format.D24_UNorm_S8_UInt, (uint)hostWidth, (uint)hostHeight, bindFlags: Vortice.Direct3D11.BindFlags.DepthStencil);
			_t2dSurface = _texture2D.QueryInterface<Vortice.DXGI.IDXGISurface1>();
			_device.ImmediateContext.ClearState();
			_device.ImmediateContext.Flush();
			var hr = _DXGISwapChain1.ResizeBuffers(0, (uint)hostWidth, (uint)hostHeight,Vortice.DXGI.Format.R8G8B8A8_UNorm,swapChainFlags: Vortice.DXGI.SwapChainFlags.AllowTearing);
			
			_DXGISwapChainSurface = _DXGISwapChain1.GetBuffer<Vortice.DXGI.IDXGISurface1>(0);
			_D2D1RenderTarget = _factory2D.CreateDxgiSurfaceRenderTarget(_DXGISwapChainSurface, new() { DpiX = (float)(currentDpi.DpiScaleX * 96d), DpiY = (float)(currentDpi.DpiScaleY * 96d), PixelFormat = new(Vortice.DXGI.Format.R8G8B8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), Type = Vortice.Direct2D1.RenderTargetType.Hardware, Usage = Vortice.Direct2D1.RenderTargetUsage.None, MinLevel = Vortice.Direct2D1.FeatureLevel.Level_10 });
			var hr3 = _DcompDevice.CreateTargetForHwnd(hwndHost, true, out _DcompTarget);

			_t2dBitmap = _D2D1RenderTarget.CreateSharedBitmap(_t2dSurface, new(new(Vortice.DXGI.Format.R8G8B8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), (float)(currentDpi.DpiScaleX * 96d), (float)(currentDpi.DpiScaleY * 96d)));
			_DcompTarget.SetRoot(_DcompVisual);
			_DcompDevice.Commit();

		}

		public void DestoryRenderResources()
		{
			_DcompTarget?.Dispose();
			_DcompTarget = null;
			_D2D1RenderTarget?.Dispose();
			_D2D1RenderTarget = null;
			_DXGISwapChainSurface?.Dispose();
			_DXGISwapChainSurface = null;
			_t2dBitmap?.Dispose();
			_t2dBitmap = null;
			_t2dSurface?.Dispose();
			_t2dSurface = null;
			_texture2D?.Dispose();
			_texture2D = null;
			_texture2DDepth?.Dispose();
			_texture2DDepth = null;
		}

		public void DestoryDirect3D()
		{
			_DcompVisual?.Dispose();
			_DcompVisual = null;
			_DcompTarget?.Dispose();
			_DcompTarget = null;
			_DcompDevice?.Dispose();
			_DcompDevice = null;
			_DXGIFactory2?.Dispose();
			_DXGIFactory2 = null;
			_DXGISwapChain1?.Dispose();
			_DXGISwapChain1 = null;
			_deviceContext2D?.Dispose();
			_device2D?.Dispose();
			_factory2D?.Dispose();
			_DXGIDevice1?.Dispose();
			_deviceContext?.Dispose();
			_device?.Dispose();
			_commandQueue12?.Dispose();
			_device12?.Dispose();
		}

		public void Draw()
		{
			_D2D1RenderTarget.BeginDraw();
			_D2D1RenderTarget.Transform = System.Numerics.Matrix3x2.CreateScale(1, -1, new(0f, (float)(hostHeight / 2f / currentDpi.DpiScaleY)));
			//rt.SetDpi((float)(currentDpi.DpiScaleX * 96d), (float)(currentDpi.DpiScaleY * 96d));
			_D2D1RenderTarget.DrawBitmap(_t2dBitmap);
			_D2D1RenderTarget.Transform = System.Numerics.Matrix3x2.Identity;
			//var brush = rt.CreateSolidColorBrush(new Vortice.Mathematics.Color(255, 255, 255, 255));
			var queue = DWriteCore.GetCommands(this);
			float height = (float)(hostHeight / currentDpi.DpiScaleY);
			foreach (var item in queue)
			{
				item.Invoke(_D2D1RenderTarget, height);
			}
			queue.Clear();
			_D2D1RenderTarget.EndDraw();
			_DXGISwapChain1.Present(1);
			//rt.Dispose();
			//_t2dBitmap.Dispose();

			//brush.Dispose();
			//_DcompDevice.Commit();
		}

		public void WaitForVBlank()
		{
			//_DcompDevice.WaitForCommitCompletion();
			//_DXGIOutput.WaitForVBlank();
		}
	}
}
