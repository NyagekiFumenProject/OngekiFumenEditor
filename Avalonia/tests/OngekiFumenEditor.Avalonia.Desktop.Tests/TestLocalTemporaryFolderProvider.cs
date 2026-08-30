#nullable enable

using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests;

/// <summary>
/// Adapts the desktop temporary provider for legacy APIs that require a local path
/// from <see cref="ISimpleFile.FullPath"/> or <see cref="ISimpleDirectory.FullPath"/>.
/// </summary>
internal sealed class TestLocalTemporaryFolderProvider : ITemporaryFolderProvider
{
    private readonly DesktopTemporaryFolderProvider inner;
    private readonly string rootPath;
    private readonly TestLocalSimpleDirectory root;

    public TestLocalTemporaryFolderProvider(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        this.rootPath = Path.GetFullPath(rootPath);
        inner = new DesktopTemporaryFolderProvider(this.rootPath);
        root = new TestLocalSimpleDirectory(this, inner.Root);
    }

    public bool IsAvailable => inner.IsAvailable;

    public ISimpleDirectory Root => root;

    public async Task<ISimpleFile> CreateUniqueFileAsync(
        string prefix = "tempFile",
        string extension = ".dat",
        ISimpleDirectory? parent = null,
        CancellationToken cancellationToken = default)
    {
        var file = await inner.CreateUniqueFileAsync(
            prefix,
            extension,
            Unwrap(parent),
            cancellationToken);
        return Wrap(file);
    }

    public async Task<ISimpleDirectory> CreateUniqueFolderAsync(
        string prefix = "tempFolder",
        ISimpleDirectory? parent = null,
        CancellationToken cancellationToken = default)
    {
        var directory = await inner.CreateUniqueFolderAsync(
            prefix,
            Unwrap(parent),
            cancellationToken);
        return Wrap(directory);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        inner.ClearAsync(cancellationToken);

    private ISimpleDirectory? Unwrap(ISimpleDirectory? directory) =>
        directory is null ? null :
        directory is TestLocalSimpleDirectory localDirectory &&
        ReferenceEquals(localDirectory.Provider, this)
            ? localDirectory.Inner
            : throw new ArgumentException(
                "The parent directory belongs to a different test temporary provider.",
                nameof(directory));

    private ISimpleFile Wrap(ISimpleFile file) => new TestLocalSimpleFile(this, file);

    private TestLocalSimpleDirectory Wrap(ISimpleDirectory directory) =>
        new(this, directory);

    private string GetLocalPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootPath, normalized));
    }

    private sealed class TestLocalSimpleDirectory(
        TestLocalTemporaryFolderProvider provider,
        ISimpleDirectory inner) : ISimpleDirectory
    {
        public TestLocalTemporaryFolderProvider Provider { get; } = provider;

        public ISimpleDirectory Inner { get; } = inner;

        public ISimpleDirectory? ParentDictionary =>
            Inner.ParentDictionary is { } parent ? Provider.Wrap(parent) : null;

        public ISimpleDirectory[] ChildDictionaries =>
            Inner.ChildDictionaries.Select(Provider.Wrap).Cast<ISimpleDirectory>().ToArray();

        public ISimpleFile[] ChildFiles =>
            Inner.ChildFiles.Select(Provider.Wrap).Cast<ISimpleFile>().ToArray();

        public string FullPath => Provider.GetLocalPath(Inner.FullPath);

        public string DirectoryName => Inner.DirectoryName;

        public bool ExistsDirectory(string dirName) => Inner.ExistsDirectory(dirName);

        public bool ExistsFile(string fileName) => Inner.ExistsFile(fileName);

        public ISimpleFile[] GetFiles(string pattern = "*") =>
            Inner.GetFiles(pattern).Select(Provider.Wrap).Cast<ISimpleFile>().ToArray();

        public async Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
            string directoryName,
            CancellationToken cancellationToken = default) =>
            Provider.Wrap(await Inner.GetOrCreateDirectoryAsync(directoryName, cancellationToken));

        public async Task<ISimpleFile> CreateFileAsync(
            string fileName,
            CancellationToken cancellationToken = default) =>
            Provider.Wrap(await Inner.CreateFileAsync(fileName, cancellationToken));

        public Task DeleteAsync(CancellationToken cancellationToken = default) =>
            Inner.DeleteAsync(cancellationToken);

        public void Dispose() => Inner.Dispose();
    }

    private sealed class TestLocalSimpleFile(
        TestLocalTemporaryFolderProvider provider,
        ISimpleFile inner) : ISimpleFile
    {
        public TestLocalTemporaryFolderProvider Provider { get; } = provider;

        public ISimpleFile Inner { get; } = inner;

        public ISimpleDirectory? ParentDictionary =>
            Inner.ParentDictionary is { } parent ? Provider.Wrap(parent) : null;

        public string FullPath => Provider.GetLocalPath(Inner.FullPath);

        public string FileName => Inner.FileName;

        public long FileLength => Inner.FileLength;

        public ValueTask<string[]> ReadAllLines() => Inner.ReadAllLines();

        public ValueTask<byte[]> ReadAllBytes() => Inner.ReadAllBytes();

        public Task<Stream> OpenRead() => Inner.OpenRead();

        public Task<Stream> OpenWrite() => Inner.OpenWrite();

        public Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default) =>
            Inner.WriteAsync(writer, cancellationToken);

        public Task DeleteAsync(CancellationToken cancellationToken = default) =>
            Inner.DeleteAsync(cancellationToken);

        public void Dispose() => Inner.Dispose();
    }
}
