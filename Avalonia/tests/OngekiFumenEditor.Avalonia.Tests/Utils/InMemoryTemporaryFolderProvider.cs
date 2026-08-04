#nullable enable

using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

internal sealed class InMemoryTemporaryFolderProvider : TemporaryFolderProviderBase
{
    private readonly object sync = new();
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly HashSet<string> folders = new(StringComparer.Ordinal) { string.Empty };

    public override bool IsAvailable => true;

    protected override string? GetLocalPathCore(string relativePath) => null;

    protected override Task<TemporaryEntryKind> GetEntryKindCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            return Task.FromResult(files.ContainsKey(relativePath)
                ? TemporaryEntryKind.File
                : folders.Contains(relativePath)
                    ? TemporaryEntryKind.Folder
                    : TemporaryEntryKind.Missing);
        }
    }

    protected override Task CreateFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            EnsureParentFolderExists(relativePath);
            if (folders.Contains(relativePath))
                throw new IOException($"A folder already exists at '{relativePath}'.");

            files.TryAdd(relativePath, []);
        }

        return Task.CompletedTask;
    }

    protected override Task<bool> TryCreateFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            EnsureParentFolderExists(relativePath);
            if (files.ContainsKey(relativePath) || folders.Contains(relativePath))
                return Task.FromResult(false);

            files.Add(relativePath, []);
            return Task.FromResult(true);
        }
    }

    protected override Task CreateFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            EnsureParentFolderExists(relativePath);
            if (files.ContainsKey(relativePath))
                throw new IOException($"A file already exists at '{relativePath}'.");

            folders.Add(relativePath);
        }

        return Task.CompletedTask;
    }

    protected override Task<bool> TryCreateFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            EnsureParentFolderExists(relativePath);
            if (files.ContainsKey(relativePath) || folders.Contains(relativePath))
                return Task.FromResult(false);

            folders.Add(relativePath);
            return Task.FromResult(true);
        }
    }

    protected override Task<long> GetFileLengthCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<long>(GetFileCopy(relativePath).Length);
    }

    protected override Task<byte[]> ReadAllBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetFileCopy(relativePath));
    }

    protected override Task<Stream> OpenReadCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new MemoryStream(GetFileCopy(relativePath), writable: false));
    }

    protected override async Task WriteFileCoreAsync(
        string relativePath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = new MemoryStream();
        await writer(buffer, cancellationToken);
        byte[] committed = buffer.ToArray();

        // The producer succeeded, so the commit intentionally ignores later cancellation.
        lock (sync)
        {
            EnsureParentFolderExists(relativePath);
            if (folders.Contains(relativePath))
                throw new IOException($"A folder already exists at '{relativePath}'.");

            files[relativePath] = committed;
        }
    }

    protected override Task AppendFileCoreAsync(
        string relativePath,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            EnsureParentFolderExists(relativePath);
            if (folders.Contains(relativePath))
                throw new IOException($"A folder already exists at '{relativePath}'.");

            files.TryGetValue(relativePath, out byte[]? existing);
            byte[] appended = new byte[(existing?.Length ?? 0) + data.Length];
            existing?.CopyTo(appended, 0);
            data.Span.CopyTo(appended.AsSpan(existing?.Length ?? 0));
            files[relativePath] = appended;
        }

        return Task.CompletedTask;
    }

    protected override Task DeleteFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            files.Remove(relativePath);
        }

        return Task.CompletedTask;
    }

    protected override Task DeleteFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (relativePath.Length == 0)
            {
                files.Clear();
                folders.RemoveWhere(static path => path.Length > 0);
                return Task.CompletedTask;
            }

            string descendantPrefix = $"{relativePath}/";
            foreach (string filePath in files.Keys
                         .Where(path => path.StartsWith(descendantPrefix, StringComparison.Ordinal))
                         .ToArray())
            {
                files.Remove(filePath);
            }

            folders.RemoveWhere(path =>
                path.Equals(relativePath, StringComparison.Ordinal) ||
                path.StartsWith(descendantPrefix, StringComparison.Ordinal));
        }

        return Task.CompletedTask;
    }

    protected override Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            files.Clear();
            folders.RemoveWhere(static path => path.Length > 0);
        }

        return Task.CompletedTask;
    }

    private byte[] GetFileCopy(string relativePath)
    {
        lock (sync)
        {
            if (!files.TryGetValue(relativePath, out byte[]? data))
                throw new FileNotFoundException($"Temporary file '{relativePath}' does not exist.", relativePath);

            return data.ToArray();
        }
    }

    private void EnsureParentFolderExists(string relativePath)
    {
        int separator = relativePath.LastIndexOf('/');
        string parent = separator < 0 ? string.Empty : relativePath[..separator];
        if (!folders.Contains(parent))
            throw new DirectoryNotFoundException($"Temporary folder '{parent}' does not exist.");
    }
}
