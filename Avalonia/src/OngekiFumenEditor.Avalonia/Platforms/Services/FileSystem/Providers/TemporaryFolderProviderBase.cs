#nullable enable

using System.Collections.Concurrent;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public abstract class TemporaryFolderProviderBase : ITemporaryFolderProvider
{
    protected enum TemporaryEntryKind
    {
        Missing,
        File,
        Folder
    }

    private const int UniqueNameAttempts = 128;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> fileMutationLocks = new(StringComparer.Ordinal);
    private readonly Lazy<ITemporaryFolder> root;

    protected TemporaryFolderProviderBase()
    {
        root = new Lazy<ITemporaryFolder>(
            () => new TemporaryFolder(this, "temp", string.Empty),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public abstract bool IsAvailable { get; }

    public ITemporaryFolder Root => root.Value;

    public async Task<ITemporaryFile> CreateUniqueFileAsync(
        string prefix = "tempFile",
        string extension = ".dat",
        ITemporaryFolder? parent = null,
        CancellationToken cancellationToken = default)
    {
        TemporaryEntryName.Validate(prefix, nameof(prefix));
        string normalizedExtension = TemporaryEntryName.NormalizeExtension(extension);
        TemporaryFolder folder = GetOwnedFolder(parent);

        for (int attempt = 0; attempt < UniqueNameAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = $"{prefix}.{Guid.NewGuid():N}{normalizedExtension}";
            TemporaryEntryName.Validate(name);
            string relativePath = TemporaryEntryName.Combine(folder.RelativePath, name);
            if (await TryCreateFileCoreAsync(relativePath, cancellationToken).ConfigureAwait(false))
                return new TemporaryFile(this, name, relativePath);
        }

        throw new IOException($"Could not allocate a unique temporary file after {UniqueNameAttempts} attempts.");
    }

    public async Task<ITemporaryFolder> CreateUniqueFolderAsync(
        string prefix = "tempFolder",
        ITemporaryFolder? parent = null,
        CancellationToken cancellationToken = default)
    {
        TemporaryEntryName.Validate(prefix, nameof(prefix));
        TemporaryFolder folder = GetOwnedFolder(parent);

        for (int attempt = 0; attempt < UniqueNameAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = $"{prefix}_{Guid.NewGuid():N}";
            TemporaryEntryName.Validate(name);
            string relativePath = TemporaryEntryName.Combine(folder.RelativePath, name);
            if (await TryCreateFolderCoreAsync(relativePath, cancellationToken).ConfigureAwait(false))
                return new TemporaryFolder(this, name, relativePath);
        }

        throw new IOException($"Could not allocate a unique temporary folder after {UniqueNameAttempts} attempts.");
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        ClearCoreAsync(cancellationToken);

    protected abstract string? GetLocalPathCore(string relativePath);

    protected abstract Task<TemporaryEntryKind> GetEntryKindCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task CreateFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task<bool> TryCreateFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task CreateFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task<bool> TryCreateFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task<long> GetFileLengthCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task<byte[]> ReadAllBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task<Stream> OpenReadCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task WriteFileCoreAsync(
        string relativePath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken);

    protected abstract Task AppendFileCoreAsync(
        string relativePath,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken);

    protected abstract Task DeleteFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task DeleteFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken);

    protected abstract Task ClearCoreAsync(CancellationToken cancellationToken);

    private TemporaryFolder GetOwnedFolder(ITemporaryFolder? folder)
    {
        if (folder is null)
            return (TemporaryFolder)Root;

        if (folder is not TemporaryFolder temporaryFolder || !ReferenceEquals(temporaryFolder.Provider, this))
            throw new ArgumentException("The parent folder belongs to a different temporary provider.", nameof(folder));

        return temporaryFolder;
    }

    private async Task<T> WithFileMutationLockAsync<T>(
        string relativePath,
        CancellationToken cancellationToken,
        Func<Task<T>> action)
    {
        SemaphoreSlim gate = fileMutationLocks.GetOrAdd(relativePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task WithFileMutationLockAsync(
        string relativePath,
        CancellationToken cancellationToken,
        Func<Task> action) =>
        WithFileMutationLockAsync(
            relativePath,
            cancellationToken,
            async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            });

    private abstract class TemporaryEntry(
        TemporaryFolderProviderBase provider,
        string name,
        string relativePath) : ITemporaryEntry
    {
        public TemporaryFolderProviderBase Provider { get; } = provider;
        public string Name { get; } = name;
        public string RelativePath { get; } = relativePath;
        public string? LocalPath => Provider.GetLocalPathCore(RelativePath);
    }

    private sealed class TemporaryFile(
        TemporaryFolderProviderBase provider,
        string name,
        string relativePath) : TemporaryEntry(provider, name, relativePath), ITemporaryFile
    {
        public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) =>
            Provider.GetFileLengthCoreAsync(RelativePath, cancellationToken);

        public Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default) =>
            Provider.ReadAllBytesCoreAsync(RelativePath, cancellationToken);

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Provider.OpenReadCoreAsync(RelativePath, cancellationToken);

        public Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            return Provider.WithFileMutationLockAsync(
                RelativePath,
                cancellationToken,
                () => Provider.WriteFileCoreAsync(RelativePath, writer, cancellationToken));
        }

        public Task WriteAllBytesAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) =>
            WriteAsync(
                (stream, writerCancellationToken) =>
                    stream.WriteAsync(data, writerCancellationToken).AsTask(),
                cancellationToken);

        public Task AppendAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) =>
            Provider.WithFileMutationLockAsync(
                RelativePath,
                cancellationToken,
                () => Provider.AppendFileCoreAsync(RelativePath, data, cancellationToken));

        public Task DeleteAsync(CancellationToken cancellationToken = default) =>
            Provider.WithFileMutationLockAsync(
                RelativePath,
                cancellationToken,
                () => Provider.DeleteFileCoreAsync(RelativePath, cancellationToken));
    }

    private sealed class TemporaryFolder(
        TemporaryFolderProviderBase provider,
        string name,
        string relativePath) : TemporaryEntry(provider, name, relativePath), ITemporaryFolder
    {
        public async Task<ITemporaryFile?> TryGetFileAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(name);
            string childPath = TemporaryEntryName.Combine(RelativePath, name);
            return await Provider.GetEntryKindCoreAsync(childPath, cancellationToken).ConfigureAwait(false)
                == TemporaryEntryKind.File
                ? new TemporaryFile(Provider, name, childPath)
                : null;
        }

        public async Task<ITemporaryFolder?> TryGetFolderAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(name);
            string childPath = TemporaryEntryName.Combine(RelativePath, name);
            return await Provider.GetEntryKindCoreAsync(childPath, cancellationToken).ConfigureAwait(false)
                == TemporaryEntryKind.Folder
                ? new TemporaryFolder(Provider, name, childPath)
                : null;
        }

        public async Task<ITemporaryFile> GetOrCreateFileAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(name);
            string childPath = TemporaryEntryName.Combine(RelativePath, name);
            TemporaryEntryKind kind = await Provider
                .GetEntryKindCoreAsync(childPath, cancellationToken)
                .ConfigureAwait(false);
            if (kind == TemporaryEntryKind.Folder)
                throw new IOException($"A folder already exists at temporary path '{childPath}'.");
            if (kind == TemporaryEntryKind.Missing)
                await Provider.CreateFileCoreAsync(childPath, cancellationToken).ConfigureAwait(false);

            return new TemporaryFile(Provider, name, childPath);
        }

        public async Task<ITemporaryFolder> GetOrCreateFolderAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(name);
            string childPath = TemporaryEntryName.Combine(RelativePath, name);
            TemporaryEntryKind kind = await Provider
                .GetEntryKindCoreAsync(childPath, cancellationToken)
                .ConfigureAwait(false);
            if (kind == TemporaryEntryKind.File)
                throw new IOException($"A file already exists at temporary path '{childPath}'.");
            if (kind == TemporaryEntryKind.Missing)
                await Provider.CreateFolderCoreAsync(childPath, cancellationToken).ConfigureAwait(false);

            return new TemporaryFolder(Provider, name, childPath);
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default) =>
            Provider.DeleteFolderCoreAsync(RelativePath, cancellationToken);
    }
}
