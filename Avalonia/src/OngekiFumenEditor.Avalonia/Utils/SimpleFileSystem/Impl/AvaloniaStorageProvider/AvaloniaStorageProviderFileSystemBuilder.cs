#nullable enable

using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public static class AvaloniaStorageProviderFileSystemBuilder
{
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
