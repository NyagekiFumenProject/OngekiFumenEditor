#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;

public sealed class LocalSimpleFile : ISimpleFile
{
    private static readonly string[] LineSeparators = ["\r\n", "\n"];
    private bool isDisposed;

    public LocalSimpleFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FullPath = Path.GetFullPath(filePath);
        FileName = Path.GetFileName(FullPath);
    }

    ISimpleDirectory? parentDirectory = default;

    public ISimpleDirectory? ParentDictionary => parentDirectory ??= TryCreateParentDictionary();

    private ISimpleDirectory? TryCreateParentDictionary()
    {
        var parentDir = Path.GetDirectoryName(FullPath);
        //todo: create LocalSimpleDirectory
        return default;
    }

    public string FullPath { get; }
    public string? LocalPath => FullPath;
    public string FileName { get; }

    public long FileLength
    {
        get
        {
            ThrowIfDisposed();
            return File.Exists(FullPath) ? new FileInfo(FullPath).Length : 0;
        }
    }

    public async ValueTask<string[]> ReadAllLines()
    {
        var text = Encoding.UTF8.GetString(await ReadAllBytes());
        return text.Split(LineSeparators, StringSplitOptions.None);
    }

    public async ValueTask<byte[]> ReadAllBytes()
    {
        ThrowIfDisposed();
        return await File.ReadAllBytesAsync(FullPath).ConfigureAwait(false);
    }

    public Task<Stream> OpenRead()
    {
        ThrowIfDisposed();
        return Task.FromResult<Stream>(new FileStream(
            FullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public Task<Stream> OpenWrite()
    {
        ThrowIfDisposed();
        return Task.FromResult<Stream>(new FileStream(
            FullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return SimpleFileWriteTransaction.WriteLocalAsync(FullPath, writer, cancellationToken);
    }

    public void Dispose()
    {
        isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }
}
