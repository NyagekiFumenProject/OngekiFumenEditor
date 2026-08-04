#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public sealed class DiscardTemporaryFolderProvider : TemporaryFolderProviderBase
{
    public override bool IsAvailable => false;

    protected override string? GetLocalPathCore(string relativePath) => null;

    protected override Task<TemporaryEntryKind> GetEntryKindCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TemporaryEntryKind.Missing);
    }

    protected override Task CreateFileCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    protected override Task<bool> TryCreateFileCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    protected override Task CreateFolderCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    protected override Task<bool> TryCreateFolderCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    protected override Task<long> GetFileLengthCoreAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        Task.FromException<long>(CreateNotFound(relativePath, cancellationToken));

    protected override Task<byte[]> ReadAllBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        Task.FromException<byte[]>(CreateNotFound(relativePath, cancellationToken));

    protected override Task<Stream> OpenReadCoreAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        Task.FromException<Stream>(CreateNotFound(relativePath, cancellationToken));

    protected override async Task WriteFileCoreAsync(
        string relativePath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = new MemoryStream();
        await writer(buffer, cancellationToken).ConfigureAwait(false);
    }

    protected override Task AppendFileCoreAsync(
        string relativePath,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    protected override Task DeleteFileCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    protected override Task DeleteFolderCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    protected override Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static Exception CreateNotFound(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new FileNotFoundException(
            $"Temporary file '{relativePath}' does not exist because temporary storage is unavailable.",
            relativePath);
    }
}
