//#define OGL_LOG
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.LineDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.CircleDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.PolygonDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.StringDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.TextureDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU;
using OngekiFumenEditor.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    [Export(typeof(IRenderManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultSkiaDrawingManager : IRenderManager
    {
        private TaskCompletionSource initTaskSource = new TaskCompletionSource();
        private bool initialized = false;
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

        private void Initialize()
        {
            #region Create Drawings

            Log.LogInfo("Drawing objects were created.");

            CircleDrawing = new DefaultSkiaCircleDrawing(this);
            LineDrawing = new DefaultSkiaLineDrawing(this);
            SimpleLineDrawing = new DefaultSkiaLineDrawing(this);
            StaticVBODrawing = new DefaultSkiaLineDrawing(this);
            StringDrawing = new DefaultSkiaStringDrawing(this);
            TextureDrawing = new DefaultSkiaTextureDrawing(this);
            PolygonDrawing = new DefaultSkiaPolygonDrawing(this);
            HighlightBatchTextureDrawing = new DefaultSkiaHighlightBatchTextureDrawing(this);
            BatchTextureDrawing = new DefaultSkiaBatchTextureDrawing(this);

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
            else
            {
                Log.LogError("Listening DPI Changing failed, PresentationSource.FromVisual(mainWindow) return null.");
            }

            #endregion

            Log.LogInfo("OpenGL Drawing Manager initialized successfully.");
            initTaskSource.SetResult();
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (currentDPI.DpiScaleX != e.NewDpi.DpiScaleX || currentDPI.DpiScaleY != e.NewDpi.DpiScaleY)
                Log.LogInfo($"currentDPI changed: {currentDPI.DpiScaleX},{currentDPI.DpiScaleY} -> {e.NewDpi.DpiScaleX},{e.NewDpi.DpiScaleY}");
            currentDPI = e.NewDpi;
        }

        public Task WaitForInitializationIsDone(CancellationToken cancellation)
        {
            return initTaskSource.Task;
        }

        public Task InitializeRenderControl(FrameworkElement rc, CancellationToken cancellation = default)
        {
            var renderControl = CheckRenderControl(rc);

            if (!initialized)
            {
                initialized = true;

                Log.LogDebug($"Start to invoke {nameof(DefaultSkiaDrawingManager)}::Initialize()");
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

        Dictionary<FrameworkElement, DefaultSkiaRenderContext> cachedRenderControlMap = new();

        public IImage LoadImageFromStream(Stream stream)
        {
            CheckInitialization();

            return new SkiaImage(SKImage.FromEncodedData(stream));
        }

        public async Task<IRenderContext> GetRenderContext(FrameworkElement rc, CancellationToken cancellation = default)
        {
            return await GetSkiaRenderContext(rc, cancellation);
        }

        public Task<DefaultSkiaRenderContext> GetSkiaRenderContext(FrameworkElement rc, CancellationToken cancellation = default)
        {
            var renderControl = CheckRenderControl(rc);

            if (!cachedRenderControlMap.TryGetValue(renderControl, out var renderContext))
                renderContext = cachedRenderControlMap[renderControl] = new DefaultSkiaRenderContext(this, renderControl);

            return Task.FromResult(renderContext);
        }

        private SkiaRenderControlBase CheckRenderControl(FrameworkElement rc)
        {
            if (rc is not SkiaRenderControlBase renderControl)
                throw new Exception("renderControl must be SkiaRenderControl object.");
            return renderControl;
        }

        public FrameworkElement CreateRenderControl()
        {
            var backend = RenderBackendType.DirectX;

            switch (backend)
            {
                case RenderBackendType.OpenGL:
                    return new SkiaRenderControl_OpenGL();
                case RenderBackendType.DirectX:
                    return new SkiaRenderControl_DirectX();
                case RenderBackendType.CPU:
                default:
                    return new SkiaRenderControl_CPU();
            }
        }
    }
}
