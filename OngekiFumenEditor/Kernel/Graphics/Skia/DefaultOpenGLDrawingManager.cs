//#define OGL_LOG
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Utils;
using OpenTK.Windowing.Common;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using System;
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

        private void InitializeOpenGL()
        {

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

        public Task InitializeRenderControl(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            if (renderControl is not SKElement skElement)
                throw new Exception($"renderControl must be {nameof(SKElement)} object.");

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

        public IImage LoadImageFromStream(Stream stream)
        {
            CheckInitialization();

            return new SkiaImage(SKImage.FromEncodedData(stream));
        }

        public Task<IRenderContext> GetRenderContext(FrameworkElement renderControl, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }
    }
}
