#nullable enable

using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public sealed class AvaloniaStorageProviderSimpleDirectory : ISimpleDirectory
{
    private readonly Dictionary<string, AvaloniaStorageProviderSimpleDirectory> directories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AvaloniaStorageProviderSimpleFile> files =
        new(StringComparer.OrdinalIgnoreCase);
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
        this.storageFolder = storageFolder;
    }

    public ISimpleDirectory? ParentDictionary { get; }

    public ISimpleDirectory[] ChildDictionaries => [.. directories.Values];

    public ISimpleFile[] ChildFiles => [.. files.Values];

    public string FullPath => Path.Combine(ParentDictionary?.FullPath ?? string.Empty, DirectoryName);

    public string DirectoryName { get; set; }

    public bool ExistsDirectory(string dirName)
    {
        return directories.ContainsKey(dirName);
    }

    public bool ExistsFile(string fileName)
    {
        return files.ContainsKey(fileName);
    }

    public ISimpleFile[] GetFiles(string pattern = "*")
    {
        var regex = SimpleIO.WildcardToRegex(pattern);
        return [.. files.Where(pair => regex.IsMatch(pair.Key)).Select(pair => pair.Value)];
    }

    public void AddDirectory(AvaloniaStorageProviderSimpleDirectory directory)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(directory);

        if (directories.TryGetValue(directory.DirectoryName, out var replaced) &&
            !ReferenceEquals(replaced, directory))
        {
            replaced.Dispose();
        }

        directories[directory.DirectoryName] = directory;
    }

    public void AddFile(AvaloniaStorageProviderSimpleFile file)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(file);

        if (files.TryGetValue(file.FileName, out var replaced) &&
            !ReferenceEquals(replaced, file))
        {
            replaced.Dispose();
        }

        files[file.FileName] = file;
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        foreach (var childDirectory in directories.Values)
            childDirectory.Dispose();
        foreach (var childFile in files.Values)
            childFile.Dispose();

        storageFolder?.Dispose();
        storageFolder = null;
    }

    public override string ToString()
    {
        return $"Directory: {FullPath}, ChildDirsCount: {directories.Count}, ChildFilesCount: {files.Count}";
    }
}
