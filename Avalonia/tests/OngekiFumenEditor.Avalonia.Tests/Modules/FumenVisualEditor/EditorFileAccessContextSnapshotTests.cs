using System.Text;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorFileAccessContextSnapshotTests
{
    [Fact]
    public void Serialize_RoundTripsAllBookmarks()
    {
        var snapshot = new EditorFileAccessContextSnapshot
        {
            ProjectDirectoryBookmark = "dir-bmk",
            AdditionDirectoryBookmarks = ["add1", "add2"],
            ProjectFileBookmark = "proj-bmk",
            FumenFileBookmark = "fumen-bmk",
            AudioFileBookmark = "audio-bmk"
        };

        var ok = EditorFileAccessContextSnapshot.TryDeserialize(snapshot.Serialize(), out var restored);

        Assert.True(ok);
        Assert.NotNull(restored);
        Assert.Equal("dir-bmk", restored!.ProjectDirectoryBookmark);
        Assert.Equal(new[] { "add1", "add2" }, restored.AdditionDirectoryBookmarks);
        Assert.Equal("proj-bmk", restored.ProjectFileBookmark);
        Assert.Equal("fumen-bmk", restored.FumenFileBookmark);
        Assert.Equal("audio-bmk", restored.AudioFileBookmark);
    }

    [Fact]
    public void TryDeserialize_NullableProjectFile_PreservesOptionalBookmark()
    {
        var snapshot = new EditorFileAccessContextSnapshot
        {
            ProjectDirectoryBookmark = "dir-bmk",
            AdditionDirectoryBookmarks = [],
            ProjectFileBookmark = null,
            FumenFileBookmark = "fumen-bmk",
            AudioFileBookmark = "audio-bmk"
        };

        var ok = EditorFileAccessContextSnapshot.TryDeserialize(snapshot.Serialize(), out var restored);

        Assert.True(ok);
        Assert.NotNull(restored);
        Assert.Null(restored!.ProjectFileBookmark);
    }

    [Fact]
    public void TryDeserialize_MissingRequiredBookmark_ReturnsFalse()
    {
        var snapshot = new EditorFileAccessContextSnapshot
        {
            ProjectDirectoryBookmark = "dir-bmk",
            AdditionDirectoryBookmarks = [],
            ProjectFileBookmark = null,
            FumenFileBookmark = "",
            AudioFileBookmark = "audio-bmk"
        };

        var ok = EditorFileAccessContextSnapshot.TryDeserialize(snapshot.Serialize(), out var restored);

        Assert.False(ok);
    }

    [Fact]
    public void TryDeserialize_CorruptPayload_ReturnsFalse()
    {
        var ok = EditorFileAccessContextSnapshot.TryDeserialize([0x7b, 0x62, 0x61, 0x64], out var restored);

        Assert.False(ok);
        Assert.Null(restored);
    }

    [Fact]
    public async Task TryLoadFromContextAsync_InvalidDescriptor_DisposesContextAndFiles()
    {
        var projectFile = new TrackingFile("project.nyagekiProj", [0x7b, 0x62, 0x61, 0x64]);
        var fumenFile = new TrackingFile("fumen.ogkr", [0x00]);
        var audioFile = new TrackingFile("audio.wav", [0x00]);
        var context = new EditorFileAccessContext
        {
            ProjectFile = projectFile,
            FumenFile = fumenFile,
            AudioFile = audioFile
        };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            EditorProjectDataUtils.TryLoadFromContextAsync(context));

        Assert.Equal(1, projectFile.DisposeCount);
        Assert.Equal(1, fumenFile.DisposeCount);
        Assert.Equal(1, audioFile.DisposeCount);
    }

    [Fact]
    public async Task TryLoadFromContextAsync_MissingProjectFile_ThrowsAndDisposesContext()
    {
        var fumenFile = new TrackingFile("fumen.ogkr", [0x00]);
        var audioFile = new TrackingFile("audio.wav", [0x00]);
        var context = new EditorFileAccessContext
        {
            ProjectFile = null,
            FumenFile = fumenFile,
            AudioFile = audioFile
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EditorProjectDataUtils.TryLoadFromContextAsync(context));

        Assert.Equal(1, fumenFile.DisposeCount);
        Assert.Equal(1, audioFile.DisposeCount);
    }

    private sealed class TrackingFile : ISimpleFile
    {
        private readonly byte[] content;
        private bool isDisposed;

        public TrackingFile(string fileName, byte[] content)
        {
            FileName = fileName;
            this.content = content;
        }

        public int DisposeCount { get; private set; }
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => $"memory://{FileName}";
        public string? LocalPath => null;
        public string FileName { get; }
        public long FileLength => content.LongLength;

        public ValueTask<string[]> ReadAllLines()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return ValueTask.FromResult(Encoding.UTF8.GetString(content).Split(["\r\n", "\n"], StringSplitOptions.None));
        }

        public ValueTask<byte[]> ReadAllBytes()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return ValueTask.FromResult(content.ToArray());
        }

        public Task<Stream> OpenRead()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public Task<Stream> OpenWrite() => throw new NotSupportedException();

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            DisposeCount++;
        }
    }
}
