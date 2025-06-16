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
    [Export(typeof(IDrawingManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultOpenGLDrawingManager : IDrawingManager
    {
        // Import the necessary Win32 functions
        [DllImport("opengl32.dll")]
        private static extern nint wglGetCurrentDC();

        [DllImport("opengl32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern nint wglGetProcAddress(string lpszProc);

        private IGraphicsContext sharedContext;

        private static bool IsWGL_NV_DX_interopSupported()
        {
            var hdc = wglGetCurrentDC();
            var functionPointer = wglGetProcAddress("wglDXSetResourceSharingNV");
            return functionPointer != nint.Zero;
        }

        TaskCompletionSource initTaskSource = new TaskCompletionSource();
        bool startedInit = false;
        private DpiScale currentDPI;

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

            #endregion

            #region DPI watcher

            var mainWindow = Application.Current.MainWindow;
            var source = PresentationSource.FromVisual(mainWindow);
            if (source != null)
            {
                currentDPI = VisualTreeHelper.GetDpi(mainWindow);
                mainWindow.DpiChanged += MainWindow_DpiChanged;
                Log.LogInfo($"currentDPI: {currentDPI.DpiScaleX},{currentDPI.DpiScaleY}");
            }

            #endregion

            initTaskSource.SetResult();
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            Log.LogInfo($"currentDPI: {currentDPI.DpiScaleX},{currentDPI.DpiScaleY} -> {e.NewDpi.DpiScaleX},{e.NewDpi.DpiScaleY}");
            currentDPI = e.NewDpi;
        }

        private static void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, nint message, nint userParam)
        {
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

            Log.LogDebug($"GraphicsCompatability: {isCompatability}");
            Log.LogDebug($"OutputGraphicsLog: {isOutputLog}");

            Log.LogDebug($"GLWpfControlSettings.Version: {setting.MajorVersion}.{setting.MinorVersion}");
            Log.LogDebug($"GLWpfControlSettings.GraphicsContextFlags: {setting.ContextFlags}");
            Log.LogDebug($"GLWpfControlSettings.GraphicsProfile: {setting.Profile}");

            glView.Start(setting);

            sharedContext = sharedContext ?? glView.Context;

            return Task.CompletedTask;
        }

        public IImage LoadImageFromStream(Stream stream)
        {
            using var bitmap = Image.FromStream(stream) as Bitmap;
            return new Texture(bitmap);
        }

        public void BeforeRender(IDrawingContext context)
        {
            var renderViewWidth = (int)((context.CurrentDrawingTargetContext?.ViewWidth ?? 0) * currentDPI.DpiScaleX);
            var renderViewHeight = (int)((context.CurrentDrawingTargetContext?.ViewHeight ?? 0) * currentDPI.DpiScaleY);

            GL.Viewport(0, 0, renderViewWidth, renderViewHeight);
        }

        public void AfterRender(IDrawingContext context)
        {

        }

        public void CleanRender(Vector4 cleanColor)
        {
            GL.ClearColor(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
    }
}
