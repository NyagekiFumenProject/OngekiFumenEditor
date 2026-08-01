using System;
using System.Threading.Tasks;

namespace NAudio.Wave.Browser;

/// <summary>Measures AudioWorklet startup notification latency with a short muted probe.</summary>
public static class LatencyMeasureHelper
{
    private const int WarmupRunCount = 1;
    private const int MeasurementRunCount = 5;
    private const float ProbeOutputVolume = 0.0f;
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Measures the average elapsed time from <c>AudioContext.resume()</c> being called until the
    /// browser main thread receives the AudioWorklet's first-frame message.
    /// </summary>
    /// <remarks>
    /// The method renders six muted 100 ms sine-wave probes: one warmup followed by five measured
    /// runs. Call it after Web Audio has been authorized, preferably directly from a click or touch
    /// handler. The result includes Worklet-to-main-thread message delivery, but not the Web Audio
    /// output path or the physical device's latency.
    /// </remarks>
    public static Task<TimeSpan> MeasureLatency(BrowserAudioWorkletOptions options)
    {
        BrowserAudioWorkletPlayer.ValidateOptions(options);
#if BROWSER
        return MeasureLatencyCore(
            new BrowserAudioWorkletPlayer(options),
            DefaultRunTimeout);
#else
        throw new PlatformNotSupportedException(
            "Latency measurement requires a browser WebAssembly application with AudioWorklet support.");
#endif
    }

    internal static Task<TimeSpan> MeasureLatency(
        BrowserAudioWorkletOptions options,
        IAudioWorkletBridge bridge,
        TimeSpan runTimeout)
    {
        BrowserAudioWorkletPlayer.ValidateOptions(options);
        ArgumentNullException.ThrowIfNull(bridge);
        if (runTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(runTimeout));
        }

        return MeasureLatencyCore(
            new BrowserAudioWorkletPlayer(bridge, options),
            runTimeout);
    }

    private static async Task<TimeSpan> MeasureLatencyCore(
        BrowserAudioWorkletPlayer player,
        TimeSpan runTimeout)
    {
        var source = new LatencyProbeSampleProvider();
        try
        {
            player.Init(source);
            player.Volume = ProbeOutputVolume;
            await player.PrepareAsync();

            long measuredTicks = 0;
            int totalRuns = WarmupRunCount + MeasurementRunCount;
            for (int run = 0; run < totalRuns; run++)
            {
                source.Reset();
                TimeSpan latency = await MeasureRunAsync(player, runTimeout);
                if (run >= WarmupRunCount)
                {
                    measuredTicks = checked(measuredTicks + latency.Ticks);
                }
            }

            return TimeSpan.FromTicks(measuredTicks / MeasurementRunCount);
        }
        finally
        {
            await player.DisposeAsync();
        }
    }

    private static async Task<TimeSpan> MeasureRunAsync(
        BrowserAudioWorkletPlayer player,
        TimeSpan runTimeout)
    {
        var firstFrame = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var naturallyStopped = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnFirstFrameRendered(object sender, BrowserAudioFirstFrameEventArgs args)
            => firstFrame.TrySetResult(args.ObservedResumeToFirstFrameLatency);

        void OnPlaybackStopped(object sender, StoppedEventArgs args)
        {
            if (args.Exception == null)
            {
                naturallyStopped.TrySetResult(true);
            }
            else
            {
                naturallyStopped.TrySetException(args.Exception);
            }
        }

        player.FirstFrameRendered += OnFirstFrameRendered;
        player.PlaybackStopped += OnPlaybackStopped;
        try
        {
            Task playTask;
            try
            {
                playTask = player.PlayAsync();
            }
            catch (Exception ex)
            {
                firstFrame.TrySetException(ex);
                naturallyStopped.TrySetException(ex);
                throw;
            }

            _ = CompleteOnPlaybackFailureAsync(playTask, firstFrame, naturallyStopped);
            await Task.WhenAll(playTask, firstFrame.Task, naturallyStopped.Task)
                .WaitAsync(runTimeout);
            return await firstFrame.Task;
        }
        finally
        {
            player.FirstFrameRendered -= OnFirstFrameRendered;
            player.PlaybackStopped -= OnPlaybackStopped;
        }
    }

    private static async Task CompleteOnPlaybackFailureAsync(
        Task playTask,
        TaskCompletionSource<TimeSpan> firstFrame,
        TaskCompletionSource<bool> naturallyStopped)
    {
        try
        {
            await playTask;
        }
        catch (Exception ex)
        {
            firstFrame.TrySetException(ex);
            naturallyStopped.TrySetException(ex);
        }
    }

    private sealed class LatencyProbeSampleProvider : ISampleProvider
    {
        private const int SampleRate = 48000;
        private const int Channels = 2;
        private const int FrameCount = SampleRate / 10;
        private const double Frequency = 440;
        private const double Gain = 0.2;
        private const double PhaseIncrement = 2 * Math.PI * Frequency / SampleRate;
        private int position;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(
            SampleRate,
            Channels);

        public int Read(Span<float> buffer)
        {
            int requestedFrames = buffer.Length / Channels;
            int framesToRead = Math.Min(requestedFrames, FrameCount - position);
            for (int frame = 0; frame < framesToRead; frame++)
            {
                float sample = (float)(Gain * Math.Sin((position + frame) * PhaseIncrement));
                int sampleOffset = frame * Channels;
                buffer[sampleOffset] = sample;
                buffer[sampleOffset + 1] = sample;
            }

            position += framesToRead;
            return framesToRead * Channels;
        }

        public void Reset() => position = 0;
    }
}
