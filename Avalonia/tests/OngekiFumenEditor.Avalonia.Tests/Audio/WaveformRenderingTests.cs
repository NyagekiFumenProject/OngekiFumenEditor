using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;
using SkiaSharp;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class WaveformRenderingTests
{
    [Fact]
    public void Viewport_ProjectsCurrentTimeFromOffsetAndZoom()
    {
        var created = WaveformGeometry.TryCreateViewport(
            200,
            100,
            TimeSpan.FromSeconds(2),
            50,
            10,
            out var viewport);

        Assert.True(created);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), viewport.FromTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), viewport.ToTime);
        Assert.Equal(-50, viewport.CurrentTimeX);
        Assert.Equal(viewport.CurrentTimeX, viewport.ProjectX(TimeSpan.FromSeconds(2)), 3);
        Assert.Equal(50, viewport.ProjectX(TimeSpan.FromSeconds(3)), 3);
    }

    [Theory]
    [InlineData(0, 100, 10)]
    [InlineData(100, 0, 10)]
    [InlineData(100, 100, 0)]
    [InlineData(float.NaN, 100, 10)]
    [InlineData(100, 100, float.PositiveInfinity)]
    public void Viewport_RejectsInvalidSizeOrZoom(float width, float height, float durationMsPerPixel)
    {
        Assert.False(WaveformGeometry.TryCreateViewport(
            width,
            height,
            TimeSpan.Zero,
            0,
            durationMsPerPixel,
            out _));
    }

    [Fact]
    public void VerticalExtents_MirrorMonoAndSeparateStereoChannels()
    {
        Assert.True(WaveformGeometry.TryGetVerticalExtents([0.5f], 100, 0.8f, out var monoTop, out var monoBottom));
        Assert.Equal(20, monoTop, 3);
        Assert.Equal(-20, monoBottom, 3);

        Assert.True(WaveformGeometry.TryGetVerticalExtents([0.25f, 0.75f], 100, 1, out var stereoTop, out var stereoBottom));
        Assert.Equal(12.5f, stereoTop, 3);
        Assert.Equal(-37.5f, stereoBottom, 3);
    }

    [Fact]
    public void VerticalExtents_ClampAndSanitizeMalformedAmplitudes()
    {
        Assert.True(WaveformGeometry.TryGetVerticalExtents([float.NaN, 4], 100, 2, out var top, out var bottom));
        Assert.Equal(0, top);
        Assert.Equal(-50, bottom);
        Assert.False(WaveformGeometry.TryGetVerticalExtents([], 100, 1, out _, out _));
    }

    [Fact]
    public void FrameLimiter_AllowsFirstFrameAndAccumulatesLimitedFrames()
    {
        var limiter = new WaveformFrameLimiter();

        Assert.True(limiter.ShouldRender(TimeSpan.Zero, 60));
        Assert.False(limiter.ShouldRender(TimeSpan.FromMilliseconds(5), 60));
        Assert.True(limiter.ShouldRender(TimeSpan.FromMilliseconds(12), 60));
        Assert.True(limiter.ShouldRender(TimeSpan.Zero, 0));
    }

    [AvaloniaFact]
    public async Task ProductSession_RendersMonoWaveformPixels_ResamplesAndStopsAfterDetach()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var peaks = BuildPeakData(channels: 1, pointCount: 21);
        var samplePeak = new StubSamplePeak(peaks);
        var player = new StubAudioPlayer(() => Task.FromResult(CreateSampleData(channels: 1)))
        {
            CurrentTime = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(2)
        };
        var state = new WaveformRenderState(player, null!, true, 0, 1, 10, 80, -1);
        var drawing = new DefaultWaveformDrawing(new DefaultWaveformOption(loadSettings: false));

        var host = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        var window = new Window
        {
            Width = 160,
            Height = 90,
            Content = host
        };
        using var session = new WaveformRenderSession(manager, samplePeak, drawing, () => state);

        try
        {
            window.Show();
            window.UpdateLayout();
            await session.AttachAsync(host);
            var renderContext = Assert.IsType<DefaultSkiaRenderContext>(session.RenderContext);
            await session.SetAudioPlayerAsync(player);

            Assert.Same(peaks, session.RawPeakData);
            Assert.Same(peaks, session.PeakData);
            Assert.Equal(1, samplePeak.CallCount);

            state = state with { ResampleSize = 5 };
            await session.ResampleAsync();
            Assert.NotNull(session.PeakData);
            Assert.True(session.PeakData.Count < peaks.Count);

            window.UpdateLayout();
            using var capturedFrame = window.CaptureRenderedFrame();
            Assert.NotNull(capturedFrame);
            using var encodedFrame = new MemoryStream();
            capturedFrame.Save(encodedFrame);
            encodedFrame.Position = 0;
            using var bitmap = SKBitmap.Decode(encodedFrame);
            Assert.NotNull(bitmap);

            var targetRect = session.CurrentDrawingTargetContext.Rect;
            var dominantColors = string.Join(", ", bitmap.Pixels
                .GroupBy(static color => color)
                .OrderByDescending(static group => group.Count())
                .Take(6)
                .Select(static group => $"{group.Key}:{group.Count()}"));
            var nonBackgroundPixels = bitmap.Pixels.Count(static color =>
                color.Alpha > 0 && (color.Red > 24 || color.Green > 24 || color.Blue > 24));
            var waveformPixels = bitmap.Pixels.Count(static color =>
                color.Alpha >= 200
                && color.Red is >= 65 and <= 145
                && color.Green is >= 110 and <= 195
                && color.Blue >= 190);
            Assert.True(targetRect.Width > 0 && targetRect.Height > 0,
                $"The waveform drawing target has an invalid size: {targetRect.Width}x{targetRect.Height}.");
            Assert.True(waveformPixels > 0,
                $"The product waveform path did not render any waveform-colored pixels. "
                + $"Target={targetRect.Width}x{targetRect.Height}, frames={session.RenderedFrameCount}, "
                + $"nonBackground={nonBackgroundPixels}, dominantColors=[{dominantColors}].");
            Assert.True(session.RenderedFrameCount > 0);
            Assert.True(renderContext.IsRendering);

            var initialWidth = targetRect.Width;
            window.Width = 220;
            window.UpdateLayout();
            using (var resizedFrame = window.CaptureRenderedFrame())
                Assert.NotNull(resizedFrame);
            Assert.True(session.CurrentDrawingTargetContext.Rect.Width > initialWidth,
                "The waveform drawing target did not follow the product control resize.");

            var renderedFrameCount = session.RenderedFrameCount;
            session.Detach(host);
            Assert.Null(host.Content);
            Assert.False(session.IsAttached);
            Assert.False(renderContext.IsRendering);

            renderContext.StartRendering();
            using var frameAfterDetach = window.CaptureRenderedFrame();
            renderContext.StopRendering();
            Assert.Equal(renderedFrameCount, session.RenderedFrameCount);
        }
        finally
        {
            session.Detach(host);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ProductSession_DetachCancelsPendingSampleLoadWithoutPublishingPeaks()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var pendingSamples = new TaskCompletionSource<SampleData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var samplePeak = new StubSamplePeak(BuildPeakData(channels: 2, pointCount: 2));
        var player = new StubAudioPlayer(() => pendingSamples.Task)
        {
            Duration = TimeSpan.FromSeconds(1)
        };
        var state = new WaveformRenderState(player, null!, true, 0, 1, 10, 40, -1);
        var host = new ContentControl();
        var window = new Window
        {
            Width = 100,
            Height = 60,
            Content = host
        };
        using var session = new WaveformRenderSession(
            manager,
            samplePeak,
            new DefaultWaveformDrawing(new DefaultWaveformOption(loadSettings: false)),
            () => state);

        try
        {
            window.Show();
            window.UpdateLayout();
            await session.AttachAsync(host);
            var preparation = session.SetAudioPlayerAsync(player);
            Assert.Equal(1, player.SampleRequestCount);

            session.Detach(host);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparation);
            pendingSamples.TrySetResult(CreateSampleData(channels: 2));

            Assert.Equal(0, samplePeak.CallCount);
            Assert.Null(session.RawPeakData);
            Assert.Null(session.PeakData);
        }
        finally
        {
            session.Detach(host);
            window.Close();
        }
    }

    private static PeakPointCollection BuildPeakData(int channels, int pointCount)
    {
        var sampleInfo = new SampleInfo
        {
            SampleRate = 48_000,
            BitsPerSample = 32,
            Channels = channels
        };
        var peaks = new PeakPointCollection(sampleInfo);
        peaks.BeginBatchAction();
        for (var i = 0; i < pointCount; i++)
        {
            var amplitudes = channels == 1
                ? new[] { 0.8f }
                : new[] { 0.8f, 0.45f };
            peaks.Add(new PeakPoint(TimeSpan.FromMilliseconds(i * 100), amplitudes));
        }
        peaks.EndBatchAction();
        return peaks;
    }

    private static SampleData CreateSampleData(int channels)
    {
        return new(
            ReadOnlyMemory<byte>.Empty,
            new SampleInfo
            {
                SampleRate = 48_000,
                BitsPerSample = 32,
                Channels = channels
            });
    }

    private sealed class StubSamplePeak(PeakPointCollection peaks) : ISamplePeak
    {
        public int CallCount { get; private set; }

        public PeakPointCollection GetPeakValues(SampleData data)
        {
            CallCount++;
            return peaks;
        }
    }

    private sealed class StubAudioPlayer(Func<Task<SampleData>> getSamplesAsync) : IAudioPlayer
    {
        public TimeSpan CurrentTime { get; init; }
        public float Speed { get; set; } = 1;
        public TimeSpan Duration { get; init; }
        public bool IsPlaying { get; init; }
        public bool IsAvaliable => true;
        public int SampleRequestCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public event IAudioPlayer.OnPlaybackFinishedFunc? OnPlaybackFinished
        {
            add { }
            remove { }
        }

        public void Play()
        {
        }

        public void Stop()
        {
        }

        public void Pause()
        {
        }

        public void Seek(TimeSpan timeSpan, bool pause)
        {
        }

        public Task<SampleData> GetSamplesAsync()
        {
            SampleRequestCount++;
            return getSamplesAsync();
        }

        public void Dispose()
        {
        }
    }
}
