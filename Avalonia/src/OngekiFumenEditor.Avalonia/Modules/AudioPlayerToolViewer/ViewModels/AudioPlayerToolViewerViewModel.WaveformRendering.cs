using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;

public partial class AudioPlayerToolViewerViewModel
{
    private WaveformRenderSession waveformRenderSession;

    internal async Task AttachWaveformHostAsync(ContentControl host, CancellationToken cancellationToken = default)
    {
        if (isDisposed)
            return;

        waveformRenderSession ??= CreateWaveformRenderSession();
        if (waveformRenderSession is null)
            return;

        await waveformRenderSession.AttachAsync(host, cancellationToken);
        ObserveWaveformTask(waveformRenderSession.SetAudioPlayerAsync(AudioPlayer), "prepare waveform");
    }

    internal void DetachWaveformHost(ContentControl host)
    {
        waveformRenderSession?.Detach(host);
    }

    private WaveformRenderSession CreateWaveformRenderSession()
    {
        var renderManager = TryGetService<IRenderManager>();
        var samplePeak = TryGetService<ISamplePeak>();
        if (renderManager is null || samplePeak is null || WaveformDrawing is null)
        {
            Log.LogWarn("Waveform rendering services are not available.");
            return null;
        }

        return new(
            renderManager.GetCurrentRenderManagerImpl(),
            samplePeak,
            WaveformDrawing,
            CaptureWaveformRenderState);
    }

    private WaveformRenderState CaptureWaveformRenderState()
    {
        return new(
            AudioPlayer,
            Editor,
            IsShowWaveform,
            ResampleSize,
            WaveformVecticalScale,
            DurationMsPerPixel,
            CurrentTimeXOffset,
            LimitFPS);
    }

    private void OnWaveformAudioPlayerChanged(IAudioPlayer player)
    {
        if (waveformRenderSession is not null)
            ObserveWaveformTask(waveformRenderSession.SetAudioPlayerAsync(player), "prepare waveform");
    }

    private void OnWaveformResampleSizeChanged()
    {
        if (waveformRenderSession is not null)
            ObserveWaveformTask(waveformRenderSession.ResampleAsync(), "resample waveform");
    }

    private static void ObserveWaveformTask(Task task, string operation)
    {
        _ = ObserveWaveformTaskAsync(task, operation);
    }

    private static async Task ObserveWaveformTaskAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to {operation}.", e);
        }
    }

    private void DisposeWaveformRendering()
    {
        waveformRenderSession?.Dispose();
        waveformRenderSession = null;
    }
}
