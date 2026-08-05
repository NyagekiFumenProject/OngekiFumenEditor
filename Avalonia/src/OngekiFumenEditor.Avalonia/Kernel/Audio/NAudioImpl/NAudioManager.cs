using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Music;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Sound;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.SoundTouch;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Utils;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl;

[RegisterSingleton<IAudioManager>]
internal sealed class NAudioManager : ObservableObject, IAudioManager
{
    private readonly HashSet<WeakReference<IAudioPlayer>> ownAudioPlayerRefs = [];
    private readonly bool enableSoundMultiPlay;
    private readonly int targetSampleRate;
    private readonly INAudioWavePlayerFactory wavePlayerFactory;
    private readonly INAudioFileReaderFactory audioFileReaderFactory;
    private readonly ISchedulerManager schedulerManager;
    private readonly SemaphoreSlim outputInitializationLock = new(1, 1);
    private readonly MixingSampleProvider masterMixer;
    private readonly MixingSampleProvider soundMixer;
    private readonly MixingSampleProvider musicMixer;
    private readonly Dictionary<CachedSound, ISampleProvider> cs2providerMap = [];
    private readonly Dictionary<ISampleProvider, CachedSound> provider2csMap = [];
    private readonly VolumeSampleProvider soundVolumeWrapper;
    private readonly VolumeSampleProvider musicVolumeWrapper;
    private readonly VarispeedSampleProvider speedProvider;
    private IWavePlayer audioOutputDevice;
    private bool disposed;

    public bool EnableVarspeed => speedProvider is not null;
    public int SpeedCostDelayMs { get; }

    public float SoundVolume
    {
        get => soundVolumeWrapper.Volume;
        set
        {
            soundVolumeWrapper.Volume = value;

            AudioSetting.Default.SoundVolume = value;
            AudioSetting.Default.Save();
            OnPropertyChanged();
        }
    }

    public float MusicVolume
    {
        get => musicVolumeWrapper.Volume;
        set
        {
            musicVolumeWrapper.Volume = value;

            AudioSetting.Default.MusicVolume = value;
            AudioSetting.Default.Save();
            OnPropertyChanged();
        }
    }

    public float MusicSpeed
    {
        get => EnableVarspeed ? speedProvider.PlaybackRate : 1;
        set
        {
            if (EnableVarspeed)
            {
                //we can able to change speed when all player is not playing
                if (!ownAudioPlayerRefs.Any(x => x.TryGetTarget(out var player) && player.IsPlaying))
                    speedProvider.PlaybackRate = value;
            }

            OnPropertyChanged();
        }
    }

    public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList
        => audioFileReaderFactory.SupportAudioFileExtensionList;

    public NAudioManager(
        INAudioWavePlayerFactory wavePlayerFactory,
        INAudioFileReaderFactory audioFileReaderFactory,
        ISchedulerManager schedulerManager,
        IAudioPlatformCapabilities platformCapabilities)
    {
        this.wavePlayerFactory = wavePlayerFactory;
        this.audioFileReaderFactory = audioFileReaderFactory;
        this.schedulerManager = schedulerManager;

        var requestedVarspeed = AudioSetting.Default.EnableVarspeed;
        enableSoundMultiPlay = AudioSetting.Default.EnableSoundMultiPlay;
        targetSampleRate = AudioSetting.Default.AudioSampleRate;
        var enableVarspeed = requestedVarspeed && platformCapabilities.SupportsVarspeed;
        SpeedCostDelayMs = enableVarspeed ? AudioSetting.Default.VarspeedReadDurationMs : 0;

        if (requestedVarspeed && !enableVarspeed)
        {
            Log.LogWarning(
                $"SoundTouch varispeed is unavailable for audio profile {platformCapabilities.Profile}.");
        }

        Log.LogDebug($"targetSampleRate: {targetSampleRate}");
        var requestedOutput = (AudioOutputType)AudioSetting.Default.AudioOutputType;
        var outputResolution = platformCapabilities.ResolveOutput(requestedOutput);
        Log.LogDebug(
            $"audioOutputType: requested={requestedOutput}, " +
            $"effective={outputResolution.EffectiveBackend}, profile={platformCapabilities.Profile}");
        Log.LogDebug($"enableSoundMultiPlay: {enableSoundMultiPlay}");
        Log.LogDebug($"enableVarspeed: {enableVarspeed}");
        Log.LogDebug($"SpeedCostDelayMs: {SpeedCostDelayMs}");

        var format = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, 2);
        masterMixer = new MixingSampleProvider(format) { ReadFully = true };

        //setup sound
        soundMixer = new MixingSampleProvider(format) { ReadFully = true };
        soundMixer.MixerInputEnded += SoundMixer_MixerInputEnded;
        soundVolumeWrapper = new VolumeSampleProvider(soundMixer)
        {
            Volume = AudioSetting.Default.SoundVolume
        };
        masterMixer.AddMixerInput(soundVolumeWrapper);

        //setup music
        musicMixer = new MixingSampleProvider(format) { ReadFully = true };
        if (enableVarspeed)
        {
            speedProvider = new VarispeedSampleProvider(
                musicMixer,
                SpeedCostDelayMs,
                new SoundTouchProfile(true, false));
            musicVolumeWrapper = new VolumeSampleProvider(speedProvider);
        }
        else
        {
            musicVolumeWrapper = new VolumeSampleProvider(musicMixer);
        }

        musicVolumeWrapper.Volume = AudioSetting.Default.MusicVolume;
        masterMixer.AddMixerInput(musicVolumeWrapper);
    }

    private async Task EnsureAudioOutputInitializedAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (audioOutputDevice is not null)
            return;

        await outputInitializationLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (audioOutputDevice is not null)
                return;

            IWavePlayer newOutputDevice = null;
            try
            {
                newOutputDevice = await wavePlayerFactory.CreateDefaultWavePlayer();
                newOutputDevice.Init(masterMixer);
                audioOutputDevice = newOutputDevice;
            }
            catch (Exception e)
            {
                newOutputDevice?.Dispose();
                Log.LogError($"Can't create audio output device: {e.Message}");
                throw;
            }

            Log.LogDebug($"audioOutputDevice: {audioOutputDevice}");
            Log.LogInfo($"Audio implement will use {GetType()}");
        }
        finally
        {
            outputInitializationLock.Release();
        }
    }

    internal void StartOutput()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        audioOutputDevice?.Play();
    }

    private void SoundMixer_MixerInputEnded(object sender, SampleProviderEventArgs e)
    {
        RemoveSoundMixerInput(e.SampleProvider, false);
    }

    public void PlaySound(CachedSound sound, float volume, TimeSpan init)
    {
        StartOutput();

        if (!enableSoundMultiPlay)
        {
            //stop previous
            if (cs2providerMap.TryGetValue(sound, out var prevProvider))
                RemoveSoundMixerInput(prevProvider, true);
        }

        ISampleProvider provider = new VolumeSampleProvider(new CachedSoundSampleProvider(sound))
        {
            Volume = volume
        };
        if (init.TotalMilliseconds != 0)
        {
            provider = new OffsetSampleProvider(provider)
            {
                SkipOver = init
            };
        }

        AddSoundMixerInput(provider, sound);
    }

    public void AddSoundMixerInput(ISampleProvider input, CachedSound cachedSound)
    {
        if (!enableSoundMultiPlay)
        {
            cs2providerMap[cachedSound] = input;
            provider2csMap[input] = cachedSound;
        }

        soundMixer.AddMixerInput(input);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="input"></param>
    /// <param name="mixerRemove">mixer是否需要调用RemoveMixerInput()</param>
    public void RemoveSoundMixerInput(ISampleProvider input, bool mixerRemove)
    {
        if (mixerRemove)
            soundMixer.RemoveMixerInput(input);

        if (!enableSoundMultiPlay)
        {
            if (provider2csMap.TryGetValue(input, out var cachedSound))
                cs2providerMap.Remove(cachedSound);
            provider2csMap.Remove(input);
        }
    }

    private async Task<IAudioPlayer> LoadAudioFromLocalPathAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        await EnsureAudioOutputInitializedAsync();

        if (filePath.EndsWith(".acb", StringComparison.OrdinalIgnoreCase))
        {
            if (!SupportAudioFileExtensionList.Any(x =>
                    x.fileExt.Equals(".acb", StringComparison.OrdinalIgnoreCase)))
            {
                throw new PlatformNotSupportedException(
                    "ACB audio conversion is not available on this platform.");
            }

            filePath = await AcbConverter.ConvertAcbFileToWavFile(filePath);
            if (filePath is null)
                return null;
        }

        var player = new DefaultMusicPlayer(
            musicMixer,
            this,
            schedulerManager,
            audioFileReaderFactory);
        ownAudioPlayerRefs.Add(new WeakReference<IAudioPlayer>(player));
        await player.Load(filePath, targetSampleRate);
        return player;
    }

    public async Task<IAudioPlayer> LoadAudioAsync(ISimpleFile file)
    {
        if (file is null)
            return null;

        var extension = Path.GetExtension(file.FileName);
        if (extension.Equals(".acb", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(file.LocalPath))
            {
                throw new PlatformNotSupportedException(
                    "ACB audio requires a local file path and access to its associated AWB file.");
            }

            return await LoadAudioFromLocalPathAsync(file.LocalPath);
        }

        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(file.LocalPath))
        {
            // The current Desktop decoder is Media Foundation, whose API accepts paths only.
            return await LoadAudioFromLocalPathAsync(file.LocalPath);
        }

        await EnsureAudioOutputInitializedAsync();
        var player = new DefaultMusicPlayer(
            musicMixer,
            this,
            schedulerManager,
            audioFileReaderFactory);
        ownAudioPlayerRefs.Add(new WeakReference<IAudioPlayer>(player));
        await player.Load(file, targetSampleRate);
        return player;
    }

    public async Task<ISoundPlayer> LoadSoundAsync(ISimpleFile file)
    {
        if (file is null)
            return null;

        var extension = Path.GetExtension(file.FileName);
        Log.LogInfo($"Load sound file: {file.FullPath}");

        if ((extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".acb", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(file.LocalPath))
        {
            await EnsureAudioOutputInitializedAsync();
            using var localAudioFileReader = audioFileReaderFactory.CreateAudioFileReader(file.LocalPath);
            return await CreateSoundPlayerAsync(localAudioFileReader);
        }

        await using var sourceStream = await file.OpenRead();
        return await LoadSoundAsync(sourceStream, file.FileName);
    }

    public async Task<ISoundPlayer> LoadSoundAsync(Stream stream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        await EnsureAudioOutputInitializedAsync();
        Log.LogInfo($"Load sound stream: {fileName}");
        using var audioFileReader = audioFileReaderFactory.CreateAudioFileReader(stream, fileName);
        return await CreateSoundPlayerAsync(audioFileReader);
    }

    private async Task<ISoundPlayer> CreateSoundPlayerAsync(WaveStream audioFileReader)
    {
        var provider = await AudioCompatibilizer.CheckCompatible(
            audioFileReader.ToSampleProvider(),
            targetSampleRate);
        return new NAudioSoundPlayer(new CachedSound(provider), this);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Log.LogDebug("call DefaultAudioManager.Dispose()");
        foreach (var weakRef in ownAudioPlayerRefs)
        {
            if (weakRef.TryGetTarget(out var player))
                player?.Dispose();
        }

        ownAudioPlayerRefs.Clear();
        soundMixer.MixerInputEnded -= SoundMixer_MixerInputEnded;
        audioOutputDevice?.Dispose();
        speedProvider?.Dispose();
        outputInitializationLock.Dispose();
    }

    public ILoopHandle PlayLoopSound(CachedSound sound, float volume, TimeSpan init)
    {
        StartOutput();

        if (!enableSoundMultiPlay)
        {
        }

        ISampleProvider provider = new LoopableProvider(new CachedSoundSampleProvider(sound));

        if (init.TotalMilliseconds != 0)
        {
            provider = new OffsetSampleProvider(provider)
            {
                SkipOver = init
            };
        }

        var handle = new NAudioLoopHandle(new VolumeSampleProvider(provider));
        handle.Volume = volume;

        //add to mixer
        AddSoundMixerInput(handle.Provider, sound);

        //Log.LogDebug($"handle hashcode = {handle.GetHashCode()}");
        return handle;
    }

    public void StopLoopSound(ILoopHandle h)
    {
        if (h is not NAudioLoopHandle handle)
            return;

        //Log.LogDebug($"handle hashcode = {handle.GetHashCode()}");
        RemoveSoundMixerInput(handle.Provider, true);
    }

    public void Reposition()
    {
        speedProvider?.Reposition();
    }
}
