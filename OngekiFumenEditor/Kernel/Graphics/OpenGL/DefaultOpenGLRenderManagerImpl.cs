//#define OGL_LOG
using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// OpenGL implementation of the render manager.
    /// </summary>
    [Export(typeof(IRenderManagerImpl))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultOpenGLRenderManagerImpl : IRenderManagerImpl
    {
        private readonly DrawCommandListContextSlots drawCommandListContextSlots = new();

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

        /// <summary>
        /// Gets the current display DPI captured from the main window.
        /// </summary>
        public DpiScale CurrentDPI { get; private set; }

        /// <inheritdoc />
        public string Name { get; } = "OpenGL";

        private void Initialize()
        {
            Log.LogInfo("OpenGL Drawing Manager initializing...");
            InitializeOpenGL();

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

        /// <inheritdoc />
        public Task WaitForInitializationIsDone(CancellationToken cancellation)
        {
            return initTaskSource.Task;
        }

        /// <inheritdoc />
        public async Task InitializeRenderControl(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            var glView = CheckRenderControl(renderControl);
            var renderCtx = (await GetOrCreateRenderContext(glView, cancellation)) as DefaultOpenGLRenderContext;

            if (renderCtx.IsInitialized)
                return;

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
                Dispatcher.CurrentDispatcher.InvokeAsync(Initialize).Task.NoWait();
            }

            renderCtx.IsInitialized = true;

            await WaitForInitializationIsDone(cancellation);
        }

        [Conditional("DEBUG")]
        private void CheckInitialization()
        {
            if (!initialized)
                throw new Exception("Only able to call after InitializeRenderControl() called.");
        }

        /// <inheritdoc />
        public IImage LoadImageFromStream(Stream stream)
        {
            CheckInitialization();

            using var bitmap = Image.FromStream(stream) as Bitmap;
            return new DefaultOpenGLTexture(bitmap);
        }

        Dictionary<FrameworkElement, IRenderContext> cachedRenderControlMap = new();

        /// <inheritdoc />
        public Task<IRenderContext> GetOrCreateRenderContext(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            var glView = CheckRenderControl(renderControl);

            if (!cachedRenderControlMap.TryGetValue(renderControl, out var renderContext))
                renderContext = cachedRenderControlMap[renderControl] = new DefaultOpenGLRenderContext(this, glView);

            return Task.FromResult(renderContext);
        }

        private GLWpfControl CheckRenderControl(FrameworkElement renderControl)
        {
            if (renderControl is not GLWpfControl glView)
                throw new Exception("renderControl must be GLWpfControl object.");
            return glView;
        }

        /// <inheritdoc />
        public FrameworkElement CreateRenderControl()
        {
            var glControl = new GLWpfControl()
            {

            };

            return glControl;
        }

        /// <inheritdoc />
        public IDrawCommandListBuilder CreateDrawCommandListBuilder()
        {
            return new DrawCommandListBuilder(new DefaultStringDrawing(this));
        }

        /// <inheritdoc />
        public void PostDrawCommandList(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true)
        {
            drawCommandListContextSlots.Post(context, drawCommandList, autoDispose);
        }

        /// <inheritdoc />
        public bool SwapDrawCommandList(IRenderContext context)
        {
            return drawCommandListContextSlots.Swap(context);
        }

        /// <inheritdoc />
        public void PresentDrawCommandList(IRenderContext context)
        {
            drawCommandListContextSlots.Present(context);
        }
    }
}
