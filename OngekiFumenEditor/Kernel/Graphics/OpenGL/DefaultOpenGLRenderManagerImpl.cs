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
                    throw new ObjectDisposedException(nameof(DefaultOpenGLRenderManagerImpl), "离屏渲染队列已关闭。");

                if (activeRenderContexts.Count == 0)
                    throw new InvalidOperationException("创建 OpenGL 离屏上下文要求至少存在一个已开始渲染（StartRendering）的 GL 控件。");
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
            // 同步完成：取消排队请求、（若有 current 上下文）执行延迟删除；绝不等待渲染 tick（退出路径上等 tick 会死锁）。
            offscreenRenderQueue.Terminate();
            Log.LogInfo("[离屏] OpenGL 离屏渲染队列已关闭。");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 在 GL 控件渲染回调（上下文 current）内排空离屏渲染队列。
        /// 每个 tick 都必须调用，包括被 FPS 闸门丢弃的 tick。
        /// </summary>
        internal void PumpOffscreenRenders()
        {
            offscreenRenderQueue.Pump(this);
        }

        /// <summary>登记一个待删除的 GL 纹理（延迟删除：下次 drain 或退出时执行）。</summary>
        internal void EnqueueTextureDeletion(int textureId)
        {
            if (textureId != 0)
                offscreenRenderQueue.EnqueueDeletion(new OpenGLPendingDeletion(textureId, 0));
        }

        /// <summary>登记一个待删除的 GL 帧缓冲。</summary>
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
                // 最后一个渲染中的 GL 控件停止后不再产生 tick，挂起中的排队请求必须以异常结束，避免永久等待。
                offscreenRenderQueue.CancelQueued(null, new OperationCanceledException("没有处于渲染中的 GL 控件，排队中的离屏渲染请求被取消。"));
            }
        }

        /// <summary>当前线程是否存在 current 的 GL 上下文（用于判断能否安全执行 GL 调用）。</summary>
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
