#nullable enable

using System.Collections.Concurrent;
using System.Text;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

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
    private readonly Lazy<ISimpleDirectory> root;

    protected TemporaryFolderProviderBase()
    {
        root = new Lazy<ISimpleDirectory>(
            () => new TemporaryFolder(this, "temp", string.Empty, null),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public abstract bool IsAvailable { get; }

    public ISimpleDirectory Root => root.Value;

    public async Task<ISimpleFile> CreateUniqueFileAsync(
        string prefix = "tempFile",
        string extension = ".dat",
        ISimpleDirectory? parent = null,
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
            {
                var file = new TemporaryFile(this, name, relativePath, folder, 0);
                folder.TrackFile(file);
                return file;
            }
        }

        throw new IOException($"Could not allocate a unique temporary file after {UniqueNameAttempts} attempts.");
    }

    public async Task<ISimpleDirectory> CreateUniqueFolderAsync(
        string prefix = "tempFolder",
        ISimpleDirectory? parent = null,
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
            {
                var child = new TemporaryFolder(this, name, relativePath, folder);
                folder.TrackFolder(child);
                return child;
            }
        }

        throw new IOException($"Could not allocate a unique temporary folder after {UniqueNameAttempts} attempts.");
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await ClearCoreAsync(cancellationToken).ConfigureAwait(false);
        ((TemporaryFolder)Root).ClearTrackedEntries();
    }

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

    private TemporaryFolder GetOwnedFolder(ISimpleDirectory? folder)
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
        string relativePath,
        TemporaryFolder? parent) : IDisposable
    {
        protected TemporaryFolder? Parent { get; } = parent;

        public TemporaryFolderProviderBase Provider { get; } = provider;
        public string Name { get; } = name;
        public string RelativePath { get; } = relativePath;
        public string? LocalPath => Provider.GetLocalPathCore(RelativePath);

        // Temporary handles do not own an OS resource. Dispose is provided so the same
        // handle can be consumed by APIs that own ISimpleFile/ISimpleDirectory values.
        public void Dispose()
        {
        }
    }

    private sealed class TemporaryFile(
        TemporaryFolderProviderBase provider,
        string name,
        string relativePath,
        TemporaryFolder parent,
        long initialLength) : TemporaryEntry(provider, name, relativePath, parent), ISimpleFile
    {
        private static readonly string[] LineSeparators = ["\r\n", "\n"];
        private long fileLength = initialLength;

        public ISimpleDirectory? ParentDictionary => Parent;
        public string FullPath => RelativePath;
        public string FileName => Name;
        public long FileLength => Interlocked.Read(ref fileLength);

        public void SetCachedLength(long length) => Interlocked.Exchange(ref fileLength, length);

        public async Task<long> GetLengthAsync(CancellationToken cancellationToken = default)
        {
            long length = await Provider
                .GetFileLengthCoreAsync(RelativePath, cancellationToken)
                .ConfigureAwait(false);
            Interlocked.Exchange(ref fileLength, length);
            return length;
        }

        public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
        {
            byte[] bytes = await Provider
                .ReadAllBytesCoreAsync(RelativePath, cancellationToken)
                .ConfigureAwait(false);
            Interlocked.Exchange(ref fileLength, bytes.LongLength);
            return bytes;
        }

        public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            Stream stream = await Provider
                .OpenReadCoreAsync(RelativePath, cancellationToken)
                .ConfigureAwait(false);
            if (stream.CanSeek)
            {
                try
                {
                    Interlocked.Exchange(ref fileLength, stream.Length);
                }
                catch (NotSupportedException)
                {
                    // The stream remains usable even when it does not expose its length.
                }
            }

            return stream;
        }

        public ValueTask<string[]> ReadAllLines() => ReadAllLinesAsync();

        public ValueTask<byte[]> ReadAllBytes() => new(ReadAllBytesAsync());

        public Task<Stream> OpenRead() => OpenReadAsync();

        public Task<Stream> OpenWrite() =>
            throw new NotSupportedException(
                "Temporary files must be written through WriteAsync() or WriteAllBytesAsync().");

        public async Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            await Provider.WithFileMutationLockAsync(
                RelativePath,
                cancellationToken,
                async () =>
                {
                    await Provider
                        .WriteFileCoreAsync(RelativePath, writer, cancellationToken)
                        .ConfigureAwait(false);
                    await RefreshCachedLengthAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        public Task WriteAllBytesAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) =>
            WriteAsync(
                (stream, writerCancellationToken) =>
                    stream.WriteAsync(data, writerCancellationToken).AsTask(),
                cancellationToken);

        public async Task AppendAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            await Provider.WithFileMutationLockAsync(
                RelativePath,
                cancellationToken,
                async () =>
                {
                    await Provider
                        .AppendFileCoreAsync(RelativePath, data, cancellationToken)
                        .ConfigureAwait(false);
                    await RefreshCachedLengthAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            await Provider.WithFileMutationLockAsync(
                RelativePath,
                cancellationToken,
                () => Provider.DeleteFileCoreAsync(RelativePath, cancellationToken)).ConfigureAwait(false);
            Interlocked.Exchange(ref fileLength, 0);
            Parent?.UntrackFile(this);
        }

        private async ValueTask<string[]> ReadAllLinesAsync()
        {
            var text = Encoding.UTF8.GetString(await ReadAllBytesAsync().ConfigureAwait(false));
            return text.Split(LineSeparators, StringSplitOptions.None);
        }

        private async Task RefreshCachedLengthAsync()
        {
            try
            {
                long length = await Provider
                    .GetFileLengthCoreAsync(RelativePath, CancellationToken.None)
                    .ConfigureAwait(false);
                Interlocked.Exchange(ref fileLength, length);
            }
            catch (FileNotFoundException)
            {
                // The discard provider intentionally drops writes and has no readable length.
            }
        }
    }

    private sealed class TemporaryFolder(
        TemporaryFolderProviderBase provider,
        string name,
        string relativePath,
        TemporaryFolder? parent) : TemporaryEntry(provider, name, relativePath, parent), ISimpleDirectory
    {
        private readonly ConcurrentDictionary<string, TemporaryFolder> folders = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, TemporaryFile> files = new(StringComparer.Ordinal);

        public ISimpleDirectory? ParentDictionary => Parent;
        public ISimpleDirectory[] ChildDictionaries => folders.Values.Cast<ISimpleDirectory>().ToArray();
        public ISimpleFile[] ChildFiles => files.Values.Cast<ISimpleFile>().ToArray();
        public string FullPath => RelativePath;
        public string DirectoryName => RelativePath.Length == 0 ? string.Empty : Name;

        public bool ExistsDirectory(string dirName) => folders.ContainsKey(dirName);

        public bool ExistsFile(string fileName) => files.ContainsKey(fileName);

        public ISimpleFile[] GetFiles(string pattern = "*")
        {
            var regex = SimpleIO.WildcardToRegex(pattern);
            return files.Values
                .Where(file => regex.IsMatch(file.FileName))
                .Cast<ISimpleFile>()
                .ToArray();
        }

        public Task<IReadOnlyList<SimpleDirectoryEntry>> GetEntrySnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<SimpleDirectoryEntry>>([
                .. folders.Values.Select(folder => new SimpleDirectoryEntry(folder.DirectoryName, true)),
                .. files.Values.Select(file => new SimpleDirectoryEntry(file.FileName, false))
            ]);
        }

        public async Task<ISimpleFile> CreateFileAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(fileName);
            if (files.ContainsKey(fileName) || folders.ContainsKey(fileName))
                throw new IOException($"An entry already exists at temporary path '{TemporaryEntryName.Combine(RelativePath, fileName)}'.");

            string childPath = TemporaryEntryName.Combine(RelativePath, fileName);
            await Provider.CreateFileCoreAsync(childPath, cancellationToken).ConfigureAwait(false);
            var file = new TemporaryFile(Provider, fileName, childPath, this, 0);
            TrackFile(file);
            return file;
        }

        public async Task<ISimpleFile?> TryGetFileAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(name);
            string childPath = TemporaryEntryName.Combine(RelativePath, name);
            if (await Provider.GetEntryKindCoreAsync(childPath, cancellationToken).ConfigureAwait(false)
                != TemporaryEntryKind.File)
            {
                return null;
            }

            long length = await Provider
                .GetFileLengthCoreAsync(childPath, cancellationToken)
                .ConfigureAwait(false);
            if (files.TryGetValue(name, out var existing))
            {
                existing.SetCachedLength(length);
                return existing;
            }

            var file = new TemporaryFile(Provider, name, childPath, this, length);
            TrackFile(file);
            return file;
        }

        public async Task<ISimpleDirectory?> TryGetDirectoryAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            TemporaryEntryName.Validate(name);
            string childPath = TemporaryEntryName.Combine(RelativePath, name);
            if (await Provider.GetEntryKindCoreAsync(childPath, cancellationToken).ConfigureAwait(false)
                != TemporaryEntryKind.Folder)
            {
                return null;
            }

            if (folders.TryGetValue(name, out var existing))
                return existing;

            var folder = new TemporaryFolder(Provider, name, childPath, this);
            TrackFolder(folder);
            return folder;
        }

        public async Task<ISimpleFile> GetOrCreateFileAsync(
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

            long length = 0;
            if (kind == TemporaryEntryKind.Missing)
                await Provider.CreateFileCoreAsync(childPath, cancellationToken).ConfigureAwait(false);
            else
                length = await Provider
                    .GetFileLengthCoreAsync(childPath, cancellationToken)
                    .ConfigureAwait(false);

            if (files.TryGetValue(name, out var existing))
            {
                existing.SetCachedLength(length);
                return existing;
            }

            var file = new TemporaryFile(Provider, name, childPath, this, length);
            TrackFile(file);
            return file;
        }

        public async Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
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

            if (folders.TryGetValue(name, out var existing))
                return existing;

            var folder = new TemporaryFolder(Provider, name, childPath, this);
            TrackFolder(folder);
            return folder;
        }

        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            await Provider.DeleteFolderCoreAsync(RelativePath, cancellationToken).ConfigureAwait(false);
            ClearTrackedEntries();
            Parent?.UntrackFolder(this);
        }

        public void TrackFile(TemporaryFile file) => files[file.Name] = file;

        public void TrackFolder(TemporaryFolder folder) => folders[folder.Name] = folder;

        public void UntrackFile(TemporaryFile file)
        {
            if (files.TryGetValue(file.Name, out var current) && ReferenceEquals(current, file))
                files.TryRemove(file.Name, out _);
        }

        public void UntrackFolder(TemporaryFolder folder)
        {
            if (folders.TryGetValue(folder.Name, out var current) && ReferenceEquals(current, folder))
                folders.TryRemove(folder.Name, out _);
        }

        public void ClearTrackedEntries()
        {
            foreach (TemporaryFile file in files.Values)
                file.SetCachedLength(0);

            foreach (TemporaryFolder folder in folders.Values)
                folder.ClearTrackedEntries();

            folders.Clear();
            files.Clear();
        }
    }
}
