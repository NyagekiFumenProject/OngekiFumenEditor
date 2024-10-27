﻿using OpenTK.Wpf.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;

namespace OpenTK.Wpf
{
	/// <summary>
	/// DCompGL.xaml 的交互逻辑
	/// </summary>
	public partial class DCompGL : UserControl
	{
		DCompGLHost host;
		GLWpfControl GLCore;
		Thread LoopThread;
		IntPtr hwndHost;

		public event Action? Ready { add { GLCore.Ready += value; } remove { GLCore.Ready -= value; } }
		public event Action<TimeSpan>? Render { add { GLCore.Render += value; } remove { GLCore.Render -= value; } }
		public new event System.Windows.Input.MouseWheelEventHandler MouseWheel { add { CompositionHostElement.MouseWheel += value; } remove { CompositionHostElement.MouseWheel -= value; } }
		public new event System.Windows.Input.MouseButtonEventHandler PreviewMouseDown { add { CompositionHostElement.PreviewMouseDown += value; } remove { CompositionHostElement.PreviewMouseDown -= value; } }
		public new event System.Windows.Input.MouseButtonEventHandler PreviewMouseUp { add { CompositionHostElement.PreviewMouseUp += value; } remove { CompositionHostElement.PreviewMouseUp -= value; } }
		public new event System.Windows.Input.MouseEventHandler MouseMove { add { CompositionHostElement.MouseMove += value; } remove { CompositionHostElement.MouseMove -= value; } }
		public new event System.Windows.Input.MouseEventHandler MouseLeave { add { CompositionHostElement.MouseLeave += value; } remove { CompositionHostElement.MouseLeave -= value; } }
		private object disposeLock = new object();
		volatile bool loop = true;
		private double hostWidthWithDPI;
		private double hostHeightWithDPI;

		public DCompGL()
		{
			InitializeComponent();
			Loaded += DCompGL_Loaded;
			Unloaded += DCompGL_Unloaded;
			GLCore = new();
		}

		private void DCompGL_Unloaded(object sender, RoutedEventArgs e)
		{
			loop = false;
			hwndHost = IntPtr.Zero;
			host?.Dispose();
		}

		private void DCompGL_Loaded(object sender, RoutedEventArgs e)
		{
			loop = true;
			hostWidthWithDPI = RenderSize.Width;
			hostHeightWithDPI = RenderSize.Height;
			host = new(GetParentSize);
			host.Resized += HostResized;
			CompositionHostElement.Child = host;
		}

		private (double, double) GetParentSize()
		{
			return (RenderSize.Width, RenderSize.Height);
		}

		private void HostResized(IntPtr hWnd)
		{
			if (hwndHost == IntPtr.Zero)
			{
				hwndHost = hWnd;
				LoopThread = new(EntryPoint);
				LoopThread.Start();
				//host.WMPaint += GLCore.RenderD3D;
				host.WMResize += (width, height) =>
				{
					double dpi = DXInterop.GetDpiForSystem() / 96d;
					hostWidthWithDPI = width/ dpi;
					hostHeightWithDPI = height/ dpi;
				};
			}
			else
			{
				hwndHost = hWnd;
			}
		}

		public void EntryPoint()
		{
			var DesignMode = false;
			Dispatcher.Invoke(() =>
			{
				DesignMode = DesignerProperties.GetIsInDesignMode(this);
			});
			while (loop)
			{
				if (hwndHost != IntPtr.Zero)
				{
					Dispatcher.Invoke(() =>
					{
						GLCore.OnRender(DesignMode, hostWidthWithDPI, hostHeightWithDPI, hwndHost);
					});
					GLCore.RenderD3D();
					GLCore.WaitForVBlank();
				}
				else
				{
					Thread.Sleep(32);
				}
			}
			Dispatcher.Invoke(() =>
			{
				//GLCore.Dispose();
				host.Dispose();
			});
		}

		public void Start(GLWpfControlSettings settings)
		{
			Dispatcher.Invoke(() =>
			{
				GLCore.Start(settings);
			});
		}
	}

	public partial class DCompGLHost : HwndHost
	{
		private DpiScale currentDpi;
		private IntPtr hwndHost;
		private int hostWidth;
		private int hostHeight;
		public Func<(double, double)> getSizeFunc;

		public Action<IntPtr>? Resized;

		public Action<uint,uint>? WMResize;

		public Action? WMPaint;

		public DCompGLHost(Func<(double, double)> GetSize)
		{
			getSizeFunc = GetSize;
			var (width, height) = getSizeFunc();
			Resize(width, height);
		}

		public DCompGLHost(int PixelWidth, int PixelHeight)
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

		[Flags]
		enum WindowStyle : int
		{
			WS_CLIPCHILDREN = 0x02000000,
			WS_CHILD = 0x40000000,
			WS_VISIBLE = 0x10000000,
			LBS_NOTIFY = 0x00000001,
			HOST_ID = 0x00000002,
			LISTBOX_ID = 0x00000001,
			WS_VSCROLL = 0x00200000,
			WS_BORDER = 0x00800000,
		}

		[Flags]
		enum WindowStyleEx : int
		{
			WS_EX_NOREDIRECTIONBITMAP = 0x00200000,
		}
		protected override HandleRef BuildWindowCore(HandleRef hwndParent)
		{
			var (width, height) = getSizeFunc();
			Resize(width, height);
			hwndHost = CreateWindowEx(
			(int)(WindowStyleEx.WS_EX_NOREDIRECTIONBITMAP), "STATIC", "",
			(int)(WindowStyle.WS_CHILD | WindowStyle.WS_VISIBLE),
			0, 0,
			hostWidth, hostHeight,
			hwndParent.Handle,
			(IntPtr)WindowStyle.HOST_ID,
			IntPtr.Zero, 0);
			Resized?.Invoke(hwndHost);
			return new HandleRef(this, hwndHost);
		}

		protected override nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
		{
			switch (msg)
			{
				case 0x0005:
					var width = LOWORD((uint)lParam);
					var height = HIWORD((uint)lParam);
					WMResize?.Invoke(width, height);
					//SetWindowPos(hwnd, 0, 0, 0, (int)width, (int)height, 0x4000 | 0x0200 | 0x0008 | 0x0002 | 0x0004);
					handled = true;
					return 0;
				case 0x000F:
					WMPaint?.Invoke();
					handled = true;
					return 0;
				default:
					break;
			}
			return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
		}

		private static ushort LOWORD(uint value)
		{
			return (ushort)(value & 0xFFFF);
		}

		private static ushort HIWORD(uint value)
		{
			return (ushort)(value >> 16);
		}

		protected override void DestroyWindowCore(HandleRef hwnd)
		{
			DestroyWindow(hwndHost);
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

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int X, int Y, int cx, int cy, uint flag);
	}
}
