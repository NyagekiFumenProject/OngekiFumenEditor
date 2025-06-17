using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static OngekiFumenEditor.Kernel.Graphics.IDrawingContext;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    public partial class AudioPlayerToolViewerViewModel : IWaveformDrawingContext
    {
        private float viewWidth;
        private float viewHeight;
        private int renderViewWidth;
        private int renderViewHeight;
        private IPerfomenceMonitor performenceMonitor;
        private Stopwatch sw;
        private ISamplePeak samplePeak;
        private CancellationTokenSource loadWaveformTask;
        private CancellationTokenSource resampleTaskCancelTokenSource;
        private TaskCompletionSource initTask = new TaskCompletionSource();

        private PeakPointCollection rawPeakData;
        private PeakPointCollection usingPeakData;

        public IPerfomenceMonitor PerfomenceMonitor => performenceMonitor;

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

        private float actualRenderInterval = float.MaxValue;
        private int limitFPS = Properties.AudioPlayerToolViewerSetting.Default.LimitFPS;
        private IRenderContext renderContext;

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

        private void UpdateActualRenderInterval()
        {
            actualRenderInterval = LimitFPS switch
            {
                <= 0 => float.MaxValue,
                _ => 1000.0F / LimitFPS
            };
        }

        public async void PrepareRenderLoop(FrameworkElement renderControl)
        {
            Log.LogDebug($"ready.");
            await IoC.Get<IRenderManager>().WaitForInitializationIsDone();
            renderContext = await IoC.Get<IRenderManager>().GetRenderContext(renderControl);

            InitRender();
            var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
            var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

            viewWidth = (float)renderControl.ActualWidth;
            viewHeight = (float)renderControl.ActualHeight;
            renderViewWidth = (int)(renderControl.ActualWidth * dpiX);
            renderViewHeight = (int)(renderControl.ActualHeight * dpiY);

            //暂时没有需要显示检测的必要?
            //performenceMonitor = IoC.Get<IPerfomenceMonitor>();
            performenceMonitor = new DummyPerformenceMonitor();

            sw = new Stopwatch();
            sw.Start();
        }

        private void InitRender()
        {
            samplePeak = IoC.Get<ISamplePeak>();
            WaveformDrawing = IoC.Get<IWaveformDrawing>();
            initTask.SetResult();
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

        public void Render(TimeSpan ts)
        {
            if (renderContext is null)
                return;
            //limit
            if (LimitFPS > 0)
            {
                if (sw.ElapsedMilliseconds < actualRenderInterval)
                    return;
                sw.Restart();
            }

            renderContext.CleanRender(this, new(16 / 255f, 16 / 255f, 16 / 255f, 1f));

            if (Editor is null || !IsShowWaveform)
                return;

            PerfomenceMonitor.PostUIRenderTime(ts);
            PerfomenceMonitor.OnBeforeRender();

            renderContext.BeforeRender(this);

            UpdateDrawingContext();

            if (usingPeakData is not null)
                WaveformDrawing.Draw(this, usingPeakData);

            PerfomenceMonitor.OnAfterRender();
            renderContext.AfterRender(this);
        }

        private void UpdateDrawingContext()
        {
            var projectionMatrix =
                Matrix4.CreateOrthographic(viewWidth, viewHeight, -1, 1);
            var viewMatrix =
                Matrix4.CreateTranslation(new Vector3(0, 0, 0));
            var vp = viewMatrix * projectionMatrix;

            CurrentDrawingTargetContext.ViewMatrix = viewMatrix;
            CurrentDrawingTargetContext.ProjectionMatrix = projectionMatrix;
            CurrentDrawingTargetContext.ViewProjectionMatrix = vp;

            CurrentDrawingTargetContext.Rect = new VisibleRect(new(0 + viewWidth, 0), new(0, 0 + viewHeight));
            CurrentDrawingTargetContext.ViewWidth = viewWidth;
            CurrentDrawingTargetContext.ViewHeight = viewHeight;

            if (AudioPlayer?.IsPlaying ?? false)
                CurrentTime = AudioPlayer.CurrentTime;
            else
            {
                var tGrid = Editor?.GetCurrentTGrid();
                if (tGrid is null)
                    return;
                var editorAudioTime = TGridCalculator.ConvertTGridToAudioTime(tGrid, Editor);
                CurrentTime = editorAudioTime;
            }
        }

        public async void OnRenderControlHostLoaded(ActionExecutionContext executionContext)
        {
            if (executionContext.Source is not ContentControl contentControl)
                return; //todo throw exception
            //check render control is created and shown.
            if (contentControl.Content != null)
                return;

            var renderControl = IoC.Get<IRenderManager>().CreateRenderControl();
            await IoC.Get<IRenderManager>().InitializeRenderControl(renderControl);

            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is created");

            renderControl.Loaded += RenderControl_Loaded;
            renderControl.Unloaded += RenderControl_UnLoaded;
            renderControl.SizeChanged += RenderControl_SizeChanged;

            contentControl.Content = renderControl;

            PrepareRenderLoop(renderControl);
        }

        private void RenderControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"renderControl new size: {e.NewSize} , renderControl.RenderSize = {renderControl.RenderSize}");

            var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
            var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

            viewWidth = (float)e.NewSize.Width;
            viewHeight = (float)e.NewSize.Height;
            renderViewWidth = (int)(e.NewSize.Width * dpiX);
            renderViewHeight = (int)(e.NewSize.Height * dpiY);
        }

        private async void RenderControl_UnLoaded(object sender, RoutedEventArgs e)
        {
            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is unloaded");

            renderContext = await IoC.Get<IRenderManager>().GetRenderContext(renderControl);
            renderContext.OnRender -= Render;
            renderContext.StopRendering();
        }

        private async void RenderControl_Loaded(object sender, RoutedEventArgs e)
        {
            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is loaded");

            renderContext = await IoC.Get<IRenderManager>().GetRenderContext(renderControl);
            renderContext.OnRender += Render;
            renderContext.StartRendering();
        }
    }
}
