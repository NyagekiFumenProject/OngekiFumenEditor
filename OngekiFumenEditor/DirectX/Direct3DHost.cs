using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics.Wgl;
using OpenTK.Platform.Windows;
using OpenTK.Wpf;
using SharpVectors.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DpiScale = System.Windows.DpiScale;

namespace OngekiFumenEditor.DirectX
{
	internal class Direct3DHost : HwndHost
	{
		private Vortice.Direct3D11.ID3D11Device _device = null;
		private Vortice.Direct3D11.ID3D11DeviceContext _deviceContext = null;
		private Vortice.Direct3D11.ID3D11Texture2D _texture2D = null;
		private Vortice.DXGI.IDXGISurface1 _t2dSurface = null;
		private Vortice.Direct2D1.ID2D1Bitmap1 _t2dBitmap1 = null;

		private Vortice.Direct3D12.ID3D12Device _device12 = null;
		private Vortice.Direct3D12.ID3D12CommandQueue _commandQueue12 = null;

		private Vortice.DXGI.IDXGIDevice _DXGIDevice = null;

		private Vortice.DirectComposition.IDCompositionDevice _DcompDevice = null;
		private Vortice.DirectComposition.IDCompositionTarget _DcompTarget = null;
		private Vortice.DirectComposition.IDCompositionVisual _DcompVisual = null;
		private Vortice.DirectComposition.IDCompositionSurface _DcompSurface = null;

		private Vortice.Direct2D1.ID2D1Factory1 _factory2D = null;
		private Vortice.Direct2D1.ID2D1Device _device2D = null;
		private Vortice.Direct2D1.ID2D1DeviceContext _deviceContext2D = null;

		private IntPtr _openGLDeviceHandle = IntPtr.Zero;
		private int _openGLFrameBufferHandle = 0;
		private int _openGLRenderBufferHandle = 0;
		private int _openGLTextureHandle = 0;
		private IntPtr _openGLID3D11Texture2DHandle = IntPtr.Zero;

		private DpiScale currentDpi;
		private IntPtr hwndHost;
		private int hostHeight;
		private int hostWidth;

		public Action<TimeSpan> Render;

		public long Ready = 0;

		public long HasResourses = 0;

		[Flags]
		enum WindowStyle : int
		{
			WS_CHILD = 0x40000000,
			WS_VISIBLE = 0x10000000,
			LBS_NOTIFY = 0x00000001,
			HOST_ID = 0x00000002,
			LISTBOX_ID = 0x00000001,
			WS_VSCROLL = 0x00200000,
			WS_BORDER = 0x00800000,
		}

		public Direct3DHost(double width, double height)
		{
			hostWidth = (int)width;
			hostHeight = (int)height;
		}

		public void Start()
		{
			if (Interlocked.Exchange(ref Ready, 1) != 1)
			{
				if (Interlocked.Exchange(ref HasResourses, 1) != 1)
				{
					CreateRenderResources();
				}
			}
		}

		public void InitDirect3D(bool UseDirect3D12)
		{
			currentDpi = VisualTreeHelper.GetDpi(this);
			if (UseDirect3D12)
			{
				var hr = Vortice.Direct3D12.D3D12.D3D12CreateDevice(null, out _device12);
				_commandQueue12 = _device12.CreateCommandQueue(Vortice.Direct3D12.CommandListType.Direct);
				Log.LogDebug($"D3D12Device create result: {hr.Success} code: {hr.Code}");
				var hr1 = Vortice.Direct3D11on12.Apis.D3D11On12CreateDevice(_device12, Vortice.Direct3D11.DeviceCreationFlags.BgraSupport, [Vortice.Direct3D.FeatureLevel.Level_11_1], [_commandQueue12], 0, out _device, out _deviceContext, out _);
				Log.LogDebug($"D3D11On12Device create result: {hr1.Success} code: {hr1.Code}");
			}
			else
			{
				var hr = Vortice.Direct3D11.D3D11.D3D11CreateDevice(null, Vortice.Direct3D.DriverType.Hardware, Vortice.Direct3D11.DeviceCreationFlags.BgraSupport, [Vortice.Direct3D.FeatureLevel.Level_11_1], out _device);
				_deviceContext = _device.CreateDeferredContext();
				Log.LogDebug($"D3D11Device create result: {hr.Success} code: {hr.Code}");
			}
			_DXGIDevice = _device.QueryInterface<Vortice.DXGI.IDXGIDevice>();
			_factory2D = Vortice.Direct2D1.D2D1.D2D1CreateFactory<Vortice.Direct2D1.ID2D1Factory1>(Vortice.Direct2D1.FactoryType.MultiThreaded, Vortice.Direct2D1.DebugLevel.None);
			_device2D = _factory2D.CreateDevice(_DXGIDevice);
			_deviceContext2D = _device2D.CreateDeviceContext(Vortice.Direct2D1.DeviceContextOptions.EnableMultithreadedOptimizations);

		}

		public void DestoryDirect3D()
		{
			_DcompDevice.Dispose();
			_DXGIDevice.Dispose();
			_deviceContext?.Dispose();
			_device?.Dispose();
			_commandQueue12?.Dispose();
			_device12?.Dispose();
		}

		private void CreateRenderResources()
		{
			_texture2D = _device.CreateTexture2D(Vortice.DXGI.Format.B8G8R8A8_UNorm, (uint)hostWidth, (uint)hostHeight);
			_t2dSurface = _texture2D.QueryInterface<Vortice.DXGI.IDXGISurface1>();
			_t2dBitmap1 = _deviceContext2D.CreateBitmapFromDxgiSurface(_t2dSurface, new(new(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), (float)(currentDpi.DpiScaleX * 96d), (float)(currentDpi.DpiScaleY * 96d), Vortice.Direct2D1.BitmapOptions.None));
			var hr2 = Vortice.DirectComposition.DComp.DCompositionCreateDevice(_DXGIDevice, out _DcompDevice);
			Log.LogDebug($"DcompDevice create result: {hr2.Success} code: {hr2.Code}");
			var hr3 = _DcompDevice.CreateTargetForHwnd(hwndHost, true, out _DcompTarget);
			Log.LogDebug($"DcompTarget create result: {hr3.Success} code: {hr3.Code}");
			var hr4 = _DcompDevice.CreateVisual(out _DcompVisual);
			Log.LogDebug($"DcompVisual create result: {hr4.Success} code: {hr4.Code}");
			var hr5 = _DcompDevice.CreateSurface((uint)hostWidth, (uint)hostHeight, Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DXGI.AlphaMode.Premultiplied, out _DcompSurface);
			Log.LogDebug($"DcompSurface create result: {hr5.Success} code: {hr5.Code}");
			_DcompVisual.SetContent(_DcompSurface);
			_DcompVisual.SetOffsetX(0);
			_DcompVisual.SetOffsetY(0);
			_DcompTarget.SetRoot(_DcompVisual);
			_DcompDevice.Commit();
			BindOpenGL();
		}

		private void DestoryRenderResources()
		{
			UnbindOpenGL();
			_texture2D?.Dispose();
			_DcompSurface?.Dispose();
			_DcompVisual?.Dispose();
			_DcompTarget?.Dispose();
		}

		private unsafe void BindOpenGL()
		{
			_openGLDeviceHandle = Wgl.DXOpenDeviceNV(_device.NativePointer);
			GLUtility.CheckError();
			_openGLFrameBufferHandle = GL.GenFramebuffer();
			GLUtility.CheckError();
			_openGLRenderBufferHandle = GL.GenRenderbuffer();
			GLUtility.CheckError();
			_openGLID3D11Texture2DHandle = Wgl.DXRegisterObjectNV(_openGLDeviceHandle, _texture2D.NativePointer, (uint)_openGLRenderBufferHandle, (uint)RenderbufferTarget.Renderbuffer, OpenTK.Platform.Windows.WGL_NV_DX_interop.AccessReadWrite);
			GLUtility.CheckError();
		}

		public void RenderLoop(TimeSpan ts)
		{
			Wgl.DXLockObjectsNV(_openGLDeviceHandle, 1, [_openGLID3D11Texture2DHandle]);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, _openGLFrameBufferHandle);
			GL.Viewport(0, 0, hostWidth, hostHeight);
			Render?.Invoke(ts);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			GL.Flush();
			Wgl.DXUnlockObjectsNV(_openGLDeviceHandle, 1, [_openGLID3D11Texture2DHandle]);
			var dXGISurface1 = _DcompSurface.BeginDraw<Vortice.DXGI.IDXGISurface1>(null, out _);
			var rt = _factory2D.CreateDxgiSurfaceRenderTarget(dXGISurface1, new() { DpiX = (float)(currentDpi.DpiScaleX * 96d), DpiY = (float)(currentDpi.DpiScaleY * 96d), MinLevel = Vortice.Direct2D1.FeatureLevel.Default, PixelFormat = new(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), Type = Vortice.Direct2D1.RenderTargetType.Hardware, Usage = Vortice.Direct2D1.RenderTargetUsage.None });
			rt.BeginDraw();
			rt.DrawBitmap(_t2dBitmap1);
			rt.EndDraw();
			rt.Dispose();
			dXGISurface1.Dispose();
			_DcompSurface.EndDraw();
			_DcompDevice.Commit();
			_DcompDevice.WaitForCommitCompletion();
		}

		private void UnbindOpenGL()
		{
			GL.DeleteFramebuffer(_openGLFrameBufferHandle);
			GL.DeleteRenderbuffer(_openGLRenderBufferHandle);
			Wgl.DXUnregisterObjectNV(_openGLDeviceHandle, _openGLID3D11Texture2DHandle);
			Wgl.DXCloseDeviceNV(_openGLDeviceHandle);
		}

		//private void Draw()
		//{
		//	var dXGISurface1 = _DcompSurface.BeginDraw<Vortice.DXGI.IDXGISurface1>(null, out _);
		//	var rt = _factory2D.CreateDxgiSurfaceRenderTarget(dXGISurface1, new() { DpiX = (float)(currentDpi.DpiScaleX * 96d), DpiY = (float)(currentDpi.DpiScaleY * 96d), MinLevel = Vortice.Direct2D1.FeatureLevel.Default, PixelFormat = new(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), Type = Vortice.Direct2D1.RenderTargetType.Hardware, Usage = Vortice.Direct2D1.RenderTargetUsage.None });
		//	var brush = rt.CreateSolidColorBrush(Vortice.Mathematics.Colors.AliceBlue);
		//	rt.BeginDraw();
		//	rt.FillRectangle(new Vortice.Mathematics.Rect(0, 0, 1000, 1000), brush);
		//	rt.EndDraw();
		//	brush.Dispose();
		//	rt.Dispose();
		//	dXGISurface1.Dispose();
		//	_DcompSurface.EndDraw();
		//	_DcompDevice.Commit();
		//}



		protected override HandleRef BuildWindowCore(HandleRef hwndParent)
		{
			hwndHost = CreateWindowEx(
			0, "STATIC", "",
			(int)(WindowStyle.WS_CHILD | WindowStyle.WS_VISIBLE),
			0, 0,
			hostWidth, hostHeight,
			hwndParent.Handle,
			(IntPtr)WindowStyle.HOST_ID,
			IntPtr.Zero, 0);
			if (Ready == 1)
			{
				if (Interlocked.Exchange(ref HasResourses, 1) == 1)
				{
					DestoryRenderResources();
				}
				CreateRenderResources();
			}
			return new HandleRef(this, hwndHost);
		}

		protected override void DestroyWindowCore(HandleRef hwnd)
		{
			if (Ready == 1)
			{
				if (Interlocked.Exchange(ref HasResourses, 0) == 1)
				{
					DestoryRenderResources();
				}
			}
			DestroyWindow(hwndHost);
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			currentDpi = VisualTreeHelper.GetDpi(this);
			hostWidth = (int)(ActualWidth * currentDpi.DpiScaleX);
			hostHeight = (int)(ActualHeight * currentDpi.DpiScaleY);
			base.OnRenderSizeChanged(sizeInfo);
		}

		[DllImport("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Unicode)]
		private static extern IntPtr CreateWindowEx(int dwExStyle,
													  string lpszClassName,
													  string lpszWindowName,
													  int style,
													  int x, int y,
													  int width, int height,
													  IntPtr hwndParent,
													  IntPtr hMenu,
													  IntPtr hInst,
													  [MarshalAs(UnmanagedType.AsAny)] object pvParam);

		[DllImport("user32.dll", EntryPoint = "DestroyWindow", CharSet = CharSet.Unicode)]
		private static extern bool DestroyWindow(IntPtr hwnd);

		[DllImport("opengl32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		private static extern IntPtr wglGetProcAddress(string lpszProc);
	}
}
