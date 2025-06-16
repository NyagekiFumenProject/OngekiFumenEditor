//#define OGL_LOG
using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.CircleDrawing;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.LineDrawing;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.PolygonDrawing;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.SvgDrawing;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.TextureDrawing;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.BeamDrawing;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    [Export(typeof(IRenderManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultOpenGLDrawingManager : IRenderManager
    {
        private IRenderContext commonRenderContext;

        // Import the necessary Win32 functions
        [DllImport("opengl32.dll")]
        private static extern nint wglGetCurrentDC();

        [DllImport("opengl32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern nint wglGetProcAddress(string lpszProc);

        private static IGraphicsContext sharedContext;

        private static bool IsWGL_NV_DX_interopSupported()
        {
            var hdc = wglGetCurrentDC();
            var functionPointer = wglGetProcAddress("wglDXSetResourceSharingNV");
            return functionPointer != nint.Zero;
        }

        private TaskCompletionSource initTaskSource = new TaskCompletionSource();
        private bool initialized = false;

        public DpiScale CurrentDPI { get; private set; }

        public ICircleDrawing CircleDrawing { get; private set; }

        public ILineDrawing LineDrawing { get; private set; }

        public ISimpleLineDrawing SimpleLineDrawing { get; private set; }

        public IStaticVBODrawing StaticVBODrawing { get; private set; }

        public IStringDrawing StringDrawing { get; private set; }

        public ISvgDrawing SvgDrawing { get; private set; }

        public ITextureDrawing TextureDrawing { get; private set; }

        public IBatchTextureDrawing BatchTextureDrawing { get; private set; }

        public IHighlightBatchTextureDrawing HighlightBatchTextureDrawing { get; private set; }

        public IPolygonDrawing PolygonDrawing { get; private set; }

        public IBeamDrawing BeamDrawing { get; private set; }

        public DefaultOpenGLDrawingManager()
        {
            commonRenderContext = new DefaultOpenGLRenderContext(this);
        }

        private void Initialize()
        {
            Log.LogInfo("OpenGL Drawing Manager initializing...");
            InitializeOpenGL();

            #region Create Drawings

            CircleDrawing = new DefaultInstancedCircleDrawing(this);
            LineDrawing = new DefaultLineDrawing(this);
            PolygonDrawing = new DefaultPolygonDrawing(this);
            StaticVBODrawing = SimpleLineDrawing = new DefaultInstancedLineDrawing(this);
            StringDrawing = new DefaultStringDrawing(this);
            SvgDrawing = new DefaultSvgDrawing(this);
            TextureDrawing = BatchTextureDrawing = new DefaultBatchTextureDrawing(this);
            HighlightBatchTextureDrawing = new DefaultHighlightBatchTextureDrawing(this);
            BeamDrawing = new DefaultBeamDrawing(this);

            Log.LogInfo("Drawing objects were created.");

            #endregion

            #region DPI watcher

            var mainWindow = Application.Current.MainWindow;
            var source = PresentationSource.FromVisual(mainWindow);
            if (source != null)
            {
                CurrentDPI = VisualTreeHelper.GetDpi(mainWindow);
                mainWindow.DpiChanged += MainWindow_DpiChanged;
                Log.LogInfo($"currentDPI: {CurrentDPI.DpiScaleX},{CurrentDPI.DpiScaleY}");
            }
            else
            {
                Log.LogError("Listening DPI Changing failed, PresentationSource.FromVisual(mainWindow) return null.");
            }

            #endregion

            Log.LogInfo("OpenGL Drawing Manager initialized successfully.");
            initTaskSource.SetResult();
        }

        private void InitializeOpenGL()
        {
            if (Properties.ProgramSetting.Default.OutputGraphicsLog)
            {
                GL.DebugMessageCallback(OnOpenGLDebugLog, nint.Zero);
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
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (CurrentDPI.DpiScaleX != e.NewDpi.DpiScaleX || CurrentDPI.DpiScaleY != e.NewDpi.DpiScaleY)
                Log.LogInfo($"currentDPI changed: {CurrentDPI.DpiScaleX},{CurrentDPI.DpiScaleY} -> {e.NewDpi.DpiScaleX},{e.NewDpi.DpiScaleY}");
            CurrentDPI = e.NewDpi;
        }

        private static void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, nint message, nint userParam)
        {
            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"[{source}.{type}]{id}:  {str}");
        }

        public Task WaitForInitializationIsDone(CancellationToken cancellation)
        {
            return initTaskSource.Task;
        }

        public Task InitializeRenderControl(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            if (renderControl is not GLWpfControl glView)
                throw new Exception("renderControl must be GLWpfControl object.");

            var isCompatability = Properties.ProgramSetting.Default.GraphicsCompatability;
            var isOutputLog = Properties.ProgramSetting.Default.OutputGraphicsLog;

            var flag = isOutputLog ? ContextFlags.Debug : ContextFlags.Default;

            GLWpfControlSettings setting = isCompatability ? new()
            {
                MajorVersion = 3,
                MinorVersion = 3,
                ContextFlags = flag | ContextFlags.ForwardCompatible,
                Profile = ContextProfile.Compatability,
            } : new()
            {
                MajorVersion = 4,
                MinorVersion = 5,
                ContextFlags = flag,
                Profile = ContextProfile.Core
            };

            setting.ContextToUse = sharedContext;

            Log.LogDebug($"ContextToUse: {setting.ContextToUse != default}");

            Log.LogDebug($"GraphicsCompatability: {isCompatability}");
            Log.LogDebug($"OutputGraphicsLog: {isOutputLog}");

            Log.LogDebug($"GLWpfControlSettings.Version: {setting.MajorVersion}.{setting.MinorVersion}");
            Log.LogDebug($"GLWpfControlSettings.GraphicsContextFlags: {setting.ContextFlags}");
            Log.LogDebug($"GLWpfControlSettings.GraphicsProfile: {setting.Profile}");

            glView.Start(setting);

            sharedContext = sharedContext ?? glView.Context;

            if (!initialized)
            {
                initialized = true;

                Log.LogDebug($"Start to invoke DefaultOpenGLDrawingManager::Initialize()");
                Dispatcher.CurrentDispatcher.InvokeAsync(Initialize);
            }

            return WaitForInitializationIsDone(cancellation);
        }

        [Conditional("DEBUG")]
        private void CheckInitialization()
        {
            if (!initialized)
                throw new Exception("Only able to call after InitializeRenderControl() called.");
        }

        public IImage LoadImageFromStream(Stream stream)
        {
            CheckInitialization();

            using var bitmap = Image.FromStream(stream) as Bitmap;
            return new Texture(bitmap);
        }

        public Task<IRenderContext> GetRenderContext(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            return Task.FromResult(commonRenderContext);
        }
    }
}
