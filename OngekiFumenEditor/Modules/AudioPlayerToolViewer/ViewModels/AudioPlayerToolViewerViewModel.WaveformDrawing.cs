using Caliburn.Micro;
using FontStashSharp.RichText;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    public partial class AudioPlayerToolViewerViewModel : IWaveformDrawingContext
    {
        private float viewWidth;
        private float viewHeight;
        private float renderScaleX = 1;
        private float renderScaleY = 1;
        private ISamplePeak samplePeak;
        private CancellationTokenSource loadWaveformTask;
        private CancellationTokenSource resampleTaskCancelTokenSource;
        private TaskCompletionSource initTask = new TaskCompletionSource();

        private PeakPointCollection rawPeakData;
        private PeakPointCollection usingPeakData;

        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan AudioTotalDuration => AudioPlayer?.Duration ?? default;

        private IWaveformDrawing waveformDrawing;
        public IWaveformDrawing WaveformDrawing
        {
            get => waveformDrawing;
            set
            {
                Set(ref waveformDrawing, value);
            }
        }

        private int resampleSize = Properties.AudioPlayerToolViewerSetting.Default.ResampleSize;
        public int ResampleSize
        {
            get => resampleSize;
            set
            {
                Set(ref resampleSize, value);
                ResamplePeak();
                Properties.AudioPlayerToolViewerSetting.Default.ResampleSize = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float waveformVecticalScale = Properties.AudioPlayerToolViewerSetting.Default.WaveformVecticalScale;
        public float WaveformVecticalScale
        {
            get => waveformVecticalScale;
            set
            {
                Set(ref waveformVecticalScale, value);
                Properties.AudioPlayerToolViewerSetting.Default.WaveformVecticalScale = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float durationMsPerPixel = Properties.AudioPlayerToolViewerSetting.Default.DurationMsPerPixel;
        public float DurationMsPerPixel
        {
            get => durationMsPerPixel;
            set
            {
                Set(ref durationMsPerPixel, value);
                Properties.AudioPlayerToolViewerSetting.Default.DurationMsPerPixel = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float currentTimeXOffset = Properties.AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset;
        public float CurrentTimeXOffset
        {
            get => currentTimeXOffset;
            set
            {
                Set(ref currentTimeXOffset, value);
                Properties.AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private bool isShowWaveform = true;
        public bool IsShowWaveform
        {
            get => isShowWaveform && Properties.AudioPlayerToolViewerSetting.Default.EnableWaveformDisplay;
            set => Set(ref isShowWaveform, value);
        }

        private int limitFPS = Properties.AudioPlayerToolViewerSetting.Default.LimitFPS;
        private IRenderManagerImpl renderImpl;

        public int LimitFPS
        {
            get => limitFPS;
            set
            {
                Set(ref limitFPS, value);
                Properties.AudioPlayerToolViewerSetting.Default.LimitFPS = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
                UpdateActualRenderInterval();
            }
        }

        public FumenVisualEditorViewModel EditorViewModel => Editor;

        public DrawingTargetContext CurrentDrawingTargetContext { get; private set; } = new();

        public IRenderContext RenderContext { get; private set; }

        private void UpdateActualRenderInterval()
        {
            if (RenderContext is null)
                return;

            RenderContext.LimitFPS = LimitFPS <= 0 ? -1 : LimitFPS;
        }

        public async void PrepareRenderLoop(FrameworkElement renderControl, IRenderManagerImpl impl)
        {
            Log.LogDebug($"ready.");

            await impl.WaitForInitializationIsDone();
            RenderContext = await impl.GetOrCreateRenderContext(renderControl);

            samplePeak = IoC.Get<ISamplePeak>();
            WaveformDrawing = IoC.Get<IWaveformDrawing>();
            WaveformDrawing.Initialize(impl);
            initTask.SetResult();

            viewWidth = (float)renderControl.ActualWidth;
            viewHeight = (float)renderControl.ActualHeight;
            UpdateRenderScale(renderControl);

            UpdateActualRenderInterval();
        }

        private void PrepareWaveform(IAudioPlayer player)
        {
            CleanWaveform();
            loadWaveformTask = new CancellationTokenSource();
            var cancelToken = loadWaveformTask.Token;

            Task.Run(() => OnPrepareWaveform(player, cancelToken), cancelToken);
        }

        private async void OnPrepareWaveform(IAudioPlayer player, CancellationToken cancelToken)
        {
            await initTask.Task;
            if (cancelToken.IsCancellationRequested || player is null || samplePeak is null)
                return;
            var sampleData = await player.GetSamplesAsync();
            rawPeakData = sampleData is not null ? samplePeak.GetPeakValues(sampleData) : null;
            ResamplePeak();
        }

        private async void ResamplePeak()
        {
            resampleTaskCancelTokenSource?.Cancel();
            var tokenSource = new CancellationTokenSource();
            resampleTaskCancelTokenSource = tokenSource;

            if (ResampleSize == 0)
                usingPeakData = rawPeakData;
            else
            {
                var newPeakData = rawPeakData is null ? default : await rawPeakData?.GenerateSimplfiedAsync(ResampleSize, tokenSource.Token);
                if (!tokenSource.IsCancellationRequested)
                    usingPeakData = newPeakData;
            }
        }

        private void CleanWaveform()
        {
            loadWaveformTask?.Cancel();
            loadWaveformTask = null;
            rawPeakData = null;
            usingPeakData = null;
        }

        public void OnWaveformOptionReset()
        {
            WaveformDrawing?.Options?.Reset();
        }

        public void OnWaveformOptionSave()
        {
            WaveformDrawing?.Options?.Save();
        }

        public void Render(IRenderContext context, TimeSpan ts)
        {
            if (RenderContext is null || renderImpl is null)
                return;

            context.PerfomenceMonitor.OnBeforeRender();

            try
            {
                UpdateDrawingContext();

                using var builder = renderImpl.CreateDrawCommandListBuilder();
                builder.SetCleanColor(new(16 / 255f, 16 / 255f, 16 / 255f, 1f));
                builder.SetViewport(viewWidth, viewHeight, renderScaleX, renderScaleY);
                builder.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
                builder.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

                if (Editor is not null && IsShowWaveform && usingPeakData is not null)
                    WaveformDrawing.Draw(this, usingPeakData, builder);

                var drawCommandList = builder.GetDrawCommandList();
                var ownsDrawCommandList = true;

                try
                {
                    RenderContext.PostDrawCommandList(drawCommandList, autoDispose: true);
                    ownsDrawCommandList = false;
                }
                catch
                {
                    if (ownsDrawCommandList)
                        drawCommandList.Dispose();
                    throw;
                }
            }
            finally
            {
                context.PerfomenceMonitor.OnAfterRender();
            }
        }

        private void UpdateDrawingContext()
        {
            var projectionMatrix =
                Matrix4x4.CreateOrthographic(viewWidth, viewHeight, -1, 1);
            var viewMatrix = Matrix4x4.CreateTranslation(new Vector3(0, 0, 0));

            CurrentDrawingTargetContext.ViewMatrix = viewMatrix;
            CurrentDrawingTargetContext.ProjectionMatrix = projectionMatrix;

            CurrentDrawingTargetContext.ViewRelativeRect = new VisibleRect(new(0 + viewWidth, 0), new(0, 0 + viewHeight));
            CurrentDrawingTargetContext.ViewWidth = viewWidth;
            CurrentDrawingTargetContext.ViewHeight = viewHeight;
            CurrentDrawingTargetContext.RenderScaleX = renderScaleX;
            CurrentDrawingTargetContext.RenderScaleY = renderScaleY;

            if (AudioPlayer?.IsPlaying ?? false)
                CurrentTime = AudioPlayer.CurrentTime;
            else
            {
                var tGrid = Editor?.GetCurrentTGrid();
                if (tGrid is null)
                    return;
                var editorAudioTime = Editor.ConvertTGridToAudioTime(tGrid);
                CurrentTime = editorAudioTime;
            }
        }

        public async void OnRenderControlHostLoaded(ActionExecutionContext executionContext)
        {
            if (executionContext.Source is not ContentControl contentControl)
                throw new InvalidOperationException($"Waveform render control host source must be ContentControl, actual={executionContext.Source?.GetType().FullName}");
            //check render control is created and shown.
            if (renderImpl != null)
                return;

            renderImpl = IoC.Get<IRenderManager>().GetCurrentRenderManagerImpl();
            var renderControl = renderImpl.CreateRenderControl();
            await renderImpl.InitializeRenderControl(renderControl);

            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is created");

            renderControl.Loaded += RenderControl_Loaded;
            renderControl.Unloaded += RenderControl_UnLoaded;
            renderControl.SizeChanged += RenderControl_SizeChanged;

            contentControl.Content = renderControl;

            PrepareRenderLoop(renderControl, renderImpl);
        }

        private void RenderControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"renderControl new size: {e.NewSize} , renderControl.RenderSize = {renderControl.RenderSize}");

            viewWidth = (float)e.NewSize.Width;
            viewHeight = (float)e.NewSize.Height;
            UpdateRenderScale(renderControl);
        }

        private void UpdateRenderScale(FrameworkElement renderControl)
        {
            var dpi = VisualTreeHelper.GetDpi(renderControl);
            renderScaleX = (float)dpi.DpiScaleX;
            renderScaleY = (float)dpi.DpiScaleY;
        }

        private void RenderControl_UnLoaded(object sender, RoutedEventArgs e)
        {
            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is unloaded");

            var context = RenderContext;
            if (context is null)
                return;

            context.OnRender -= Render;
            context.Name = default;
            context.StopRendering();
        }

        private async void RenderControl_Loaded(object sender, RoutedEventArgs e)
        {
            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is loaded");

            RenderContext = await renderImpl.GetOrCreateRenderContext(renderControl);
            RenderContext.Name = "AudioPlayerToolViewerViewModel.WaveRender";
            UpdateActualRenderInterval();
            RenderContext.OnRender += Render;
            RenderContext.StartRendering();
        }
    }
}
