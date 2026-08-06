using System.Reflection;
using Avalonia.Headless.XUnit;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class AudioManagerSimpleFileTests
{
    [Fact]
    public void Interface_LoadMethods_AcceptSimpleFilesAndSoundStreamsWithoutPathOnlyOverloads()
    {
        var methods = typeof(IAudioManager)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var loadAudio = Assert.Single(methods, x => x.Name == nameof(IAudioManager.LoadAudioAsync));
        var loadSounds = methods.Where(x => x.Name == nameof(IAudioManager.LoadSoundAsync)).ToArray();
        var simpleFileSound = Assert.Single(loadSounds, x => x.GetParameters().Length == 1);
        var streamSound = Assert.Single(loadSounds, x => x.GetParameters().Length == 2);

        Assert.Equal(typeof(Task<IAudioPlayer>), loadAudio.ReturnType);
        Assert.Equal(typeof(ISimpleFile), Assert.Single(loadAudio.GetParameters()).ParameterType);
        Assert.Equal(typeof(Task<ISoundPlayer>), simpleFileSound.ReturnType);
        Assert.Equal(typeof(ISimpleFile), Assert.Single(simpleFileSound.GetParameters()).ParameterType);
        Assert.Equal(typeof(Task<ISoundPlayer>), streamSound.ReturnType);
        Assert.Collection(
            streamSound.GetParameters(),
            parameter => Assert.Equal(typeof(Stream), parameter.ParameterType),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType));
        Assert.DoesNotContain(
            methods,
            x => x.GetParameters() is [{ ParameterType: var parameterType }] &&
                 parameterType == typeof(string));
    }

    [AvaloniaFact]
    public async Task LoadSoundAsync_NonLocalSimpleFile_ReadsThroughStreamFactory()
    {
        var readerFactory = new TrackingAudioFileReaderFactory();
        using var manager = new NAudioManager(
            new StubWavePlayerFactory(),
            readerFactory,
            new StubSchedulerManager(),
            AudioPlatformCapabilities.Unknown);
        using var file = new StreamOnlyTestSimpleFile(
            "sound.wav",
            "test://audio/sound.wav",
            CreateWaveFile());

        using var sound = await manager.LoadSoundAsync(file);

        Assert.NotNull(sound);
        Assert.Equal(1, readerFactory.StreamOpenCount);
        Assert.Equal(0, readerFactory.PathOpenCount);
        Assert.Equal("sound.wav", readerFactory.OpenedFileName);
        Assert.InRange(
            sound.Duration,
            TimeSpan.FromMilliseconds(9),
            TimeSpan.FromMilliseconds(11));
    }

    private static byte[] CreateWaveFile()
    {
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(
                   stream,
                   WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2)))
        {
            writer.WriteSamples(new float[960], 0, 960);
        }

        return stream.ToArray();
    }

    private sealed class StreamOnlyTestSimpleFile(
        string fileName,
        string fullPath,
        byte[] content) : ISimpleFile
    {
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => fullPath;
        public string? LocalPath => null;
        public string FileName => fileName;
        public long FileLength => content.LongLength;

        public ValueTask<string[]> ReadAllLines() => throw new NotSupportedException();
        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(content.ToArray());
        public Task<Stream> OpenRead() =>
            Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        public Task<Stream> OpenWrite() => throw new NotSupportedException();
        public void Dispose()
        {
        }
    }

    private sealed class TrackingAudioFileReaderFactory : INAudioFileReaderFactory
    {
        public IReadOnlyList<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } =
            [(".wav", "Audio File")];

        public int PathOpenCount { get; private set; }
        public int StreamOpenCount { get; private set; }
        public string OpenedFileName { get; private set; } = string.Empty;

        public WaveStream CreateAudioFileReader(string filePath)
        {
            PathOpenCount++;
            throw new InvalidOperationException("The non-local file must not be opened by path.");
        }

        public WaveStream CreateAudioFileReader(Stream stream, string fileName)
        {
            StreamOpenCount++;
            OpenedFileName = fileName;
            return new WaveFileReader(stream);
        }
    }

    private sealed class StubWavePlayerFactory : INAudioWavePlayerFactory
    {
        public Task<IWavePlayer> CreateDefaultWavePlayer() =>
            Task.FromResult<IWavePlayer>(new StubWavePlayer());
    }

    private sealed class StubWavePlayer : IWavePlayer
    {
        public PlaybackState PlaybackState { get; private set; }
        public WaveFormat OutputWaveFormat { get; private set; } =
            WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        public float Volume { get; set; } = 1;

        public event EventHandler<StoppedEventArgs> PlaybackStopped
        {
            add { }
            remove { }
        }

        public void Init(IWaveProvider waveProvider)
        {
            OutputWaveFormat = waveProvider.WaveFormat;
        }

        public void Play()
        {
            PlaybackState = PlaybackState.Playing;
        }

        public void Pause()
        {
            PlaybackState = PlaybackState.Paused;
        }

        public void Stop()
        {
            PlaybackState = PlaybackState.Stopped;
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubSchedulerManager : ISchedulerManager
    {
        public IEnumerable<ISchedulable> Schedulers => [];
        public Task Init() => Task.CompletedTask;
        public Task AddScheduler(ISchedulable s) => Task.CompletedTask;
        public Task RemoveScheduler(ISchedulable s) => Task.CompletedTask;
        public Task Term() => Task.CompletedTask;
    }
}
