//#define OGL_LOG
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Graphics
{
	[Export(typeof(IDrawingManager))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class DefaultDrawingManager : IDrawingManager
	{
		// Import the necessary Win32 functions
		[DllImport("opengl32.dll")]
		private static extern IntPtr wglGetCurrentDC();

		[DllImport("opengl32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		private static extern IntPtr wglGetProcAddress(string lpszProc);

		private static bool IsWGL_NV_DX_interopSupported()
		{
			var hdc = wglGetCurrentDC();
			var functionPointer = wglGetProcAddress("wglDXSetResourceSharingNV");
			return functionPointer != IntPtr.Zero;
		}

		TaskCompletionSource initTaskSource = new TaskCompletionSource();
		bool startedInit = false;

		public Task CheckOrInitGraphics()
		{
			if (!startedInit)
			{
				startedInit = true;
				Dispatcher.CurrentDispatcher.InvokeAsync(OnInitOpenGL);
			}

			return initTaskSource.Task;
		}

		private void OnInitOpenGL()
		{
			if (Properties.ProgramSetting.Default.OutputGraphicsLog)
			{
				GL.DebugMessageCallback(OnOpenGLDebugLog, IntPtr.Zero);
				GL.Enable(EnableCap.DebugOutput);
				if (Properties.ProgramSetting.Default.GraphicsLogSynchronous)
					GL.Enable(EnableCap.DebugOutputSynchronous);
			}

			GL.ClearColor(System.Drawing.Color.Black);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

			Log.LogDebug($"Prepare OpenGL version : {GL.GetInteger(GetPName.MajorVersion)}.{GL.GetInteger(GetPName.MinorVersion)}");

			try
			{
				var isSupport = IsWGL_NV_DX_interopSupported();
				Log.LogDebug($"WGL_NV_DX_interop support: {isSupport}");
			}
			catch
			{
				Log.LogDebug($"WGL_NV_DX_interop support: EXCEPTION");
			}

			if (Properties.ProgramSetting.Default.GraphicsCompatability)
			{
				var extNames = string.Join(", ", Enumerable.Range(0, GL.GetInteger(GetPName.NumExtensions)).Select(i => GL.GetString(StringNameIndexed.Extensions, i)));
				Log.LogDebug($"(maybe support) OpenGL extensions: {extNames}");
			}

			initTaskSource.SetResult();
		}

		private static void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
		{
			if (id == 131185)
				return;

			var str = Marshal.PtrToStringAnsi(message, length);
			Log.LogDebug($"[{source}.{type}]{id}:  {str}");
		}

		public Task WaitForGraphicsInitializationDone(CancellationToken cancellation)
		{
			return initTaskSource.Task;
		}

		public Task CreateGraphicsContext(GLWpfControl glView, CancellationToken cancellation = default)
		{
			var isCompatability = Properties.ProgramSetting.Default.GraphicsCompatability;
			var isOutputLog = Properties.ProgramSetting.Default.OutputGraphicsLog;

			var flag = isOutputLog ? ContextFlags.Debug : ContextFlags.Default;

			var setting = isCompatability ? new GLWpfControlSettings()
			{
				MajorVersion = 3,
				MinorVersion = 3,
				GraphicsContextFlags = flag | ContextFlags.ForwardCompatible,
				GraphicsProfile = ContextProfile.Compatability
			} : new GLWpfControlSettings()
			{
				MajorVersion = 4,
				MinorVersion = 5,
				GraphicsContextFlags = flag,
				GraphicsProfile = ContextProfile.Core
			};

			Log.LogDebug($"GraphicsCompatability: {isCompatability}");
			Log.LogDebug($"OutputGraphicsLog: {isOutputLog}");

			Log.LogDebug($"GLWpfControlSettings.Version: {setting.MajorVersion}.{setting.MinorVersion}");
			Log.LogDebug($"GLWpfControlSettings.GraphicsContextFlags: {setting.GraphicsContextFlags}");
			Log.LogDebug($"GLWpfControlSettings.GraphicsProfile: {setting.GraphicsProfile}");

			glView.Start(setting);

			return Task.CompletedTask;
		}
	}
}
