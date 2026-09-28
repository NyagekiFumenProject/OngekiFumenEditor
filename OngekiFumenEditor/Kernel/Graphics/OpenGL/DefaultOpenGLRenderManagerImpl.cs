//#define OGL_LOG
using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing;
using OngekiFumenEditor.Kernel.Graphics.Performence;
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
        private readonly object drawCommandListGate = new();

        private readonly object offscreenGate = new();
        private readonly OpenGLOffscreenRenderQueue offscreenRenderQueue = new();
        private readonly HashSet<DefaultOpenGLRenderContext> activeRenderContexts = new();

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
            // RGB uses ordinary straight-alpha blending. The alpha channel must accumulate linearly (source factor ONE),
            // otherwise the source alpha multiplies itself (1-a+a^2), and offscreen content rendered over an opaque
            // background comes back with alpha < 1, so pasting those textures dims every antialiased pixel.
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha,
                BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

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

            cacheStringMeasure = new DefaultStringMeasure();

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
        private DefaultStringMeasure cacheStringMeasure;

        /// <inheritdoc />
        public Task<IRenderContext> GetOrCreateRenderContext(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            var glView = CheckRenderControl(renderControl);

            if (!cachedRenderControlMap.TryGetValue(renderControl, out var renderContext))
                renderContext = cachedRenderControlMap[renderControl] = new DefaultOpenGLRenderContext(this, glView);

            return Task.FromResult(renderContext);
        }

        /// <inheritdoc />
        public IReadOnlyList<IRenderContext> GetRenderContexts()
        {
            return cachedRenderControlMap.Values.ToArray();
        }

        /// <inheritdoc />
        public bool RemoveRenderContext(IRenderContext renderContext)
        {
            if (renderContext is null)
                throw new ArgumentNullException(nameof(renderContext));

            lock (drawCommandListGate)
                drawCommandListContextSlots.Remove(renderContext);

            var pair = cachedRenderControlMap.FirstOrDefault(x => ReferenceEquals(x.Value, renderContext));
            if (pair.Key is null)
                return false;

            renderContext.StopRendering();
            renderContext.Name = default;
            renderContext.PerfomenceMonitor = DummyPerformenceMonitor.Instance;

            return cachedRenderControlMap.Remove(pair.Key);
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
            return new DrawCommandListBuilder(cacheStringMeasure);
        }

        /// <inheritdoc />
        public void PostDrawCommandList(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true)
        {
            lock (drawCommandListGate)
                drawCommandListContextSlots.Post(context, drawCommandList, autoDispose);
        }

        /// <inheritdoc />
        public bool SwapDrawCommandList(IRenderContext context)
        {
            lock (drawCommandListGate)
                return drawCommandListContextSlots.Swap(context);
        }

        /// <inheritdoc />
        public void PresentDrawCommandList(IRenderContext context)
        {
            if (context is not DefaultOpenGLRenderContext openGLRenderContext)
                throw new Exception("renderContext must be DefaultOpenGLRenderContext object.");

            lock (drawCommandListGate)
            {
                drawCommandListContextSlots.Present(openGLRenderContext, drawCommandList =>
                {
                    var perfomenceMonitor = context.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;
                    perfomenceMonitor.OnBeforePresent();
                    try
                    {
                        OpenGLDrawCommandListReplay.Present(this, openGLRenderContext, drawCommandList);
                    }
                    finally
                    {
                        perfomenceMonitor.OnAfterPresent();
                    }
                });
            }
        }

        /// <inheritdoc />
        public IOffscreenRenderContext CreateOffscreenToImage(OffscreenRenderOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            options.Validate();

            lock (offscreenGate)
            {
                if (offscreenRenderQueue.IsTerminated)
                    throw new ObjectDisposedException(nameof(DefaultOpenGLRenderManagerImpl), "The offscreen render queue has been closed.");

                if (activeRenderContexts.Count == 0)
                    throw new InvalidOperationException("Creating an OpenGL offscreen context requires at least one GL control that has started rendering (StartRendering).");
            }

            return new OpenGLOffscreenRenderContext(this, offscreenRenderQueue, options);
        }

        /// <inheritdoc />
        public IOffscreenRenderContext CreateOffscreenToImage(int width, int height)
        {
            return CreateOffscreenToImage(new OffscreenRenderOptions
            {
                Width = width,
                Height = height,
            });
        }

        /// <inheritdoc />
        public Task Term()
        {
            // Completes synchronously: cancels queued requests and (when a context is current) runs delayed deletions; never waits for a render tick (waiting for a tick on the shutdown path would deadlock).
            offscreenRenderQueue.Terminate();
            Log.LogInfo("[Offscreen] OpenGL offscreen render queue terminated.");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Drains the offscreen render queue inside a GL control render callback (with the context current).
        /// Must be called on every tick, including ticks dropped by the FPS gate.
        /// </summary>
        internal void PumpOffscreenRenders()
        {
            offscreenRenderQueue.Pump(this);
        }

        /// <summary>Registers a GL texture to delete (delayed deletion: performed on the next drain or on shutdown).</summary>
        internal void EnqueueTextureDeletion(int textureId)
        {
            if (textureId != 0)
                offscreenRenderQueue.EnqueueDeletion(new OpenGLPendingDeletion(textureId, 0));
        }

        /// <summary>Registers a GL framebuffer to delete.</summary>
        internal void EnqueueFramebufferDeletion(int framebufferId)
        {
            if (framebufferId != 0)
                offscreenRenderQueue.EnqueueDeletion(new OpenGLPendingDeletion(0, framebufferId));
        }

        internal void NotifyRenderContextStarted(DefaultOpenGLRenderContext context)
        {
            lock (offscreenGate)
                activeRenderContexts.Add(context);
        }

        internal void NotifyRenderContextStopped(DefaultOpenGLRenderContext context)
        {
            bool noActiveContext;

            lock (offscreenGate)
            {
                activeRenderContexts.Remove(context);
                noActiveContext = activeRenderContexts.Count == 0;
            }

            if (noActiveContext)
            {
                // Once the last rendering GL control stops, no more ticks are produced, so pending queued requests must end with an exception instead of waiting forever.
                offscreenRenderQueue.CancelQueued(null, new OperationCanceledException("No GL control is rendering; queued offscreen render requests were cancelled."));
            }
        }

        /// <summary>Whether a GL context is current on this thread (used to tell whether GL calls can be made safely).</summary>
        internal static bool HasCurrentGlContext()
        {
            try
            {
                return wglGetCurrentDC() != nint.Zero;
            }
            catch
            {
                return false;
            }
        }
    }
}
