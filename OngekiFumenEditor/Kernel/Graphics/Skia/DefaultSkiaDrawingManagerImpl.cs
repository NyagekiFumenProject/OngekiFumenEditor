using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Skia.Base;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.BeamDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.CircleDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.PolygonDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.StringDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.TextureDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.CPU;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.DirectX;
using OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.OpenGL;
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
using static OngekiFumenEditor.Kernel.Graphics.DrawCommands.DrawCommandListContextSlots;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// Skia implementation of the render manager.
    /// </summary>
    [Export(typeof(IRenderManagerImpl))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultSkiaDrawingManagerImpl : IRenderManagerImpl
    {
        private readonly Dictionary<IRenderContext, ContextSlot> contextSlots = new();
        private TaskCompletionSource initTaskSource = new TaskCompletionSource();
        private bool initialized = false;
        private DpiScale currentDPI;
        private RenderBackendType backendType;

        /// <inheritdoc />
        public ICircleDrawing CircleDrawing { get; private set; }

        /// <inheritdoc />
        public ILineDrawing LineDrawing { get; private set; }

        /// <inheritdoc />
        public ISimpleLineDrawing SimpleLineDrawing { get; private set; }

        /// <inheritdoc />
        public IStaticVBODrawing StaticVBODrawing { get; private set; }

        /// <inheritdoc />
        public IStringDrawing StringDrawing { get; private set; }

        /// <inheritdoc />
        public ITextureDrawing TextureDrawing { get; private set; }

        /// <inheritdoc />
        public IBatchTextureDrawing BatchTextureDrawing { get; private set; }

        /// <inheritdoc />
        public IHighlightBatchTextureDrawing HighlightBatchTextureDrawing { get; private set; }

        /// <inheritdoc />
        public IPolygonDrawing PolygonDrawing { get; private set; }

        /// <inheritdoc />
        public IBeamDrawing BeamDrawing { get; private set; }

        /// <inheritdoc />
        public string Name { get; } = "Skia";

        private void Initialize()
        {
            #region Create Drawings

            Log.LogInfo("Drawing objects were created.");

            CircleDrawing = new DefaultSkiaCircleDrawing(this);
            LineDrawing = new NewSkiaLineDrawing(this);
            SimpleLineDrawing = new NewSkiaLineDrawing(this);
            StaticVBODrawing = new NewSkiaLineDrawing(this);
            StringDrawing = new DefaultSkiaStringDrawing(this);
            TextureDrawing = new DefaultSkiaTextureDrawing(this);
            PolygonDrawing = new DefaultSkiaPolygonDrawing(this);
            HighlightBatchTextureDrawing = new DefaultSkiaHighlightBatchTextureDrawing(this);
            BatchTextureDrawing = new DefaultSkiaBatchTextureDrawing(this);
            BeamDrawing = new DefaultSkiaBeamDrawing(this);

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

            Log.LogInfo($"Skia Drawing Manager initialized successfully, backendType:{backendType}.");
            initTaskSource.SetResult();
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (currentDPI.DpiScaleX != e.NewDpi.DpiScaleX || currentDPI.DpiScaleY != e.NewDpi.DpiScaleY)
                Log.LogInfo($"currentDPI changed: {currentDPI.DpiScaleX},{currentDPI.DpiScaleY} -> {e.NewDpi.DpiScaleX},{e.NewDpi.DpiScaleY}");
            currentDPI = e.NewDpi;
        }

        /// <inheritdoc />
        public Task WaitForInitializationIsDone(CancellationToken cancellation)
        {
            return initTaskSource.Task;
        }

        /// <inheritdoc />
        public Task InitializeRenderControl(FrameworkElement rc, CancellationToken cancellation = default)
        {
            var renderControl = CheckRenderControl(rc);

            if (!initialized)
            {
                initialized = true;

                Log.LogDebug($"Start to invoke {nameof(DefaultSkiaDrawingManagerImpl)}::Initialize()");
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

        private void RefreshBackendType()
        {
            backendType = Enum.TryParse<RenderBackendType>(Properties.ProgramSetting.Default.SkiaRenderBackend, out var bt) ? bt : RenderBackendType.CPU;
        }

        /// <inheritdoc />
        public IImage LoadImageFromStream(Stream stream)
        {
            CheckInitialization();

            return new SkiaImage(SKImage.FromEncodedData(stream));
        }

        /// <inheritdoc />
        public async Task<IRenderContext> GetOrCreateRenderContext(FrameworkElement rc, CancellationToken cancellation = default)
        {
            return await GetOrCreateSkiaRenderContext(rc, cancellation);
        }

        /// <summary>
        /// Gets the concrete Skia render context for the specified render control.
        /// </summary>
        public Task<DefaultSkiaRenderContext> GetOrCreateSkiaRenderContext(FrameworkElement rc, CancellationToken cancellation = default)
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

        /// <inheritdoc />
        public FrameworkElement CreateRenderControl()
        {
            RefreshBackendType();

            switch (backendType)
            {
                case RenderBackendType.OpenGL:
                    return new SkiaRenderControl_OpenGL();
                case RenderBackendType.DirectX:
                    return new SkiaRenderControl_DirectX();
                case RenderBackendType.DirectX12:
                    return new SkiaRenderControl_D3D9On12();
                case RenderBackendType.CPU:
                default:
                    return new SkiaRenderControl_CPU();
            }
        }

        /// <inheritdoc />
        public IDrawCommandListBuilder CreateDrawCommandListBuilder()
        {
            return new DrawCommandListBuilder();
        }

        /// <inheritdoc />
        public void PostDrawCommandList(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (drawCommandList is null)
                throw new ArgumentNullException(nameof(drawCommandList));

            DrawCommandListSlot? oldBack = null;

            var slot = GetOrCreateSlot(context);
            oldBack = slot.Back;
            slot.Back = new DrawCommandListSlot(drawCommandList, autoDispose);

            ReleaseSlot(oldBack);
        }

        /// <inheritdoc />
        public bool SwapDrawCommandList(IRenderContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            DrawCommandListSlot? oldFront = null;
            var swapped = false;

            var slot = GetOrCreateSlot(context);
            if (slot.Back is not { } back)
                return false;

            oldFront = slot.Front;
            slot.Front = back;
            slot.Back = null;
            swapped = true;

            ReleaseSlot(oldFront);
            return swapped;
        }

        /// <inheritdoc />
        public void PresentDrawCommandList(IRenderContext context)
        {
            if (context is not ISkiaRenderContext skiaRenderContext)
                throw new Exception("renderContext must be ISkiaRenderContext object.");

            DrawCommandListSlot? front = null;

            if (!contextSlots.TryGetValue(context, out var slot))
                return;

            front = slot.Front;
            slot.Front = null;

            if (front is not { } value)
                return;

            var drawCommandList = value.DrawCommandList;
            try
            {
                if (!drawCommandList.TryBeginPresent())
                    return;

                try
                {
                    PresentCommands(drawCommandList);
                }
                finally
                {
                    drawCommandList.EndPresent();
                }
            }
            finally
            {
                if (value.AutoDispose)
                    drawCommandList.Dispose();
            }
        }

        private ContextSlot GetOrCreateSlot(IRenderContext context)
        {
            if (!contextSlots.TryGetValue(context, out var slot))
                slot = contextSlots[context] = new ContextSlot();

            return slot;
        }

        private static void ReleaseSlot(DrawCommandListSlot? slot)
        {
            if (slot is { AutoDispose: true } value)
                value.DrawCommandList.Dispose();
        }

        private static void PresentCommands(DrawCommandList drawCommandList)
        {
            foreach (var command in drawCommandList.Commands)
            {
                switch (command)
                {
                    case SetCurrentModelMatrixCommand:
                    case SetCurrentViewMatrixCommand:
                    case SetCurrentProjectionMatrixCommand:
                    case PushModelMatrixCommand:
                    case PushViewMatrixCommand:
                    case PushProjectionMatrixCommand:
                    case PopModelMatrixCommand:
                    case PopViewMatrixCommand:
                    case PopProjectionMatrixCommand:
                    case DrawLinesCommand:
                    case DrawSimpleLinesCommand:
                    case DrawTextureCommand:
                    case DrawBatchTextureCommand:
                    case DrawHighlightBatchTextureCommand:
                    case DrawCirclesCommand:
                    case DrawPolygonCommand:
                    case DrawStringCommand:
                    case DrawBeamCommand:
                        // TODO: Execute backend-specific draw command rendering here.
                        break;
                    default:
                        // TODO: Decide how custom draw commands should be dispatched.
                        break;
                }
            }
        }
    }
}
