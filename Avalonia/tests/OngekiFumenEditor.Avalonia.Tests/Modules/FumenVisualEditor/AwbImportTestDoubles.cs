#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

internal sealed class StubAwbFile : ISimpleFile
{
    private byte[] content;

    public StubAwbFile(string fileName, byte[]? initialContent = null, StubAwbDirectory? parent = null)
    {
        FileName = fileName;
        content = initialContent ?? [];
        FullPath = parent is null ? $"memory://{fileName}" : $"{parent.FullPath}/{fileName}";
        ParentDictionary = parent;
    }

    public ISimpleDirectory? ParentDictionary { get; set; }

    public string FullPath { get; }


    public string FileName { get; }

    public long FileLength => content.LongLength;

    public int DeleteCallCount { get; private set; }

    public ValueTask<string[]> ReadAllLines()
    {
        var text = System.Text.Encoding.UTF8.GetString(content);
        return ValueTask.FromResult(text.Split(["\r\n", "\n"], StringSplitOptions.None));
    }

    public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult((byte[])content.Clone());

    public Task<Stream> OpenRead() =>
        Task.FromResult<Stream>(new MemoryStream(content, writable: false));

    public Task<Stream> OpenWrite() => throw new NotSupportedException();

    public Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return CommitAsync(writer, cancellationToken);
    }

    private async Task CommitAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await writer(buffer, cancellationToken);
        content = buffer.ToArray();
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCallCount++;
        if (ParentDictionary is StubAwbDirectory directory)
            directory.Remove(this);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

internal sealed class StubAwbDirectory(string name) : ISimpleDirectory
{
    private readonly List<StubAwbFile> files = [];

    public ISimpleDirectory? ParentDictionary => null;

    public ISimpleDirectory[] ChildDictionaries => [];

    public ISimpleFile[] ChildFiles => files.ToArray();

    public string FullPath => $"memory://{name}";


    public string DirectoryName => name;

    public bool ExistsDirectory(string dirName) => false;

    public bool ExistsFile(string fileName) =>
        files.Any(file => file.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    public ISimpleFile[] GetFiles(string pattern = "*") => files.ToArray();

    public Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
        string directoryName,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ISimpleFile> CreateFileAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ExistsFile(fileName))
            throw new IOException($"A file already exists at '{fileName}'.");
        var file = new StubAwbFile(fileName, [], this);
        files.Add(file);
        return Task.FromResult<ISimpleFile>(file);
    }

    public StubAwbFile Add(string fileName, byte[] content)
    {
        var file = new StubAwbFile(fileName, content, this);
        files.Add(file);
        return file;
    }

    public void Remove(StubAwbFile file)
    {
        files.RemoveAll(candidate => ReferenceEquals(candidate, file));
    }

    public void Dispose()
    {
    }
}

internal sealed class BrokenOpenFile(string fileName, byte[] content) : ISimpleFile
{
    public ISimpleDirectory? ParentDictionary => null;
    public string FullPath => $"memory://{fileName}";
    public string FileName => fileName;
    public long FileLength => content.LongLength;
    public ValueTask<string[]> ReadAllLines() => ValueTask.FromResult(Array.Empty<string>());
    public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult((byte[])content.Clone());

    public Task<Stream> OpenRead() => throw new IOException("The source stream cannot be opened.");

    public Task<Stream> OpenWrite() => throw new NotSupportedException();
    public Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public void Dispose()
    {
    }
}
