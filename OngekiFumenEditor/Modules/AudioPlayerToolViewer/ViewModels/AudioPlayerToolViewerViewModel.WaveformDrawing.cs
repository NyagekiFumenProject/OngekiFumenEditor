using Caliburn.Micro;
using FontStashSharp.RichText;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Audio.Rhythm;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
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
        private IRhythmAnalyzer rhythmAnalyzer;
        private CancellationTokenSource loadWaveformTask;
        private CancellationTokenSource resampleTaskCancelTokenSource;
        private Task waveformTask = Task.CompletedTask;
        private Task resampleTask = Task.CompletedTask;
        private int waveformGeneration;
        private FrameworkElement waveformRenderControl;
        private bool renderControlEventsAttached;
        private TaskCompletionSource initTask = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private PeakPointCollection rawPeakData;
        private PeakPointCollection usingPeakData;
        private RhythmEnvelope rhythmCurve;

        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan AudioTotalDuration => AudioPlayer?.Duration ?? default;

        /// <summary>整首歌的节奏强度曲线，供绘制层读取；尚未分析完成时为 null。</summary>
        public RhythmEnvelope RhythmCurve => rhythmCurve;

        private IWaveformDrawing waveformDrawing;
        public IWaveformDrawing WaveformDrawing
        {
            get => waveformDrawing;
            set
            {
                if (waveformDrawing?.Options is { } oldOptions)
                    oldOptions.PropertyChanged -= OnWaveformDrawingOptionPropertyChanged;

                Set(ref waveformDrawing, value);

                if (waveformDrawing?.Options is { } newOptions)
                    newOptions.PropertyChanged += OnWaveformDrawingOptionPropertyChanged;
            }
        }

        /// <summary>波形绘制实现自身选项变化时使图块失效（几何/可见性可能已变）。</summary>
        private void OnWaveformDrawingOptionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not null and not "" and not nameof(DefaultWaveformOption.ShowWaveform))
                return;

            InvalidateWaveformBlocks();
        }

        /// <summary>
        /// 颜色/线宽/预渲染开关等用户设置变化时使图块失效：
        /// 关闭预渲染时清空缓存即释放显存，下一帧自动回退到实时绘制。
        /// </summary>
        private void OnWaveformSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.DefaultWaveformSettings.WaveformBackgroundColor):
                case nameof(Properties.DefaultWaveformSettings.WaveformFillColor):
                case nameof(Properties.DefaultWaveformSettings.WaveformCursorColor):
                case nameof(Properties.DefaultWaveformSettings.WaveformBeatLineColor):
                case nameof(Properties.DefaultWaveformSettings.WaveformObjectPlaceLineColor):
                case nameof(Properties.DefaultWaveformSettings.WaveformHoldLineColor):
                case nameof(Properties.AudioPlayerToolViewerSetting.WaveformBodyLineWidth):
                case nameof(Properties.AudioPlayerToolViewerSetting.WaveformHoldLineWidth):
                case nameof(Properties.AudioPlayerToolViewerSetting.WaveformMarkerLineWidth):
                case nameof(Properties.AudioPlayerToolViewerSetting.EnableWaveformBlockPrerender):
                    InvalidateWaveformBlocks();
                    break;
            }
        }

        private void AttachWaveformSettingsEvents()
        {
            Properties.DefaultWaveformSettings.Default.PropertyChanged += OnWaveformSettingsPropertyChanged;
            Properties.AudioPlayerToolViewerSetting.Default.PropertyChanged += OnWaveformSettingsPropertyChanged;
        }

        private void DetachWaveformSettingsEvents()
        {
            Properties.DefaultWaveformSettings.Default.PropertyChanged -= OnWaveformSettingsPropertyChanged;
            Properties.AudioPlayerToolViewerSetting.Default.PropertyChanged -= OnWaveformSettingsPropertyChanged;
        }

        private int resampleSize = Properties.AudioPlayerToolViewerSetting.Default.ResampleSize;
        public int ResampleSize
        {
            get => resampleSize;
            set
            {
                Set(ref resampleSize, value);
                StartResamplePeak();
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
                if (Set(ref waveformVecticalScale, value))
                    InvalidateWaveformBlocks();
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
                if (Set(ref durationMsPerPixel, value))
                    InvalidateWaveformBlocks();
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
            set
            {
                if (Set(ref isShowWaveform, value))
                    InvalidateWaveformBlocks();
            }
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

        private async Task PrepareRenderLoopAsync(FrameworkElement renderControl, IRenderManagerImpl impl)
        {
            try
            {
                await PrepareRenderLoopCoreAsync(renderControl, impl);
            }
            catch (OperationCanceledException)
            {
                DisposeWaveformRenderLoop();
                initTask.TrySetCanceled();
                throw;
            }
            catch (Exception e)
            {
                DisposeWaveformRenderLoop();
                initTask.TrySetException(e);
                throw;
            }
        }

        private async Task PrepareRenderLoopCoreAsync(FrameworkElement renderControl, IRenderManagerImpl impl)
        {
            Log.LogDebug($"ready.");

            var cancellationToken = LifetimeCancellation;
            await impl.WaitForInitializationIsDone(cancellationToken);
            var context = await impl.GetOrCreateRenderContext(renderControl, cancellationToken);
            if (IsDisposed || cancellationToken.IsCancellationRequested)
            {
                context.StopRendering();
                impl.RemoveRenderContext(context);
                return;
            }
            RenderContext = context;

            if (IsDisposed || cancellationToken.IsCancellationRequested)
            {
                RenderContext = null;
                context.StopRendering();
                impl.RemoveRenderContext(context);
                return;
            }

            AttachWaveformSettingsEvents();

            samplePeak = IoC.Get<ISamplePeak>();
            rhythmAnalyzer = IoC.Get<IRhythmAnalyzer>();
            WaveformDrawing = IoC.Get<IWaveformDrawing>();
            WaveformDrawing.Initialize(impl);
            initTask.TrySetResult();

            viewWidth = (float)renderControl.ActualWidth;
            viewHeight = (float)renderControl.ActualHeight;
            UpdateRenderScale(renderControl);

            UpdateActualRenderInterval();
        }

        private void PrepareWaveform(IAudioPlayer player)
        {
            CleanWaveform();
            if (IsDisposed)
                return;

            loadWaveformTask = new CancellationTokenSource();
            var cancelToken = loadWaveformTask.Token;
            var generation = Volatile.Read(ref waveformGeneration);

            waveformTask = Task.Run(() => OnPrepareWaveformAsync(player, generation, cancelToken), cancelToken);
            _ = ObserveOperationAsync(waveformTask, "waveform preparation");
        }

        private async Task OnPrepareWaveformAsync(IAudioPlayer player, int generation, CancellationToken cancelToken)
        {
            await initTask.Task.WaitAsync(cancelToken).ConfigureAwait(false);
            if (!IsWaveformCurrent(player, generation, cancelToken) || samplePeak is null)
                return;
            var sampleData = await player.GetSamplesAsync();
            if (!IsWaveformCurrent(player, generation, cancelToken))
                return;
            rawPeakData = sampleData is not null ? samplePeak.GetPeakValues(sampleData) : null;
            await ResamplePeakAsync(generation, cancelToken).ConfigureAwait(false);

            // 节奏分析要对整首歌做一次 STFT，成本和波峰同量级，所以同样放在这条后台任务里做；
            // 换歌/关闭面板时通过取消令牌放弃，结果不会发布出来。
            if (sampleData is not null && rhythmAnalyzer is not null)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var curve = rhythmAnalyzer.Analyze(sampleData, cancelToken);
                stopwatch.Stop();
                if (!IsWaveformCurrent(player, generation, cancelToken))
                    return;

                rhythmCurve = curve;
                Log.LogInfo($"[Rhythm] 分析完成 {stopwatch.ElapsedMilliseconds}ms, 曲线帧数={curve?.FrameCount.ToString() ?? "null"}");
            }
        }

        private bool IsWaveformCurrent(IAudioPlayer player, int generation, CancellationToken cancellationToken)
            => !IsDisposed && !cancellationToken.IsCancellationRequested && generation == Volatile.Read(ref waveformGeneration) && ReferenceEquals(player, AudioPlayer);

        private void StartResamplePeak()
        {
            var generation = Volatile.Read(ref waveformGeneration);
            resampleTask = ResamplePeakAsync(generation, LifetimeCancellation);
            _ = ObserveOperationAsync(resampleTask, "waveform resampling");
        }

        private async Task ResamplePeakAsync(int generation, CancellationToken parentToken)
        {
            resampleTaskCancelTokenSource?.Cancel();
            var tokenSource = new CancellationTokenSource();
            resampleTaskCancelTokenSource = tokenSource;
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentToken, tokenSource.Token);
            var cancellationToken = linkedTokenSource.Token;

            if (ResampleSize == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != Volatile.Read(ref waveformGeneration) || IsDisposed)
                    return;
                usingPeakData = rawPeakData;
                InvalidateWaveformBlocks();
            }
            else
            {
                var sourcePeakData = rawPeakData;
                var newPeakData = sourcePeakData is null ? default : await sourcePeakData.GenerateSimplfiedAsync(ResampleSize, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested || generation != Volatile.Read(ref waveformGeneration) || IsDisposed)
                    return;
                usingPeakData = newPeakData;
                InvalidateWaveformBlocks();
            }
        }

        private void CleanWaveform()
        {
            loadWaveformTask?.Cancel();
            loadWaveformTask = null;
            resampleTaskCancelTokenSource?.Cancel();
            resampleTaskCancelTokenSource = null;
            Interlocked.Increment(ref waveformGeneration);
            rawPeakData = null;
            usingPeakData = null;
            rhythmCurve = null;
            InvalidateWaveformBlocks();
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
                FlushPendingBlockDisposal();

                using var builder = renderImpl.CreateDrawCommandListBuilder();
                builder.SetCleanColor(WaveformViewCleanColor);
                builder.SetViewport(viewWidth, viewHeight, renderScaleX, renderScaleY);
                builder.SetCurrentViewMatrix(CurrentDrawingTargetContext.ViewMatrix);
                builder.SetCurrentProjectionMatrix(CurrentDrawingTargetContext.ProjectionMatrix);

                if (Editor is not null && IsShowWaveform && usingPeakData is not null)
                {
                    var peakData = usingPeakData;
                    var fromTime = CurrentTime - TimeSpan.FromMilliseconds(CurrentTimeXOffset * DurationMsPerPixel);
                    var toTime = fromTime + TimeSpan.FromMilliseconds(viewWidth * DurationMsPerPixel);
                    var (iFrom, iTo) = GetWaveformBlockRange(fromTime, toTime, WaveformBlockSpanMs);

                    waveformVisibleBlockFrom = iFrom;
                    waveformVisibleBlockTo = iTo;

                    if (EnableWaveformBlockPrerender && IsWaveformPolylineVisible && TryGetVisibleWaveformBlocks(iFrom, iTo, out var blocks))
                    {
                        // 图块就绪：只贴回缓存纹理 + 补绘边界标记，波形本体不再逐帧重建。
                        DrawWaveformBlocks(builder, blocks, iFrom, fromTime, toTime);
                        DrawWaveformEdgeMarkers(builder, peakData, fromTime, toTime);
                        WaveformDrawing.Draw(this, peakData, builder, drawWaveform: false);
                    }
                    else
                    {
                        // 图块未就绪：整帧回退到实时绘制。
                        WaveformDrawing.Draw(this, peakData, builder);
                    }

                    ScheduleWaveformBlockRender(iFrom, iTo);
                }

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

        public void OnRenderControlHostLoaded(ActionExecutionContext executionContext)
        {
            _ = ObserveOperationAsync(OnRenderControlHostLoadedAsync(executionContext), "render control initialization");
        }

        private async Task OnRenderControlHostLoadedAsync(ActionExecutionContext executionContext)
        {
            if (IsDisposed)
                return;

            if (executionContext.Source is not ContentControl contentControl)
                throw new InvalidOperationException($"Waveform render control host source must be ContentControl, actual={executionContext.Source?.GetType().FullName}");
            //check render control is created and shown.
            if (renderImpl != null)
                return;

            renderImpl = IoC.Get<IRenderManager>().GetCurrentRenderManagerImpl();
            var renderControl = renderImpl.CreateRenderControl();
            await renderImpl.InitializeRenderControl(renderControl, LifetimeCancellation);
            if (IsDisposed || LifetimeCancellation.IsCancellationRequested)
            {
                renderImpl = null;
                return;
            }

            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is created");

            renderControl.Loaded += RenderControl_Loaded;
            renderControl.Unloaded += RenderControl_UnLoaded;
            renderControl.SizeChanged += RenderControl_SizeChanged;
            waveformRenderControl = renderControl;
            renderControlEventsAttached = true;

            contentControl.Content = renderControl;

            await PrepareRenderLoopAsync(renderControl, renderImpl);
        }

        private void RenderControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsDisposed)
                return;

            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"renderControl new size: {e.NewSize} , renderControl.RenderSize = {renderControl.RenderSize}");

            var prevHeight = viewHeight;
            var prevScaleX = renderScaleX;
            var prevScaleY = renderScaleY;

            viewWidth = (float)e.NewSize.Width;
            viewHeight = (float)e.NewSize.Height;
            UpdateRenderScale(renderControl);

            // 只与高度 / 缩放有关的输入才影响图块内容；宽度变化只会平移可见范围。
            if (viewHeight != prevHeight || renderScaleX != prevScaleX || renderScaleY != prevScaleY)
                InvalidateWaveformBlocks();
        }

        private void UpdateRenderScale(FrameworkElement renderControl)
        {
            var dpi = VisualTreeHelper.GetDpi(renderControl);
            renderScaleX = (float)dpi.DpiScaleX;
            renderScaleY = (float)dpi.DpiScaleY;
        }

        private void RenderControl_UnLoaded(object sender, RoutedEventArgs e)
        {
            if (IsDisposed)
                return;

            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is unloaded");

            waveformRenderActive = false;
            InvalidateWaveformBlocks();

            var context = RenderContext;
            if (context is null)
                return;

            context.OnRender -= Render;
            context.Name = default;
            context.StopRendering();
        }

        private void DisposeWaveformRenderLoop()
        {
            waveformRenderActive = false;

            var renderControl = waveformRenderControl;
            if (renderControl is not null && renderControlEventsAttached)
            {
                renderControl.Loaded -= RenderControl_Loaded;
                renderControl.Unloaded -= RenderControl_UnLoaded;
                renderControl.SizeChanged -= RenderControl_SizeChanged;
            }

            waveformRenderControl = null;
            renderControlEventsAttached = false;

            var context = RenderContext;
            RenderContext = null;
            if (context is not null)
            {
                context.OnRender -= Render;
                context.Name = default;
                context.StopRendering();
                renderImpl?.RemoveRenderContext(context);
            }

            initTask.TrySetCanceled();
        }

        private void RenderControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = ObserveOperationAsync(RenderControlLoadedAsync(sender), "render control reload");
        }

        private async Task RenderControlLoadedAsync(object sender)
        {
            if (IsDisposed)
                return;

            var renderControl = sender as FrameworkElement;
            Log.LogDebug($"RenderControl({renderControl.GetHashCode()}) is loaded");

            waveformRenderActive = true;

            var context = await renderImpl.GetOrCreateRenderContext(renderControl, LifetimeCancellation);
            if (IsDisposed || LifetimeCancellation.IsCancellationRequested)
            {
                context.OnRender -= Render;
                context.StopRendering();
                renderImpl.RemoveRenderContext(context);
                return;
            }

            RenderContext = context;
            RenderContext.Name = "AudioPlayerToolViewerViewModel.WaveRender";
            UpdateActualRenderInterval();
            RenderContext.OnRender += Render;
            RenderContext.StartRendering();
        }
    }
}
