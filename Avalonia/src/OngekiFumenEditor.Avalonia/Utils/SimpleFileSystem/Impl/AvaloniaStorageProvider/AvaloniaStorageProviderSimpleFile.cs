#nullable enable

using System.Text;
using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public sealed class AvaloniaStorageProviderSimpleFile : ISimpleFile, IBookmarkableSimpleFileSystemItem
{
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    private readonly string? standaloneFullPath;
    private IStorageFile? file;
    private WeakReference<byte[]>? data;

    public AvaloniaStorageProviderSimpleFile(
        ISimpleDirectory? parent,
        string fileName,
        long fileLength,
        IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegative(fileLength);

        ParentDictionary = parent;
        FileName = fileName;
        FileLength = fileLength;
        standaloneFullPath = parent is null ? file.Path.ToString() : null;
        this.file = file;
    }

    public ISimpleDirectory? ParentDictionary { get; }

    public string FullPath => ParentDictionary is null
        ? standaloneFullPath!
        : Path.Combine(ParentDictionary.FullPath, FileName);

    public string FileName { get; }

    public long FileLength { get; private set; }

    bool IBookmarkableSimpleFileSystemItem.CanBookmark => GetStorageFile().CanBookmark;

    Task<string?> IBookmarkableSimpleFileSystemItem.SaveBookmarkAsync() =>
        GetStorageFile().SaveBookmarkAsync();

    public async ValueTask<byte[]> ReadAllBytes()
    {
        var storageFile = GetStorageFile();
        if (data is not null && data.TryGetTarget(out var cached))
            return cached;

        await using var stream = await storageFile.OpenReadAsync();
        if (stream.CanSeek)
            FileLength = stream.Length;
        else
            await RefreshFileLength(storageFile);
        var bytes = await ReadToEndAsync(stream, FileLength);
        FileLength = bytes.LongLength;
        data = new WeakReference<byte[]>(bytes);
        return bytes;
    }

    public async ValueTask<string[]> ReadAllLines()
    {
        var text = Encoding.UTF8.GetString(await ReadAllBytes());
        return text.Split(LineSeparators, StringSplitOptions.None);
    }

    public async Task<Stream> OpenRead()
    {
        var storageFile = GetStorageFile();
        var stream = await storageFile.OpenReadAsync();
        if (stream.CanSeek)
        {
            FileLength = stream.Length;
            return stream;
        }

        try
        {
            await RefreshFileLength(storageFile);
            return new SeekableStream(stream, FileLength);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public async Task<Stream> OpenWrite()
    {
        var stream = await GetStorageFile().OpenWriteAsync();
        try
        {
            data = null;
            if (stream.CanSeek)
            {
                stream.Position = 0;
                try
                {
                    stream.SetLength(0);
                    FileLength = 0;
                }
                catch (NotSupportedException)
                {
                    // Some providers expose a seekable stream without supporting truncation.
                }
            }

            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var storageFile = GetStorageFile();
        cancellationToken.ThrowIfCancellationRequested();
        await storageFile.DeleteAsync();
        if (ParentDictionary is AvaloniaStorageProviderSimpleDirectory parent)
            parent.RemoveFile(this);
        Dispose();
    }

    public async Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var storageFile = GetStorageFile();

        long fileLength;
        cancellationToken.ThrowIfCancellationRequested();
        data = null;
        try
        {
            long? streamLength;
            await using (var stream = await storageFile.OpenWriteAsync())
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                    try
                    {
                        stream.SetLength(0);
                    }
                    catch (NotSupportedException)
                    {
                        // Some providers expose a seekable stream without supporting truncation.
                    }
                }

                await writer(stream, cancellationToken);
                await stream.FlushAsync(CancellationToken.None);
                streamLength = stream.CanSeek ? stream.Length : null;
            }

            fileLength = await GetFileLength(storageFile, streamLength);
        }
        catch
        {
            try
            {
                await RefreshFileLength(storageFile);
            }
            catch
            {
                // The original write exception is more useful than a metadata refresh failure.
            }

            throw;
        }

        data = null;
        FileLength = fileLength;
    }

    public void Dispose()
    {
        var storageFile = Interlocked.Exchange(ref file, null);
        storageFile?.Dispose();
        data = null;
    }

    public override string ToString()
    {
        return $"File: {FullPath}, Length: {FileLength}";
    }

    private IStorageFile GetStorageFile()
    {
        return file ?? throw new ObjectDisposedException(nameof(AvaloniaStorageProviderSimpleFile));
    }

    private async Task RefreshFileLength(IStorageFile storageFile)
    {
        FileLength = await GetFileLength(storageFile);
    }

    private static async Task<long> GetFileLength(IStorageFile storageFile, long? fallback = null)
    {
        var properties = await storageFile.GetBasicPropertiesAsync();
        return properties.Size is { } size
            ? checked((long)size)
            : fallback ?? 0;
    }

    private static async Task CommitReplacementThroughProviderAsync(
        IStorageFile storageFile,
        string temporaryPath,
        long expectedLength)
    {
        await using var source = new FileStream(
            temporaryPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var target = await storageFile.OpenWriteAsync();
        if (target.CanSeek)
        {
            target.Position = 0;
            target.SetLength(0);
        }

        await source.CopyToAsync(target, CancellationToken.None);
        if (target.CanSeek)
            target.SetLength(expectedLength);
        await target.FlushAsync(CancellationToken.None);
    }

    private static async Task<byte[]> ReadToEndAsync(Stream stream, long expectedLength)
    {
        if (expectedLength > Array.MaxLength)
            throw new IOException($"The file is too large to load into a byte array: {expectedLength} bytes.");

        using var buffer = expectedLength > 0
            ? new MemoryStream(checked((int)expectedLength))
            : new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
