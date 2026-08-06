#nullable enable

#if ENABLE_SVG_PREFAB_OBJECTS
namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public sealed class ProjectResourceSimpleFile : ISimpleFile
{
    private byte[]? content;

    public ProjectResourceSimpleFile(ISimpleFile projectFile, string locator, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(projectFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(locator);
        ArgumentNullException.ThrowIfNull(content);
        ParentDictionary = projectFile.ParentDictionary;
        FullPath = locator;
        FileName = projectFile.FileName;
        this.content = content.ToArray();
    }

    public ISimpleDirectory? ParentDictionary { get; }

    public string FullPath { get; }

    public string? LocalPath => null;

    public string FileName { get; }

    public long FileLength => GetContent().LongLength;

    public ValueTask<string[]> ReadAllLines()
    {
        var text = System.Text.Encoding.UTF8.GetString(GetContent());
        return ValueTask.FromResult(text.Split(["\r\n", "\n"], StringSplitOptions.None));
    }

    public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(GetContent().ToArray());

    public Task<Stream> OpenRead() =>
        Task.FromResult<Stream>(new MemoryStream(GetContent(), writable: false));

    public Task<Stream> OpenWrite() =>
        throw new NotSupportedException("Imported project resources are immutable through this handle.");

    public void Dispose()
    {
        content = null;
    }

    private byte[] GetContent() =>
        content ?? throw new ObjectDisposedException(nameof(ProjectResourceSimpleFile));
}
#endif
