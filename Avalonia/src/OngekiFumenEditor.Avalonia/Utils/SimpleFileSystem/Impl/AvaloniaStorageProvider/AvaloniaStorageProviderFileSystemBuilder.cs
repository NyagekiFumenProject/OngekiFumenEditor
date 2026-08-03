#nullable enable

using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public static class AvaloniaStorageProviderFileSystemBuilder
{
    /// <summary>
    /// Wraps only the selected folder. This is intended for path-oriented settings and avoids
    /// recursively enumerating an arbitrarily large tree during the picker operation.
    /// </summary>
    public static ISimpleDirectory LoadRootFromAvaloniaStorageFolder(IStorageFolder storageFolder)
    {
        ArgumentNullException.ThrowIfNull(storageFolder);

        try
        {
            var root = new AvaloniaStorageProviderSimpleDirectory(null, string.Empty, storageFolder);
            return root;
        }
        catch
        {
            storageFolder.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps a storage file and transfers ownership of it to the returned file.
    /// </summary>
    public static async Task<ISimpleFile> LoadFromAvaloniaStorageFile(
        IStorageFile storageFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storageFile);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var properties = await storageFile.GetBasicPropertiesAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var fileLength = properties.Size is { } size
                ? checked((long)size)
                : 0;
            return new AvaloniaStorageProviderSimpleFile(
                null,
                storageFile.Name,
                fileLength,
                storageFile);
        }
        catch
        {
            storageFile.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Builds an in-memory directory index and transfers ownership of the storage folder tree
    /// to the returned root directory.
    /// </summary>
    public static async Task<ISimpleDirectory> LoadFromAvaloniaStorageFolder(
        IStorageFolder rootStorageFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootStorageFolder);

        var root = await BuildDirectory(null, rootStorageFolder, cancellationToken).ConfigureAwait(false);
        root.DirectoryName = string.Empty;
        return root;
    }

    private static async Task<AvaloniaStorageProviderSimpleDirectory> BuildDirectory(
        ISimpleDirectory? parent,
        IStorageFolder folder,
        CancellationToken cancellationToken)
    {
        var directory = new AvaloniaStorageProviderSimpleDirectory(parent, folder.Name, folder);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var item in folder.GetItemsAsync().ConfigureAwait(false))
            {
                var ownershipTransferred = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    switch (item)
                    {
                        case IStorageFile childFile:
                        {
                            var properties = await childFile.GetBasicPropertiesAsync().ConfigureAwait(false);
                            var fileLength = properties.Size is { } size
                                ? checked((long)size)
                                : 0;
                            directory.AddFile(new AvaloniaStorageProviderSimpleFile(
                                directory,
                                childFile.Name,
                                fileLength,
                                childFile));
                            ownershipTransferred = true;
                            break;
                        }
                        case IStorageFolder childFolder:
                        {
                            var childDirectory = await BuildDirectory(
                                    directory,
                                    childFolder,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            directory.AddDirectory(childDirectory);
                            ownershipTransferred = true;
                            break;
                        }
                    }
                }
                finally
                {
                    if (!ownershipTransferred)
                        item.Dispose();
                }
            }

            return directory;
        }
        catch
        {
            directory.Dispose();
            throw;
        }
    }
}
