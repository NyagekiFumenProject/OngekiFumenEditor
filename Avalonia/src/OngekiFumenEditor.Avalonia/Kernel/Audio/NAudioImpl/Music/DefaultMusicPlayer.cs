using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Utils;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Utils;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Music;

internal sealed class DefaultMusicPlayer : ObservableObject, IAudioPlayer, ISchedulable
{
    private readonly MixingSampleProvider musicMixer;
    private readonly NAudioManager manager;
    private readonly ISchedulerManager schedulerManager;
    private readonly INAudioFileReaderFactory audioFileReaderFactory;
    private readonly Stopwatch stopwatch = new();
    private FinishedListenerProvider finishProvider;
    private TimeSpan baseOffset;
    private TimeSpan pauseTime;
    private bool isAvaliable;
    private bool isPlaying;
    private TimeSpan duration;
    private byte[] samples;
    private BufferWaveStream audioFileReader;

    public event IAudioPlayer.OnPlaybackFinishedFunc OnPlaybackFinished;

    public TimeSpan Duration => duration;

    public TimeSpan CurrentTime => GetTime();

    public float Speed
    {
        get => manager.MusicSpeed;
        set => manager.MusicSpeed = value;
    }

    public bool IsPlaying
    {
        get => isPlaying;
        private set => SetProperty(ref isPlaying, value);
    }

    public float Volume
    {
        get => manager.MusicVolume;
        set
        {
            manager.MusicVolume = value;
            OnPropertyChanged();
        }
    }

    public string SchedulerName => "DefaultMusicPlayer Playing Updater";

    public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMilliseconds(1000.0 / 60);

    public bool IsAvaliable
    {
        get => isAvaliable;
        private set => SetProperty(ref isAvaliable, value);
    }

    public DefaultMusicPlayer(
        MixingSampleProvider musicMixer,
        NAudioManager manager,
        ISchedulerManager schedulerManager,
        INAudioFileReaderFactory audioFileReaderFactory)
    {
        this.musicMixer = musicMixer;
        this.manager = manager;
        this.schedulerManager = schedulerManager;
        this.audioFileReaderFactory = audioFileReaderFactory;
    }

    private void Provider_OnReturnEmptySamples()
    {
        finishProvider.StopListen();
        OnPlaybackFinished?.Invoke();
    }

    public async Task Load(string audioFile, int targetSampleRate)
    {
        //release resource before loading new one.
        Dispose();

        try
        {
            Log.LogInfo($"Load audio file: {audioFile}");
            using var rawStream = audioFileReaderFactory.CreateAudioFileReader(audioFile);
            duration = rawStream.TotalTime;
            var processedProvider = await AudioCompatibilizer.CheckCompatible(
                rawStream.ToSampleProvider(),
                targetSampleRate);

            samples = processedProvider.ToWaveProvider().ToArray();

            audioFileReader = new BufferWaveStream(samples, processedProvider.WaveFormat);
            audioFileReader.Seek(0, SeekOrigin.Begin);

            finishProvider = new FinishedListenerProvider(audioFileReader);
            finishProvider.StartListen();
            finishProvider.OnReturnEmptySamples += Provider_OnReturnEmptySamples;

            baseOffset = TimeSpan.Zero;
            pauseTime = TimeSpan.Zero;
            OnPropertyChanged(nameof(Duration));
            IsAvaliable = true;
        }
        catch (Exception e)
        {
            Log.LogError($"Load audio file ({audioFile}) failed : {e.Message}");
            Dispose();
        }
    }

    public void Seek(TimeSpan seekTime, bool pause)
    {
        if (!IsAvaliable || audioFileReader is null)
            return;

        seekTime = MathUtils.Max(TimeSpan.Zero, MathUtils.Min(seekTime, Duration));

        audioFileReader.Seek(
            (long)(audioFileReader.WaveFormat.AverageBytesPerSecond * seekTime.TotalSeconds),
            SeekOrigin.Begin);
        //more accurate
        baseOffset = audioFileReader.CurrentTime;
        pauseTime = baseOffset;

        finishProvider.StartListen();

        if (!pause)
            Play();
        UpdatePropsManually();
    }

    public void Play()
    {
        if (!IsAvaliable || IsPlaying)
            return;

        IsPlaying = true;
        baseOffset = pauseTime;
        stopwatch.Restart();
        manager.StartOutput();
        musicMixer.AddMixerInput(finishProvider);
        UpdatePropsManually();
        manager.Reposition();

        _ = schedulerManager.AddScheduler(this);
    }

    private TimeSpan GetTime()
    {
        if (!IsPlaying)
            return pauseTime;
        var offset = stopwatch.Elapsed * manager.MusicSpeed;
        var adjustedTime = offset + baseOffset - TimeSpan.FromMilliseconds(manager.SpeedCostDelayMs / 2.0);
        return MathUtils.Max(TimeSpan.Zero, adjustedTime);
    }

    public void Stop()
    {
        if (!IsAvaliable)
            return;

        IsPlaying = false;
        stopwatch.Stop();
        musicMixer.RemoveMixerInput(finishProvider);
        _ = schedulerManager.RemoveScheduler(this);
        Seek(TimeSpan.Zero, true);
        UpdatePropsManually();
    }

    public void Pause()
    {
        if (!IsAvaliable || !IsPlaying)
            return;

        pauseTime = GetTime();
        baseOffset = pauseTime;
        IsPlaying = false;
        stopwatch.Stop();
        musicMixer.RemoveMixerInput(finishProvider);
        UpdatePropsManually();
        _ = schedulerManager.RemoveScheduler(this);
    }

    private void CleanCurrentOut()
    {
        if (finishProvider is not null)
            musicMixer.RemoveMixerInput(finishProvider);
        UpdatePropsManually();
    }

    public void Dispose()
    {
        CleanCurrentOut();

        if (finishProvider is not null)
        {
            finishProvider.OnReturnEmptySamples -= Provider_OnReturnEmptySamples;
            finishProvider = null;
        }

        audioFileReader?.Dispose();
        audioFileReader = null;
        samples = null;
        stopwatch.Reset();
        IsAvaliable = false;
        IsPlaying = false;

        _ = schedulerManager.RemoveScheduler(this);
    }

    public void OnSchedulerTerm()
    {
    }

    public Task OnScheduleCall(CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
            UpdatePropsManually();
        return Task.CompletedTask;
    }

    private void UpdatePropsManually()
    {
        if (!IsAvaliable)
            return;

        OnPropertyChanged(nameof(CurrentTime));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(IsPlaying));
    }

    public Task<SampleData> GetSamplesAsync()
    {
        if (!IsAvaliable)
            return Task.FromResult<SampleData>(default);

        var subBuffer = samples.AsMemory();
        var sampleData = new SampleData(subBuffer, ConvertToSampleInfo(audioFileReader.WaveFormat));

        return Task.FromResult(sampleData);
    }

    public static SampleInfo ConvertToSampleInfo(WaveFormat waveFormat)
    {
        return new SampleInfo
        {
            SampleRate = waveFormat.SampleRate,
            Channels = waveFormat.Channels,
            BitsPerSample = waveFormat.BitsPerSample
        };
    }
}
