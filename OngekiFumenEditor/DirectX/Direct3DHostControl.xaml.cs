using OpenTK.Graphics.Wgl;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using Xv2CoreLib.Resource.App;

namespace OngekiFumenEditor.DirectX
{
	/// <summary>
	/// Direct3DHost.xaml 的交互逻辑
	/// </summary>
	public partial class Direct3DHostControl : UserControl
	{
		public Direct3DHostControl()
		{
			InitializeComponent();
			Loaded += Direct3DHostControl_Loaded;
			Unloaded += Direct3DHostControl_Unloaded;
		}
		private Direct3DHost d3dHost;

		public event Action<TimeSpan> Render
		{
			add
			{
				d3dHost.Render += value;
			}
			remove { d3dHost.Render -= value; }
		}

		public Action Ready;

		private Thread LoopThread = null;

		private volatile bool alive = true;

		private static long _ContextState = 0;
		private long _OpenGLReady = 0;
		private long _Direct3DReady = 0;

		private void Direct3DHostControl_Loaded(object sender, RoutedEventArgs e)
		{
			var currentDpi = VisualTreeHelper.GetDpi(this);
			d3dHost = new Direct3DHost(ActualWidth * currentDpi.DpiScaleX, ActualHeight * currentDpi.DpiScaleY);
			Dispatcher.Invoke(() =>
			{
				d3dHost.InitDirect3D(false);
				CompositionHostElement.Child = d3dHost;
				if (Interlocked.Exchange(ref _Direct3DReady, 1) != 1)
				{
					d3dHost?.Start();
					if (_OpenGLReady == 1)
					{
						d3dHost.Start();
						LoopThread = new(Loop);
						LoopThread.Start();
						Ready?.Invoke();
					}
				}
			});
		}

		private void Loop()
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			TimeSpan LastTime = stopwatch.Elapsed;
			while (alive)
			{
				d3dHost.RenderLoop(stopwatch.Elapsed - LastTime);
				LastTime = stopwatch.Elapsed;
				Thread.Sleep(1);
			}
			stopwatch.Stop();
		}

		private void Direct3DHostControl_Unloaded(object sender, RoutedEventArgs e)
		{
			d3dHost.DestoryDirect3D();
		}

		public void Start()
		{
			Dispatcher.Invoke(() =>
			{
				if(Interlocked.Exchange(ref _OpenGLReady, 1) != 1)
				{
					if (_Direct3DReady == 1)
					{
						d3dHost?.Start();
						Ready?.Invoke();
					}
				}
			});
		}

		static IGLFWGraphicsContext _sharedContext;
		static GLWpfControlSettings _sharedContextSettings;
		static IDisposable[] _sharedContextResources;

		public void InitContext()
		{
			if (Interlocked.Exchange(ref _ContextState, 1) == 1)
			{
				return;
			}

			var isCompatability = Properties.ProgramSetting.Default.GraphicsCompatability;
			var setting = isCompatability ? new GLWpfControlSettings()
			{
				MajorVersion = 3,
				MinorVersion = 3,
				GraphicsContextFlags = ContextFlags.Debug | ContextFlags.ForwardCompatible,
				GraphicsProfile = ContextProfile.Compatability
			} : new GLWpfControlSettings()
			{
				MajorVersion = 4,
				MinorVersion = 5,
				GraphicsContextFlags = ContextFlags.Debug,
				GraphicsProfile = ContextProfile.Core
			};
			NativeWindowSettings @default = NativeWindowSettings.Default;
			@default.StartFocused = false;
			@default.StartVisible = false;
			@default.NumberOfSamples = 0;
			@default.APIVersion = new Version(setting.MajorVersion, setting.MinorVersion);
			@default.Flags = ContextFlags.Offscreen | setting.GraphicsContextFlags;
			@default.Profile = setting.GraphicsProfile;
			@default.WindowBorder = WindowBorder.Hidden;
			@default.WindowState = OpenTK.Windowing.Common.WindowState.Minimized;
			NativeWindow nativeWindow = new NativeWindow(@default);
			Wgl.LoadBindings(new GLFWBindingsContext());
			System.Windows.Window window = System.Windows.Window.GetWindow(new DependencyObject());
			IntPtr parent = ((window == null) ? IntPtr.Zero : new WindowInteropHelper(window).Handle);
			HwndSource hwndSource = new HwndSource(0, 0, 0, 0, 0, "GLWpfControl", parent);
			_sharedContext = nativeWindow.Context;
			_sharedContextSettings = setting;
			_sharedContextResources = new IDisposable[2] { hwndSource, nativeWindow };
			_sharedContext.MakeCurrent();
		}
	}
}
