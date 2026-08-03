#nullable enable

using System.Text;
using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public sealed class AvaloniaStorageProviderSimpleFile : ISimpleFile
{
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    private IStorageFile? file;
    private WeakReference<byte[]>? data;

    public AvaloniaStorageProviderSimpleFile(
        ISimpleDirectory parent,
        string fileName,
        long fileLength,
        IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegative(fileLength);

        ParentDictionary = parent;
        FileName = fileName;
        FileLength = fileLength;
        this.file = file;
    }

    public ISimpleDirectory ParentDictionary { get; }

    public string FullPath => Path.Combine(ParentDictionary.FullPath, FileName);

    public string FileName { get; }

    public long FileLength { get; }

    public async ValueTask<byte[]> ReadAllBytes()
    {
        var storageFile = GetStorageFile();
        if (data is not null && data.TryGetTarget(out var cached))
            return cached;

        await using var stream = await storageFile.OpenReadAsync().ConfigureAwait(false);
        var bytes = await ReadToEndAsync(stream, FileLength).ConfigureAwait(false);
        data = new WeakReference<byte[]>(bytes);
        return bytes;
    }

    public async ValueTask<string[]> ReadAllLines()
    {
        var text = Encoding.UTF8.GetString(await ReadAllBytes().ConfigureAwait(false));
        return text.Split(LineSeparators, StringSplitOptions.None);
    }

    public async Task<Stream> OpenRead()
    {
        var stream = await GetStorageFile().OpenReadAsync().ConfigureAwait(false);
        return stream.CanSeek ? stream : new SeekableStream(stream, FileLength);
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

    private static async Task<byte[]> ReadToEndAsync(Stream stream, long expectedLength)
    {
        if (expectedLength > Array.MaxLength)
            throw new IOException($"The file is too large to load into a byte array: {expectedLength} bytes.");

        using var buffer = expectedLength > 0
            ? new MemoryStream(checked((int)expectedLength))
            : new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
