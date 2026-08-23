using System.ComponentModel;
using System.Text;
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorProjectCreationTransactionTests
{
    [AvaloniaFact]
    public void CreationPlan_SeparatesProjectBindingsFromFilesThatMustBeCopied()
    {
        using var root = new MemoryDirectory("project");
        var projectAudio = root.AddExistingFile("audio.wav", [1, 2, 3]);
        var projectFumen = root.AddExistingFile("chart.ogkr", [4, 5, 6]);
        using var selection = CreateExistingSelection(root, projectAudio, projectFumen);
        using var plan = EditorProjectCreationPlan.Create(selection);

        Assert.Empty(plan.FilesToCopy);
        Assert.Equal(
            new[] { EditorProjectFileRole.Fumen, EditorProjectFileRole.Audio },
            plan.ExistingBindings.Select(binding => binding.Role));
        Assert.Equal(new[] { "Song.nyagekiProj" }, plan.PlannedTargetFileNames);
    }

    [AvaloniaFact]
    public async Task AttachRejected_DeletesOnlyCreatedFilesInReverseOrder()
    {
        using var root = new MemoryDirectory("project");
        var existing = root.AddExistingFile("keep.txt", [9, 8, 7]);
        var sourceAudio = MemoryFile.CreateStandalone("source.wav", [1, 2, 3, 4]);
        var plan = EditorProjectCreationPlan.Create(CreateNewSelection(root, sourceAudio));
        var coordinator = CreateCoordinator((_, _) => Task.FromResult(false));

        var outcome = await coordinator.RunAsync(
            plan,
            progress: null,
            CancellationToken.None);

        var failed = Assert.IsType<EditorProjectCreationOutcome.Failed>(outcome);
        AssertFailureKind(failed, EditorProjectCreationFailureKind.EditorRejected);
        Assert.Empty(failed.RollbackFailures);
        Assert.Equal(
            new[] { "Song.nyagekiProj", "chart.ogkr", "audio.wav" },
            root.DeleteAttempts);
        Assert.Equal(new[] { "keep.txt" }, root.ChildFiles.Select(file => file.FileName));
        Assert.Equal([9, 8, 7], existing.Content);
        Assert.Equal(0, existing.DeleteCount);
        Assert.Equal(1, sourceAudio.DisposeCount);
        Assert.All(root.DeleteTokens, token => Assert.False(token.CanBeCanceled));
    }

    [AvaloniaFact]
    public async Task SuccessfulCommit_KeepsCreatedFilesAndTransfersRootToEditorContext()
    {
        using var root = new MemoryDirectory("project");
        root.AddExistingFile("keep.txt", [9]);
        var sourceAudio = MemoryFile.CreateStandalone("source.wav", [1, 2, 3, 4]);
        EditorContext? attachedContext = null;
        var plan = EditorProjectCreationPlan.Create(CreateNewSelection(root, sourceAudio));
        var coordinator = CreateCoordinator((context, _) =>
        {
            attachedContext = context;
            return Task.FromResult(true);
        });

        var outcome = await coordinator.RunAsync(
            plan,
            progress: null,
            CancellationToken.None);

        Assert.True(
            outcome is EditorProjectCreationOutcome.Succeeded,
            outcome is EditorProjectCreationOutcome.Failed failure
                ? failure.Exception.ToString()
                : $"Unexpected outcome: {outcome}");
        Assert.NotNull(attachedContext);
        Assert.Same(root, attachedContext!.ProjectRoot);
        Assert.Equal(
            new[] { "keep.txt", "audio.wav", "chart.ogkr", "Song.nyagekiProj" },
            root.ChildFiles.Select(file => file.FileName));
        Assert.Empty(root.DeleteAttempts);
        Assert.Equal(0, root.DisposeCount);
        Assert.Equal(1, sourceAudio.DisposeCount);
        Assert.Equal(EditorProjectDataModel.VERSION, attachedContext.ProjectData.Version);

        attachedContext.Dispose();
        Assert.Equal(1, root.DisposeCount);
    }

    [AvaloniaFact]
    public async Task CancellationAfterCreate_TracksThatFileAndRollsBackWithoutDeletingSources()
    {
        using var root = new MemoryDirectory("project");
        var existing = root.AddExistingFile("keep.txt", [7]);
        var sourceAudio = MemoryFile.CreateStandalone("source.wav", [1, 2, 3, 4]);
        using var cancellation = new CancellationTokenSource();
        root.FileCreated = file =>
        {
            if (file.FileName == "chart.ogkr")
                cancellation.Cancel();
        };
        var attachCalls = 0;
        var coordinator = CreateCoordinator((_, _) =>
        {
            attachCalls++;
            return Task.FromResult(true);
        });
        var plan = EditorProjectCreationPlan.Create(CreateNewSelection(root, sourceAudio));

        var outcome = await coordinator.RunAsync(
            plan,
            progress: null,
            cancellation.Token);

        Assert.True(
            outcome is EditorProjectCreationOutcome.Canceled,
            outcome is EditorProjectCreationOutcome.Failed failure
                ? failure.Exception.ToString()
                : $"Unexpected outcome: {outcome}");
        var canceled = (EditorProjectCreationOutcome.Canceled)outcome;
        Assert.Empty(canceled.RollbackFailures);
        Assert.Equal(0, attachCalls);
        Assert.Equal(new[] { "chart.ogkr", "audio.wav" }, root.DeleteAttempts);
        Assert.Equal(new[] { "keep.txt" }, root.ChildFiles.Select(file => file.FileName));
        Assert.Equal(0, existing.DeleteCount);
        Assert.Equal(1, sourceAudio.DisposeCount);
        Assert.All(root.DeleteTokens, token => Assert.False(token.CanBeCanceled));
    }

    [AvaloniaFact]
    public async Task RollbackDeleteFailure_DoesNotPreventRemainingCreatedFilesFromBeingDeleted()
    {
        using var root = new MemoryDirectory("project")
        {
            FailDeleteFileName = "Song.nyagekiProj"
        };
        var existing = root.AddExistingFile("keep.txt", [7]);
        var sourceAudio = MemoryFile.CreateStandalone("source.wav", [1, 2, 3]);
        var coordinator = CreateCoordinator((_, _) => Task.FromResult(false));
        var plan = EditorProjectCreationPlan.Create(CreateNewSelection(root, sourceAudio));

        var outcome = await coordinator.RunAsync(
            plan,
            progress: null,
            CancellationToken.None);

        var failed = Assert.IsType<EditorProjectCreationOutcome.Failed>(outcome);
        AssertFailureKind(failed, EditorProjectCreationFailureKind.EditorRejected);
        Assert.Single(failed.RollbackFailures);
        Assert.Contains("Song.nyagekiProj", failed.RollbackFailures[0], StringComparison.Ordinal);
        Assert.Equal(
            new[] { "Song.nyagekiProj", "chart.ogkr", "audio.wav" },
            root.DeleteAttempts);
        Assert.Equal(
            new[] { "keep.txt", "Song.nyagekiProj" },
            root.ChildFiles.Select(file => file.FileName));
        Assert.Equal(0, existing.DeleteCount);
        Assert.Equal(1, sourceAudio.DisposeCount);
    }

    [AvaloniaFact]
    public async Task TargetConflict_FailsBeforeAnyFileIsCreatedAndKeepsExistingBytes()
    {
        using var root = new MemoryDirectory("project");
        var existing = root.AddExistingFile("audio.wav", [9, 9, 9]);
        var sourceAudio = MemoryFile.CreateStandalone("source.wav", [1, 2, 3]);
        var selection = CreateNewSelection(root, sourceAudio, audioTargetName: "audio.wav");
        var coordinator = CreateCoordinator((_, _) => Task.FromResult(true));
        var plan = EditorProjectCreationPlan.Create(selection);

        var outcome = await coordinator.RunAsync(
            plan,
            progress: null,
            CancellationToken.None);

        var failed = Assert.IsType<EditorProjectCreationOutcome.Failed>(outcome);
        AssertFailureKind(failed, EditorProjectCreationFailureKind.TargetConflict);
        Assert.Empty(root.CreateAttempts);
        Assert.Empty(root.DeleteAttempts);
        Assert.Equal([9, 9, 9], existing.Content);
        Assert.Equal(0, existing.DeleteCount);
        Assert.Equal(1, sourceAudio.DisposeCount);
    }

    private static EditorProjectCreationCoordinator CreateCoordinator(
        Func<EditorContext, CancellationToken, Task<bool>> attach) =>
        new(new BinaryFumenParserManager(), new StubAudioManager(), attach);

    private static void AssertFailureKind(
        EditorProjectCreationOutcome.Failed failure,
        EditorProjectCreationFailureKind expected) =>
        Assert.True(
            failure.Kind == expected,
            $"Expected {expected}, got {failure.Kind}:{Environment.NewLine}{failure.Exception}");

    private static EditorProjectSetupSelection CreateNewSelection(
        MemoryDirectory root,
        MemoryFile sourceAudio,
        string audioTargetName = "audio.wav") =>
        new()
        {
            ProjectDirectory = root,
            ProjectDirectoryDisplayName = "project",
            ProjectName = "Song",
            ProjectFileName = "Song.nyagekiProj",
            FumenMode = SetupFumenMode.CreateNew,
            AudioFile = sourceAudio,
            NewFumenFileName = "chart.ogkr",
            BaseBpm = 128.5,
            TargetAudioFileName = audioTargetName,
            AudioDuration = TimeSpan.FromMinutes(2),
            AudioPackageKind = SetupAudioPackageKind.OrdinaryAudio,
            SupportsAcb = true,
            AudioRequiresImport = true
        };

    private static EditorProjectSetupSelection CreateExistingSelection(
        MemoryDirectory root,
        MemoryFile audio,
        MemoryFile fumen) =>
        new()
        {
            ProjectDirectory = root,
            ProjectDirectoryDisplayName = "project",
            ProjectName = "Song",
            ProjectFileName = "Song.nyagekiProj",
            FumenMode = SetupFumenMode.Existing,
            AudioFile = audio,
            ExistingFumenFile = fumen,
            ExistingFumenTargetFileName = fumen.FileName,
            TargetAudioFileName = audio.FileName,
            AudioDuration = TimeSpan.FromMinutes(2),
            AudioPackageKind = SetupAudioPackageKind.OrdinaryAudio,
            SupportsAcb = true
        };

    private sealed class BinaryFumenParserManager : IFumenParserManager
    {
        private readonly BinaryFumenCodec codec = new();

        public IFumenSerializable GetSerializer(string saveFilePath) =>
            saveFilePath.EndsWith(".ogkr", StringComparison.OrdinalIgnoreCase) ? codec : null!;

        public IFumenDeserializable GetDeserializer(string loadFilePath) =>
            loadFilePath.EndsWith(".ogkr", StringComparison.OrdinalIgnoreCase) ? codec : null!;

        public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions()
        {
            yield return (codec.FileFormatName, codec.SupportFumenFileExtensions);
        }

        public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions() =>
            GetSerializerDescriptions();
    }

    private sealed class BinaryFumenCodec : IFumenSerializable, IFumenDeserializable
    {
        public string FileFormatName => "Binary test fumen";
        public string[] SupportFumenFileExtensions => [".ogkr"];

        public Task<byte[]> SerializeAsync(OngekiFumen fumen)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(fumen.MetaInfo.BpmDefinition.First);
            writer.Write(fumen.MetaInfo.BpmDefinition.Common);
            writer.Write(fumen.MetaInfo.BpmDefinition.Minimum);
            writer.Write(fumen.MetaInfo.BpmDefinition.Maximum);
            writer.Write(fumen.BpmList.FirstBpm);
            writer.Write(fumen.MetaInfo.ProgJudgeBpm);
            writer.Flush();
            return Task.FromResult(stream.ToArray());
        }

        public Task<OngekiFumen> DeserializeAsync(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var fumen = new OngekiFumen();
            fumen.MetaInfo.BpmDefinition.First = reader.ReadDouble();
            fumen.MetaInfo.BpmDefinition.Common = reader.ReadDouble();
            fumen.MetaInfo.BpmDefinition.Minimum = reader.ReadDouble();
            fumen.MetaInfo.BpmDefinition.Maximum = reader.ReadDouble();
            fumen.BpmList.FirstBpm = reader.ReadDouble();
            fumen.MetaInfo.ProgJudgeBpm = reader.ReadSingle();
            return Task.FromResult(fumen);
        }
    }

    private sealed class StubAudioManager : IAudioManager
    {
        public bool EnableVarspeed => false;
        public float SoundVolume { get; set; }
        public float MusicVolume { get; set; }
        public float MusicSpeed { get; set; } = 1;
        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList =>
            [(".wav", "Wave audio")];

        public Task<ISoundPlayer> LoadSoundAsync(Stream stream) => throw new NotSupportedException();
        public Task<IAudioPlayer> LoadAudioAsync(Stream stream) =>
            Task.FromResult<IAudioPlayer>(new StubAudioPlayer());
        public Task<IAudioPlayer> LoadAudioAsync(Stream acbStream, Stream externalAwbStream) =>
            Task.FromResult<IAudioPlayer>(new StubAudioPlayer());
        public void Dispose()
        {
        }
    }

    private sealed class StubAudioPlayer : IAudioPlayer
    {
        public TimeSpan CurrentTime => TimeSpan.Zero;
        public float Speed { get; set; } = 1;
        public TimeSpan Duration => TimeSpan.FromMinutes(2);
        public bool IsPlaying => false;
        public bool IsAvaliable => true;
        public event PropertyChangedEventHandler? PropertyChanged;
        public event IAudioPlayer.OnPlaybackFinishedFunc? OnPlaybackFinished;
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
        public Task<SampleData> GetSamplesAsync() => throw new NotSupportedException();
        public void Dispose()
        {
        }
    }

    private sealed class MemoryDirectory(string name) : ISimpleDirectory
    {
        private readonly List<MemoryFile> files = [];
        private bool disposed;

        public Action<MemoryFile>? FileCreated { get; set; }
        public string? FailDeleteFileName { get; init; }
        public List<string> CreateAttempts { get; } = [];
        public List<string> DeleteAttempts { get; } = [];
        public List<CancellationToken> DeleteTokens { get; } = [];
        public int DisposeCount { get; private set; }
        public ISimpleDirectory? ParentDictionary => null;
        public ISimpleDirectory[] ChildDictionaries => [];
        public ISimpleFile[] ChildFiles => files.Cast<ISimpleFile>().ToArray();
        public string FullPath => $"memory://{name}";
        public string? LocalPath => null;
        public string DirectoryName => name;
        public bool ExistsDirectory(string dirName) => false;
        public bool ExistsFile(string fileName) =>
            files.Any(file => file.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        public ISimpleFile[] GetFiles(string pattern = "*") => ChildFiles;

        public MemoryFile AddExistingFile(string fileName, byte[] content)
        {
            var file = new MemoryFile(this, fileName, content);
            files.Add(file);
            return file;
        }

        public Task<IReadOnlyList<SimpleDirectoryEntry>> GetEntrySnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<SimpleDirectoryEntry>>(
                files.Select(file => new SimpleDirectoryEntry(file.FileName, false)).ToArray());
        }

        public Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
            string directoryName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ISimpleFile> CreateFileAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            CreateAttempts.Add(fileName);
            if (ExistsFile(fileName))
                throw new IOException($"'{fileName}' already exists.");

            var file = new MemoryFile(this, fileName, []);
            files.Add(file);
            FileCreated?.Invoke(file);
            return Task.FromResult<ISimpleFile>(file);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            DisposeCount++;
            foreach (var file in files.ToArray())
                file.Dispose();
        }

        internal async Task DeleteAsync(MemoryFile file, CancellationToken cancellationToken)
        {
            DeleteAttempts.Add(file.FileName);
            DeleteTokens.Add(cancellationToken);
            if (file.FileName.Equals(FailDeleteFileName, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected delete failure.");
            files.Remove(file);
            await Task.CompletedTask;
        }
    }

    private sealed class MemoryFile : ISimpleFile
    {
        private readonly MemoryDirectory? parent;
        private byte[] content;
        private bool disposed;

        public MemoryFile(MemoryDirectory? parent, string fileName, byte[] content)
        {
            this.parent = parent;
            FileName = fileName;
            this.content = content.ToArray();
        }

        public static MemoryFile CreateStandalone(string fileName, byte[] content) =>
            new(null, fileName, content);

        public byte[] Content => content.ToArray();
        public int DisposeCount { get; private set; }
        public int DeleteCount { get; private set; }
        public ISimpleDirectory? ParentDictionary => parent;
        public string FullPath => parent is null
            ? $"memory://source/{FileName}"
            : $"{parent.FullPath}/{FileName}";
        public string? LocalPath => null;
        public string FileName { get; }
        public long FileLength => content.LongLength;

        public ValueTask<string[]> ReadAllLines()
        {
            ThrowIfDisposed();
            return ValueTask.FromResult(
                Encoding.UTF8.GetString(content).Split(["\r\n", "\n"], StringSplitOptions.None));
        }

        public ValueTask<byte[]> ReadAllBytes()
        {
            ThrowIfDisposed();
            return ValueTask.FromResult(content.ToArray());
        }

        public Task<Stream> OpenRead()
        {
            ThrowIfDisposed();
            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public Task<Stream> OpenWrite() => throw new NotSupportedException();

        public async Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await using var stream = new MemoryStream();
            await writer(stream, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            content = stream.ToArray();
        }

        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (parent is null)
                throw new NotSupportedException();
            DeleteCount++;
            await parent.DeleteAsync(this, cancellationToken);
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            DisposeCount++;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    }
}
