#nullable enable

using Avalonia.Platform.Storage;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

public static class AvaloniaStorageProviderFileSystemBuilder
{
    private const int MaxParallelStorageOperations = 4;

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

        using var context = new BuildContext(cancellationToken);
        var root = await BuildDirectory(null, rootStorageFolder, context).ConfigureAwait(false);
        root.DirectoryName = string.Empty;
        return root;
    }

    private static async Task<AvaloniaStorageProviderSimpleDirectory> BuildDirectory(
        ISimpleDirectory? parent,
        IStorageFolder folder,
        BuildContext context)
    {
        var directory = new AvaloniaStorageProviderSimpleDirectory(parent, folder.Name, folder);
        try
        {
            if (parent is null && IsLocalLink(folder))
                throw new IOException("The selected project root cannot be a symbolic link, junction, or mount point.");

            var items = await EnumerateItemsAsync(folder, context).ConfigureAwait(false);
            var childTasks = items
                .Select((item, index) => BuildChildAsync(directory, item, index, context))
                .ToArray();
            BuildChildResult[] children;
            try
            {
                children = await Task.WhenAll(childTasks).ConfigureAwait(false);
            }
            catch
            {
                foreach (var childTask in childTasks)
                {
                    if (childTask is { IsCompletedSuccessfully: true })
                        DisposeChild(childTask.Result);
                }

                throw;
            }
            foreach (var child in children.OrderBy(static child => child.Index))
            {
                if (child.Directory is not null)
                    directory.AddDirectory(child.Directory);
                else if (child.File is not null)
                    directory.AddFile(child.File);
            }

            return directory;
        }
        catch
        {
            directory.Dispose();
            throw;
        }
    }

    private static async Task<IStorageItem[]> EnumerateItemsAsync(
        IStorageFolder folder,
        BuildContext context)
    {
        await context.StorageGate.WaitAsync(context.CancellationToken).ConfigureAwait(false);
        try
        {
            var items = new List<IStorageItem>();
            try
            {
                await foreach (var item in folder.GetItemsAsync().ConfigureAwait(false))
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    items.Add(item);
                }

                return items.ToArray();
            }
            catch
            {
                foreach (var item in items)
                    item.Dispose();
                throw;
            }
        }
        finally
        {
            context.StorageGate.Release();
        }
    }

    private static async Task<BuildChildResult> BuildChildAsync(
        AvaloniaStorageProviderSimpleDirectory parent,
        IStorageItem item,
        int index,
        BuildContext context)
    {
        var ownershipTransferred = false;
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await context.StorageGate.WaitAsync(context.CancellationToken).ConfigureAwait(false);
            try
            {
                if (IsLocalLink(item))
                {
                    Log.LogWarn($"Skip linked project entry '{item.Name}'.");
                    return new BuildChildResult(index, null, null);
                }

                switch (item)
                {
                    case IStorageFile childFile:
                    {
                        var properties = await childFile.GetBasicPropertiesAsync().ConfigureAwait(false);
                        var fileLength = properties.Size is { } size
                            ? checked((long)size)
                            : 0;
                        var file = new AvaloniaStorageProviderSimpleFile(
                            parent,
                            childFile.Name,
                            fileLength,
                            childFile);
                        ownershipTransferred = true;
                        return new BuildChildResult(index, null, file);
                    }
                    case IStorageFolder childFolder:
                        break;
                    default:
                        return new BuildChildResult(index, null, null);
                }
            }
            finally
            {
                context.StorageGate.Release();
            }

            ownershipTransferred = true;
            var childDirectory = await BuildDirectory(parent, (IStorageFolder)item, context)
                .ConfigureAwait(false);
            return new BuildChildResult(index, childDirectory, null);
        }
        finally
        {
            if (!ownershipTransferred)
                item.Dispose();
        }
    }

    private sealed class BuildContext : IDisposable
    {
        public BuildContext(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            StorageGate = new SemaphoreSlim(MaxParallelStorageOperations, MaxParallelStorageOperations);
        }

        public CancellationToken CancellationToken { get; }

        public SemaphoreSlim StorageGate { get; }

        public void Dispose() => StorageGate.Dispose();
    }

    private readonly record struct BuildChildResult(
        int Index,
        AvaloniaStorageProviderSimpleDirectory? Directory,
        AvaloniaStorageProviderSimpleFile? File);

    private static void DisposeChild(BuildChildResult child)
    {
        child.Directory?.Dispose();
        child.File?.Dispose();
    }

    internal static bool IsLocalLink(IStorageItem item)
    {
        var localPath = item.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return false;

        try
        {
            return (File.GetAttributes(localPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Unable to inspect project entry '{item.Name}' for file-system links.", exception);
        }
    }
}
