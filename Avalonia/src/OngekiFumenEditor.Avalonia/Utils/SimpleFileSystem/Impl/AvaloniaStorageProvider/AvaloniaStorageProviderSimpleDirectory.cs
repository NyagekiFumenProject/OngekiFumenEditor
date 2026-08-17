#nullable enable

using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public sealed class AvaloniaStorageProviderSimpleDirectory : ISimpleDirectory, IBookmarkableSimpleFileSystemItem
{
    private readonly List<AvaloniaStorageProviderSimpleDirectory> directories = [];
    private readonly List<AvaloniaStorageProviderSimpleFile> files = [];
    private IStorageFolder? storageFolder;
    private bool isDisposed;

    public AvaloniaStorageProviderSimpleDirectory(ISimpleDirectory? parent, string name)
        : this(parent, name, null)
    {
    }

    internal AvaloniaStorageProviderSimpleDirectory(
        ISimpleDirectory? parent,
        string name,
        IStorageFolder? storageFolder)
    {
        ArgumentNullException.ThrowIfNull(name);
        ParentDictionary = parent;
        DirectoryName = name;
        LocalPath = storageFolder?.TryGetLocalPath();
        this.storageFolder = storageFolder;
    }

    public ISimpleDirectory? ParentDictionary { get; }

    public ISimpleDirectory[] ChildDictionaries => [.. directories];

    public ISimpleFile[] ChildFiles => [.. files];

    public string FullPath => Path.Combine(ParentDictionary?.FullPath ?? string.Empty, DirectoryName);

    public string? LocalPath { get; }

    public string DirectoryName { get; set; }

    bool IBookmarkableSimpleFileSystemItem.CanBookmark => GetStorageFolder().CanBookmark;

    Task<string?> IBookmarkableSimpleFileSystemItem.SaveBookmarkAsync() =>
        GetStorageFolder().SaveBookmarkAsync();

    public bool ExistsDirectory(string dirName)
    {
        return directories.Any(x => x.DirectoryName.Equals(dirName, StringComparison.OrdinalIgnoreCase));
    }

    public bool ExistsFile(string fileName)
    {
        return files.Any(x => x.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public ISimpleFile[] GetFiles(string pattern = "*")
    {
        var regex = SimpleIO.WildcardToRegex(pattern);
        return [.. files.Where(file => regex.IsMatch(file.FileName))];
    }

    public async Task<IReadOnlyList<SimpleDirectoryEntry>> GetEntrySnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        var entries = new List<SimpleDirectoryEntry>();
        await foreach (var item in GetStorageFolder().GetItemsAsync().ConfigureAwait(false))
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(new SimpleDirectoryEntry(item.Name, item is IStorageFolder));
            }
            finally
            {
                item.Dispose();
            }
        }

        return entries;
    }

    public async Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
        string directoryName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryName);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = directories
            .Where(x => x.DirectoryName.Equals(directoryName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 1)
            return matches[0];
        if (matches.Length > 1)
            throw new IOException($"Directory '{directoryName}' has a case-insensitive name conflict.");

        var folder = await GetStorageFolder().CreateFolderAsync(directoryName).ConfigureAwait(false)
            ?? throw new IOException($"Unable to create directory '{directoryName}'.");
        var directory = new AvaloniaStorageProviderSimpleDirectory(this, folder.Name, folder);
        directories.Add(directory);
        return directory;
    }

    public async Task<ISimpleFile> CreateFileAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();
        if (files.Any(x => x.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            throw new IOException($"File '{fileName}' already exists.");

        var storageFile = await GetStorageFolder().CreateFileAsync(fileName).ConfigureAwait(false)
            ?? throw new IOException($"Unable to create file '{fileName}'.");
        try
        {
            var properties = await storageFile.GetBasicPropertiesAsync().ConfigureAwait(false);
            var file = new AvaloniaStorageProviderSimpleFile(
                this,
                storageFile.Name,
                properties.Size is { } size ? checked((long)size) : 0,
                storageFile);
            files.Add(file);
            return file;
        }
        catch
        {
            try
            {
                await storageFile.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // Preserve the metadata exception; the provider may not support deleting the new file.
            }

            storageFile.Dispose();
            throw;
        }
    }

    public void AddDirectory(AvaloniaStorageProviderSimpleDirectory directory)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(directory);

        directories.Add(directory);
    }

    public void AddFile(AvaloniaStorageProviderSimpleFile file)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(file);

        files.Add(file);
    }

    internal void RemoveFile(AvaloniaStorageProviderSimpleFile file)
    {
        files.Remove(file);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        foreach (var childDirectory in directories)
            childDirectory.Dispose();
        foreach (var childFile in files)
            childFile.Dispose();

        storageFolder?.Dispose();
        storageFolder = null;
    }

    public override string ToString()
    {
        return $"Directory: {FullPath}, ChildDirsCount: {directories.Count}, ChildFilesCount: {files.Count}";
    }

    private IStorageFolder GetStorageFolder()
    {
        return storageFolder ?? throw new ObjectDisposedException(nameof(AvaloniaStorageProviderSimpleDirectory));
    }
}
