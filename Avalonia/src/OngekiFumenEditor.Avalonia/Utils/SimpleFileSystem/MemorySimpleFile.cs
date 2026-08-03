#nullable enable

using System.Text;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public sealed class MemorySimpleFile : ISimpleFile
{
    private static readonly string[] LineSeparators = ["\r\n", "\n"];
    private byte[]? data;

    public MemorySimpleFile(string fileName, string fullPath, byte[] data, string? localPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(data);

        FileName = fileName;
        FullPath = fullPath;
        LocalPath = localPath;
        this.data = data;
    }

    public ISimpleDirectory? ParentDictionary => null;
    public string FullPath { get; }
    public string? LocalPath { get; }
    public string FileName { get; }
    public long FileLength => GetData().LongLength;

    public ValueTask<string[]> ReadAllLines()
    {
        var text = Encoding.UTF8.GetString(GetData());
        return ValueTask.FromResult(text.Split(LineSeparators, StringSplitOptions.None));
    }

    public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(GetData());

    public Task<Stream> OpenRead() =>
        Task.FromResult<Stream>(new MemoryStream(GetData(), writable: false));

    public Task<Stream> OpenWrite() =>
        throw new NotSupportedException("MemorySimpleFile is read-only.");

    public Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("MemorySimpleFile is read-only.");

    public void Dispose()
    {
        data = null;
    }

    private byte[] GetData() =>
        data ?? throw new ObjectDisposedException(nameof(MemorySimpleFile));
}
