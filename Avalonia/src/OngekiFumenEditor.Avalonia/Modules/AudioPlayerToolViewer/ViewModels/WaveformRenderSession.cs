using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OpenTK.Mathematics;
using NumericsVector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;

internal readonly record struct WaveformRenderState(
    IAudioPlayer AudioPlayer,
    FumenVisualEditorViewModel Editor,
    bool IsWaveformVisible,
    int ResampleSize,
    float WaveformVerticalScale,
    float DurationMsPerPixel,
    float CurrentTimeXOffset,
    int LimitFramesPerSecond);

internal sealed class WaveformRenderSession : IWaveformDrawingContext, IDisposable
{
    private static readonly NumericsVector4 BackgroundColor = new(16 / 255f, 16 / 255f, 16 / 255f, 1);

    private readonly IRenderManagerImpl renderManager;
    private readonly ISamplePeak samplePeak;
    private readonly IWaveformDrawing waveformDrawing;
    private readonly Func<WaveformRenderState> stateProvider;
    private readonly WaveformFrameLimiter frameLimiter = new();
    private readonly IPerfomenceMonitor performanceMonitor = new DummyPerformenceMonitor();

    private ContentControl host;
    private Control renderControl;
    private CancellationTokenSource lifetimeCancellationSource;
    private CancellationToken lifetimeCancellationToken = new(canceled: true);
    private CancellationTokenSource waveformCancellationSource;
    private CancellationTokenSource resampleCancellationSource;
    private PeakPointCollection rawPeakData;
    private PeakPointCollection usingPeakData;
    private WaveformRenderState currentState;
    private float viewWidth;
    private float viewHeight;
    private int attachmentVersion;
    private int waveformVersion;
    private bool isDisposed;

    public DrawingTargetContext CurrentDrawingTargetContext { get; } = new();
    public IPerfomenceMonitor PerfomenceMonitor => performanceMonitor;
    public IRenderContext RenderContext { get; private set; }
    public TimeSpan CurrentTime { get; private set; }
    public TimeSpan AudioTotalDuration => currentState.AudioPlayer?.Duration ?? default;
    public float DurationMsPerPixel => currentState.DurationMsPerPixel;
    public float CurrentTimeXOffset => currentState.CurrentTimeXOffset;
    public float WaveformVecticalScale => currentState.WaveformVerticalScale;
    public FumenVisualEditorViewModel EditorViewModel => currentState.Editor;

    internal bool IsAttached => RenderContext is not null;
    internal int RenderedFrameCount { get; private set; }
    internal PeakPointCollection RawPeakData => Volatile.Read(ref rawPeakData);
    internal PeakPointCollection PeakData => Volatile.Read(ref usingPeakData);
    internal Task WaveformPreparationTask { get; private set; } = Task.CompletedTask;

    public WaveformRenderSession(
        IRenderManagerImpl renderManager,
        ISamplePeak samplePeak,
        IWaveformDrawing waveformDrawing,
        Func<WaveformRenderState> stateProvider)
    {
        ArgumentNullException.ThrowIfNull(renderManager);
        ArgumentNullException.ThrowIfNull(samplePeak);
        ArgumentNullException.ThrowIfNull(waveformDrawing);
        ArgumentNullException.ThrowIfNull(stateProvider);

        this.renderManager = renderManager;
        this.samplePeak = samplePeak;
        this.waveformDrawing = waveformDrawing;
        this.stateProvider = stateProvider;
    }

    public async Task AttachAsync(ContentControl newHost, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(newHost);

        if (ReferenceEquals(host, newHost) && IsAttached)
            return;

        Detach();
        var version = Interlocked.Increment(ref attachmentVersion);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lifetimeToken = lifetime.Token;
        lifetimeCancellationSource = lifetime;
        lifetimeCancellationToken = lifetimeToken;
        host = newHost;

        var control = renderManager.CreateRenderControl();
        renderControl = control;
        control.SizeChanged += OnRenderControlSizeChanged;
        UpdateViewSize(control.Bounds.Size);
        newHost.Content = control;

        try
        {
            await renderManager.InitializeRenderControl(control, lifetimeToken);
            await renderManager.WaitForInitializationIsDone(lifetimeToken);
            var context = await renderManager.GetRenderContext(control, lifetimeToken);
            lifetimeToken.ThrowIfCancellationRequested();

            if (version != Volatile.Read(ref attachmentVersion))
                throw new OperationCanceledException(lifetimeToken);

            waveformDrawing.Initialize(renderManager);
            RenderContext = context;
            context.OnRender += Render;
            frameLimiter.Reset();
            context.StartRendering();
        }
        catch
        {
            if (version == Volatile.Read(ref attachmentVersion))
                Detach(newHost);
            throw;
        }
    }

    public Task SetAudioPlayerAsync(IAudioPlayer player)
    {
        var version = Interlocked.Increment(ref waveformVersion);
        CancelAndDispose(ref waveformCancellationSource);
        CancelAndDispose(ref resampleCancellationSource);
        Volatile.Write(ref rawPeakData, null);
        Volatile.Write(ref usingPeakData, null);

        if (!IsAttached || player is null)
            return WaveformPreparationTask = Task.CompletedTask;

        var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellationToken);
        waveformCancellationSource = cancellationSource;
        return WaveformPreparationTask = PrepareWaveformAsync(player, version, cancellationSource);
    }

    public Task ResampleAsync()
    {
        var rawData = Volatile.Read(ref rawPeakData);
        if (!IsAttached || rawData is null || lifetimeCancellationSource is null)
            return Task.CompletedTask;

        return ResampleAsync(rawData, Volatile.Read(ref waveformVersion), CancellationToken.None);
    }

    public void Detach(ContentControl expectedHost = null)
    {
        if (expectedHost is not null && !ReferenceEquals(expectedHost, host))
            return;

        Interlocked.Increment(ref attachmentVersion);
        Interlocked.Increment(ref waveformVersion);
        CancelAndDispose(ref waveformCancellationSource);
        CancelAndDispose(ref resampleCancellationSource);
        CancelAndDispose(ref lifetimeCancellationSource);
        lifetimeCancellationToken = new(canceled: true);

        var context = RenderContext;
        RenderContext = null;
        if (context is not null)
        {
            context.OnRender -= Render;
            context.StopRendering();
        }

        var control = renderControl;
        renderControl = null;
        if (control is not null)
        {
            control.SizeChanged -= OnRenderControlSizeChanged;
            renderManager.ReleaseRenderControl(control);
        }

        var oldHost = host;
        host = null;
        if (oldHost is not null && ReferenceEquals(oldHost.Content, control))
            oldHost.Content = null;

        Volatile.Write(ref rawPeakData, null);
        Volatile.Write(ref usingPeakData, null);
        viewWidth = 0;
        viewHeight = 0;
        frameLimiter.Reset();
    }

    public void Render(TimeSpan elapsed)
    {
        var context = RenderContext;
        if (context is null)
            return;

        var state = stateProvider();
        if (!frameLimiter.ShouldRender(elapsed, state.LimitFramesPerSecond))
            return;

        currentState = state;
        UpdateCurrentTime(state);
        UpdateDrawingContext();
        RenderedFrameCount++;

        performanceMonitor.PostUIRenderTime(elapsed);
        performanceMonitor.OnBeforeRender();
        var renderStateSaved = false;
        try
        {
            context.BeforeRender(this);
            renderStateSaved = true;
            context.CleanRender(this, BackgroundColor);
            if (state.IsWaveformVisible)
                waveformDrawing.Draw(this, Volatile.Read(ref usingPeakData));
        }
        finally
        {
            try
            {
                if (renderStateSaved)
                    context.AfterRender(this);
            }
            finally
            {
                performanceMonitor.OnAfterRender();
            }
        }
    }

    private async Task PrepareWaveformAsync(
        IAudioPlayer player,
        int version,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            var cancellationToken = cancellationSource.Token;
            var sampleData = await player.GetSamplesAsync().WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (sampleData is null)
                return;

            // Peak generation is CPU-bound. Cancellation prevents stale data from being published
            // even though the legacy ISamplePeak contract itself has no cancellation parameter.
            var peaks = await Task.Run(() => samplePeak.GetPeakValues(sampleData), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != Volatile.Read(ref waveformVersion))
                return;

            Volatile.Write(ref rawPeakData, peaks);
            await ResampleAsync(peaks, version, cancellationToken);
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref waveformCancellationSource, null, cancellationSource), cancellationSource))
                cancellationSource.Dispose();
        }
    }

    private async Task ResampleAsync(PeakPointCollection rawData, int version, CancellationToken outerCancellationToken)
    {
        CancelAndDispose(ref resampleCancellationSource);
        var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellationToken,
            outerCancellationToken);
        resampleCancellationSource = cancellationSource;

        try
        {
            var resampleSize = Math.Max(0, stateProvider().ResampleSize);
            var result = resampleSize == 0
                ? rawData
                : await rawData.GenerateSimplfiedAsync(resampleSize, cancellationSource.Token);
            cancellationSource.Token.ThrowIfCancellationRequested();

            if (version == Volatile.Read(ref waveformVersion))
                Volatile.Write(ref usingPeakData, result);
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref resampleCancellationSource, null, cancellationSource), cancellationSource))
                cancellationSource.Dispose();
        }
    }

    private void OnRenderControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewSize(e.NewSize);
    }

    private void UpdateViewSize(Size size)
    {
        viewWidth = float.IsFinite((float)size.Width) ? Math.Max(0, (float)size.Width) : 0;
        viewHeight = float.IsFinite((float)size.Height) ? Math.Max(0, (float)size.Height) : 0;
    }

    private void UpdateCurrentTime(WaveformRenderState state)
    {
        if (state.AudioPlayer?.IsPlaying == true)
        {
            CurrentTime = state.AudioPlayer.CurrentTime;
            return;
        }

        if (state.Editor is not null)
        {
            var tGrid = state.Editor.GetCurrentTGrid();
            if (tGrid is not null)
            {
                CurrentTime = TGridCalculator.ConvertTGridToAudioTime(tGrid, state.Editor);
                return;
            }
        }

        CurrentTime = state.AudioPlayer?.CurrentTime ?? default;
    }

    private void UpdateDrawingContext()
    {
        var width = viewWidth;
        var height = viewHeight;
        CurrentDrawingTargetContext.ViewMatrix = Matrix4.Identity;
        CurrentDrawingTargetContext.ProjectionMatrix = width > 0 && height > 0
            ? Matrix4.CreateOrthographic(width, height, -1, 1)
            : Matrix4.Identity;
        CurrentDrawingTargetContext.Rect = new VisibleRect(new(width, 0), new(0, height));
        CurrentDrawingTargetContext.ViewWidth = width;
        CurrentDrawingTargetContext.ViewHeight = height;
    }

    private static void CancelAndDispose(ref CancellationTokenSource cancellationSource)
    {
        var oldSource = Interlocked.Exchange(ref cancellationSource, null);
        if (oldSource is null)
            return;

        try
        {
            oldSource.Cancel();
        }
        finally
        {
            oldSource.Dispose();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        Detach();
        isDisposed = true;
    }
}

internal sealed class WaveformFrameLimiter
{
    private double elapsedMilliseconds;
    private bool isFirstFrame = true;

    public bool ShouldRender(TimeSpan elapsed, int limitFramesPerSecond)
    {
        if (limitFramesPerSecond <= 0)
        {
            elapsedMilliseconds = 0;
            isFirstFrame = false;
            return true;
        }

        if (isFirstFrame)
        {
            isFirstFrame = false;
            elapsedMilliseconds = 0;
            return true;
        }

        var elapsedDelta = elapsed.TotalMilliseconds;
        if (double.IsFinite(elapsedDelta) && elapsedDelta > 0)
            elapsedMilliseconds += elapsedDelta;

        var frameInterval = 1000d / limitFramesPerSecond;
        if (elapsedMilliseconds < frameInterval)
            return false;

        elapsedMilliseconds %= frameInterval;
        return true;
    }

    public void Reset()
    {
        elapsedMilliseconds = 0;
        isFirstFrame = true;
    }
}
