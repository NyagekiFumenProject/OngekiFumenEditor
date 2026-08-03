using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class SimpleFileSystemTests
{
    [AvaloniaFact]
    public async Task LoadFromAvaloniaStorageFolder_NestedTree_ProvidesNavigationContentAndOwnedLifetime()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var chartsPath = temporaryDirectory.CreateDirectory("Charts");
        temporaryDirectory.CreateDirectory(Path.Combine("Charts", "Empty"));
        var songContent = Encoding.UTF8.GetBytes("first\r\nsecond\n");
        await File.WriteAllBytesAsync(Path.Combine(chartsPath, "song.ogkr"), songContent);
        await File.WriteAllBytesAsync(temporaryDirectory.File("readme.txt"), [0x02, 0x03]);

        var storageRoot = await GetStorageFolder(temporaryDirectory.RootPath);
        var root = await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFolder(storageRoot);

        Assert.Null(root.ParentDictionary);
        Assert.Equal(string.Empty, root.DirectoryName);
        Assert.True(root.ExistsDirectory("charts"));
        Assert.True(root.ExistsFile("README.TXT"));

        var charts = SimpleIO.FindDirectory(root, @"CHARTS\.\Empty\..");
        Assert.NotNull(charts);
        Assert.Equal("Charts", charts.DirectoryName);
        Assert.True(SimpleIO.ExistFile(root, "charts/SONG.OGKR"));
        Assert.Equal(
            [Path.Combine("Charts", "song.ogkr")],
            SimpleIO.GetFilePaths(root, "charts", "*.OG?R"));
        Assert.Equal(["first", "second", ""], await SimpleIO.ReadAllLines(root, "charts/song.ogkr"));

        var file = Assert.IsAssignableFrom<ISimpleFile>(SimpleIO.FindFile(root, "charts/song.ogkr"));
        Assert.Equal(songContent, await file.ReadAllBytes());
        await using (var stream = await file.OpenRead())
        {
            Assert.True(stream.CanSeek);
            stream.Seek(-4, SeekOrigin.End);
            var tail = new byte[4];
            await stream.ReadExactlyAsync(tail);
            Assert.Equal(songContent[^4..], tail);
        }

        root.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => file.OpenRead());
    }

    [Fact]
    public void SimpleIO_MissingOrEscapingPath_ReturnsEmptyOrThrowsConsistently()
    {
        using var root = new AvaloniaStorageProviderSimpleDirectory(null, string.Empty);

        Assert.True(SimpleIO.ExistDirectory(root, null));
        Assert.False(SimpleIO.ExistFile(root, null));
        Assert.Null(SimpleIO.FindDirectory(root, ".."));
        Assert.Empty(SimpleIO.GetFiles(root, "missing", "*.txt"));
        Assert.Empty(SimpleIO.GetFilePaths(root, "missing", "*.txt"));
        Assert.Throws<FileNotFoundException>(() =>
        {
            _ = SimpleIO.OpenRead(root, "missing.txt");
        });
        Assert.Throws<FileNotFoundException>(() =>
        {
            _ = SimpleIO.ReadAllLines(root, "missing.txt");
        });
    }

    [AvaloniaFact]
    public async Task LoadFromAvaloniaStorageFolder_PreCanceled_ThrowsOperationCanceledException()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storageRoot = await GetStorageFolder(temporaryDirectory.RootPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFolder(
                storageRoot,
                cancellation.Token));
    }

    [Fact]
    public async Task SeekableStream_NonSeekableSource_ReplaysCachedBytesAndDisposesSource()
    {
        var content = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        var source = new NonSeekableReadStream(content);

        await using (var stream = new SeekableStream(source, content.Length))
        {
            var first = new byte[12];
            await stream.ReadExactlyAsync(first);
            Assert.Equal(content[..12], first);

            stream.Seek(-5, SeekOrigin.Current);
            var overlapping = new byte[10];
            await stream.ReadExactlyAsync(overlapping);
            Assert.Equal(content[7..17], overlapping);

            stream.Seek(-3, SeekOrigin.End);
            var tail = new byte[3];
            Assert.Equal(3, stream.Read(tail));
            Assert.Equal(content[^3..], tail);
            Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        }

        Assert.True(source.IsDisposed);
    }

    private static async Task<IStorageFolder> GetStorageFolder(string path)
    {
        var window = new Window();
        return await window.StorageProvider.TryGetFolderFromPathAsync(path)
            ?? throw new InvalidOperationException($"Unable to create a storage folder for '{path}'.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "OngekiFumenEditor.SimpleFileSystem.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string File(string relativePath)
        {
            return Path.Combine(RootPath, relativePath);
        }

        public string CreateDirectory(string relativePath)
        {
            return Directory.CreateDirectory(Path.Combine(RootPath, relativePath)).FullName;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content, writable: false);

        public bool IsDisposed { get; private set; }

        public override bool CanRead => !IsDisposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            return inner.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                IsDisposed = true;
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
