using Caliburn.Micro;
using ControlzEx.Standard;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        public void OnRenderSizeChanged(FrameworkElement glView, SizeChangedEventArgs sizeArg)
        {
            Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");
            var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
            var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

            viewWidth = (float)sizeArg.NewSize.Width;
            viewHeight = (float)sizeArg.NewSize.Height;
            renderViewWidth = (int)(sizeArg.NewSize.Width * dpiX);
            renderViewHeight = (int)(sizeArg.NewSize.Height * dpiY);
        }

        public async void PrepareRenderLoop(FrameworkElement renderControl)
        {
            Log.LogDebug($"ready.");
            await IoC.Get<IDrawingManager>().WaitForInitializationIsDone();

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
            //limit
            if (LimitFPS > 0)
            {
                if (sw.ElapsedMilliseconds < actualRenderInterval)
                    return;
                sw.Restart();
            }

            GL.ClearColor(16 / 255f, 16 / 255f, 16 / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (Editor is null || !IsShowWaveform)
                return;

            PerfomenceMonitor.PostUIRenderTime(ts);
            PerfomenceMonitor.OnBeforeRender();

            GL.Viewport(0, 0, renderViewWidth, renderViewHeight);

            UpdateDrawingContext();

            if (usingPeakData is not null)
                WaveformDrawing.Draw(this, usingPeakData);

            PerfomenceMonitor.OnAfterRender();
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
    }
}
