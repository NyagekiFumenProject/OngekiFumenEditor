using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
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
        Assert.Equal(Path.GetFullPath(temporaryDirectory.RootPath), root.LocalPath);
        Assert.True(root.ExistsDirectory("charts"));
        Assert.True(root.ExistsFile("README.TXT"));

        var charts = SimpleIO.FindDirectory(root, @"CHARTS\.\Empty\..");
        Assert.NotNull(charts);
        Assert.Equal("Charts", charts.DirectoryName);
        Assert.Equal(Path.GetFullPath(chartsPath), charts.LocalPath);
        Assert.True(SimpleIO.ExistFile(root, "charts/SONG.OGKR"));
        Assert.Equal(
            [Path.Combine("Charts", "song.ogkr")],
            SimpleIO.GetFilePaths(root, "charts", "*.OG?R"));
        Assert.Equal(["first", "second", ""], await SimpleIO.ReadAllLines(root, "charts/song.ogkr"));

        var file = Assert.IsAssignableFrom<ISimpleFile>(SimpleIO.FindFile(root, "charts/song.ogkr"));
        Assert.Equal(Path.GetFullPath(Path.Combine(chartsPath, "song.ogkr")), file.LocalPath);
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
        await Assert.ThrowsAsync<ObjectDisposedException>(() => file.OpenWrite());
    }

    [AvaloniaFact]
    public async Task LoadRootFromAvaloniaStorageFolder_DoesNotRecursivelyEnumerateSelectedDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        temporaryDirectory.CreateDirectory("Nested");
        await File.WriteAllTextAsync(temporaryDirectory.File("root.txt"), "content");

        var storageRoot = await GetStorageFolder(temporaryDirectory.RootPath);
        using var root = AvaloniaStorageProviderFileSystemBuilder
            .LoadRootFromAvaloniaStorageFolder(storageRoot);

        Assert.Equal(Path.GetFullPath(temporaryDirectory.RootPath), root.LocalPath);
        Assert.Empty(root.ChildDictionaries);
        Assert.Empty(root.ChildFiles);
    }

    [AvaloniaFact]
    public async Task LoadFromAvaloniaStorageFile_StandaloneFile_ProvidesIdentityContentAndOwnedLifetime()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var firstDirectory = temporaryDirectory.CreateDirectory("First");
        var secondDirectory = temporaryDirectory.CreateDirectory("Second");
        var filePath = Path.Combine(firstDirectory, "standalone.ogkr");
        var secondFilePath = Path.Combine(secondDirectory, "standalone.ogkr");
        var content = Encoding.UTF8.GetBytes("standalone content");
        await File.WriteAllBytesAsync(filePath, content);
        await File.WriteAllBytesAsync(secondFilePath, "other content"u8.ToArray());

        var storageFile = await GetStorageFile(filePath);
        var expectedFullPath = storageFile.Path.ToString();
        var file = await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFile(storageFile);
        var secondStorageFile = await GetStorageFile(secondFilePath);
        var expectedSecondFullPath = secondStorageFile.Path.ToString();
        var secondFile = await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFile(secondStorageFile);

        Assert.Null(file.ParentDictionary);
        Assert.Equal("standalone.ogkr", file.FileName);
        Assert.Equal(expectedFullPath, file.FullPath);
        Assert.Equal(expectedSecondFullPath, secondFile.FullPath);
        Assert.NotEqual(file.FullPath, secondFile.FullPath);
        Assert.Equal(Path.GetFullPath(filePath), file.LocalPath);
        Assert.Equal(content.LongLength, file.FileLength);
        Assert.Equal(content, await file.ReadAllBytes());

        file.Dispose();
        secondFile.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => file.OpenRead());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => file.OpenWrite());
    }

    [AvaloniaFact]
    public async Task WriteAsync_ExistingLocalProviderFile_CommitsInvalidatesCacheAndRefreshesLength()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.File("output.ogkr");
        var original = Encoding.UTF8.GetBytes("original content that is longer");
        var replacement = Encoding.UTF8.GetBytes("new content");
        await File.WriteAllBytesAsync(filePath, original);

        var storageFile = await GetStorageFile(filePath);
        using var file = await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFile(storageFile);
        Assert.Equal(original, await file.ReadAllBytes());

        await file.WriteAsync(async (stream, cancellationToken) =>
        {
            await stream.WriteAsync(replacement, cancellationToken);
        });

        Assert.Equal(replacement, await File.ReadAllBytesAsync(filePath));
        Assert.Equal(replacement, await file.ReadAllBytes());
        Assert.Equal(replacement.LongLength, file.FileLength);
        Assert.Equal(["output.ogkr"], Directory.GetFiles(temporaryDirectory.RootPath).Select(Path.GetFileName));
    }

    [AvaloniaFact]
    public async Task WriteAsync_WriterThrows_PreservesLocalProviderTargetCacheAndDeletesTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.File("output.ogkr");
        var original = Encoding.UTF8.GetBytes("original content");
        await File.WriteAllBytesAsync(filePath, original);

        var storageFile = await GetStorageFile(filePath);
        using var file = await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFile(storageFile);
        var cached = await file.ReadAllBytes();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync("partial replacement"u8.ToArray(), cancellationToken);
                throw new InvalidOperationException("writer failed");
            }));

        Assert.Equal("writer failed", exception.Message);
        Assert.Equal(original, await File.ReadAllBytesAsync(filePath));
        Assert.Same(cached, await file.ReadAllBytes());
        Assert.Equal(original.LongLength, file.FileLength);
        Assert.Equal(["output.ogkr"], Directory.GetFiles(temporaryDirectory.RootPath).Select(Path.GetFileName));
    }

    [Fact]
    public async Task WriteAsync_WriterCancels_PreservesLocalTargetAndDeletesTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.File("output.ogkr");
        var original = Encoding.UTF8.GetBytes("original content");
        await File.WriteAllBytesAsync(filePath, original);
        using var file = new LocalSimpleFile(filePath);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync("partial replacement"u8.ToArray(), cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellation.Token));

        Assert.Equal(original, await File.ReadAllBytesAsync(filePath));
        Assert.Equal(["output.ogkr"], Directory.GetFiles(temporaryDirectory.RootPath).Select(Path.GetFileName));
    }

    [Fact]
    public async Task WriteAsync_WriterCompletesBeforeCancellation_CommitsLocalTarget()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.File("output.ogkr");
        await File.WriteAllTextAsync(filePath, "original content");
        using var file = new LocalSimpleFile(filePath);
        using var cancellation = new CancellationTokenSource();
        var replacement = Encoding.UTF8.GetBytes("replacement");

        await file.WriteAsync(async (stream, cancellationToken) =>
        {
            await stream.WriteAsync(replacement, cancellationToken);
            cancellation.Cancel();
        }, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(replacement, await File.ReadAllBytesAsync(filePath));
        Assert.Equal(replacement.LongLength, file.FileLength);
        Assert.Equal(["output.ogkr"], Directory.GetFiles(temporaryDirectory.RootPath).Select(Path.GetFileName));
    }

    [Fact]
    public async Task WriteAsync_NonLocalWriterCompletesBeforeCancellation_FlushesWithoutCancellation()
    {
        var providerFile = new NonLocalWritableSimpleFile();
        using ISimpleFile file = providerFile;
        using var cancellation = new CancellationTokenSource();
        var replacement = Encoding.UTF8.GetBytes("replacement");

        await file.WriteAsync(async (stream, cancellationToken) =>
        {
            await stream.WriteAsync(replacement, cancellationToken);
            cancellation.Cancel();
        }, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(CancellationToken.None, providerFile.FlushCancellationToken);
        Assert.Equal(replacement, providerFile.Content);
    }

    [Fact]
    public void SimpleIO_MissingOrEscapingPath_ReturnsEmptyOrThrowsConsistently()
    {
        using var root = new AvaloniaStorageProviderSimpleDirectory(null, string.Empty);

        Assert.Null(root.LocalPath);
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
    public async Task LoadFromAvaloniaStorageFile_PreCanceled_DisposesStorageFileAndThrows()
    {
        var storageFile = DispatchProxy.Create<IStorageFile, TrackingStorageFileProxy>();
        var tracker = (TrackingStorageFileProxy)(object)storageFile;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFile(
                storageFile,
                cancellation.Token));
        Assert.True(tracker.IsDisposed);
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

    private static async Task<IStorageFile> GetStorageFile(string path)
    {
        var window = new Window();
        return await window.StorageProvider.TryGetFileFromPathAsync(path)
            ?? throw new InvalidOperationException($"Unable to create a storage file for '{path}'.");
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

    private class TrackingStorageFileProxy : DispatchProxy
    {
        public bool IsDisposed { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IDisposable.Dispose))
            {
                IsDisposed = true;
                return null;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class NonLocalWritableSimpleFile : ISimpleFile
    {
        private readonly FlushTrackingStream stream = new();

        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => "provider://container/output.ogkr";
        public string? LocalPath => null;
        public string FileName => "output.ogkr";
        public long FileLength => stream.Length;
        public CancellationToken? FlushCancellationToken => stream.FlushCancellationToken;
        public byte[] Content => stream.ToArray();

        public ValueTask<string[]> ReadAllLines() => throw new NotSupportedException();
        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Content);
        public Task<Stream> OpenRead() => throw new NotSupportedException();
        public Task<Stream> OpenWrite() => Task.FromResult<Stream>(stream);

        public void Dispose()
        {
            stream.Dispose();
        }
    }

    private sealed class FlushTrackingStream : MemoryStream
    {
        public CancellationToken? FlushCancellationToken { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
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
