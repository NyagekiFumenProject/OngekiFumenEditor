#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

[RegisterSingleton<ITemporaryFolderProvider>]
public sealed class BrowserTemporaryFolderProvider : ITemporaryFolderProvider
{
    private readonly ITemporaryFolderProvider implementation;

    public BrowserTemporaryFolderProvider()
    {
        try
        {
            implementation = BrowserTemporaryFileSystemInterop.IsAvailable()
                ? new OpfsTemporaryFolderProvider()
                : new DiscardTemporaryFolderProvider();
        }
        catch
        {
            implementation = new DiscardTemporaryFolderProvider();
        }
    }

    public bool IsAvailable => implementation.IsAvailable;
    public ISimpleDirectory Root => implementation.Root;

    public Task<ISimpleFile> CreateUniqueFileAsync(
        string prefix = "tempFile",
        string extension = ".dat",
        ISimpleDirectory? parent = null,
        CancellationToken cancellationToken = default) =>
        implementation.CreateUniqueFileAsync(prefix, extension, parent, cancellationToken);

    public Task<ISimpleDirectory> CreateUniqueFolderAsync(
        string prefix = "tempFolder",
        ISimpleDirectory? parent = null,
        CancellationToken cancellationToken = default) =>
        implementation.CreateUniqueFolderAsync(prefix, parent, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        implementation.ClearAsync(cancellationToken);
}

internal sealed class OpfsTemporaryFolderProvider : TemporaryFolderProviderBase
{
    private static readonly SemaphoreSlim MutationGate = new(1, 1);
    private static int nextWriteBufferHandle;

    public override bool IsAvailable => true;

    protected override string? GetLocalPathCore(string relativePath) => null;

    protected override async Task<TemporaryEntryKind> GetEntryKindCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int kind = await BrowserTemporaryFileSystemInterop.GetEntryKindAsync(relativePath).ConfigureAwait(false);
        return kind switch
        {
            0 => TemporaryEntryKind.Missing,
            1 => TemporaryEntryKind.File,
            2 => TemporaryEntryKind.Folder,
            _ => throw new IOException($"OPFS returned unknown entry kind {kind} for '{relativePath}'.")
        };
    }

    protected override Task CreateFileCoreAsync(string relativePath, CancellationToken cancellationToken) =>
        RunMutationAsync(
            () => BrowserTemporaryFileSystemInterop.CreateFileAsync(relativePath),
            cancellationToken);

    protected override Task<bool> TryCreateFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        RunMutationAsync(
            () => BrowserTemporaryFileSystemInterop.TryCreateFileAsync(relativePath),
            cancellationToken);

    protected override Task CreateFolderCoreAsync(string relativePath, CancellationToken cancellationToken) =>
        RunMutationAsync(
            () => BrowserTemporaryFileSystemInterop.CreateFolderAsync(relativePath),
            cancellationToken);

    protected override Task<bool> TryCreateFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        RunMutationAsync(
            () => BrowserTemporaryFileSystemInterop.TryCreateFolderAsync(relativePath),
            cancellationToken);

    protected override async Task<long> GetFileLengthCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double length = await BrowserTemporaryFileSystemInterop
            .GetFileLengthAsync(relativePath)
            .ConfigureAwait(false);
        return checked((long)length);
    }

    protected override async Task<byte[]> ReadAllBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var result = await BrowserTemporaryFileSystemInterop
            .ReadFileAsync(relativePath)
            .ConfigureAwait(false);
        return result.GetPropertyAsByteArray("data")
               ?? throw new IOException($"OPFS returned no byte data for '{relativePath}'.");
    }

    protected override async Task<Stream> OpenReadCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        byte[] data = await ReadAllBytesCoreAsync(relativePath, cancellationToken).ConfigureAwait(false);
        return new MemoryStream(data, writable: false);
    }

    protected override async Task WriteFileCoreAsync(
        string relativePath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = new MemoryStream();
        await writer(buffer, cancellationToken).ConfigureAwait(false);
        byte[] data = buffer.ToArray();

        // The writer completed successfully. Waiting for and executing commit no longer observes cancellation.
        await MutationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await CommitBufferAsync(
                    relativePath,
                    data,
                    BrowserTemporaryFileSystemInterop.WriteFileAsync)
                .ConfigureAwait(false);
        }
        finally
        {
            MutationGate.Release();
        }
    }

    protected override async Task AppendFileCoreAsync(
        string relativePath,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CommitBufferAsync(
                    relativePath,
                    data.ToArray(),
                    BrowserTemporaryFileSystemInterop.AppendFileAsync)
                .ConfigureAwait(false);
        }
        finally
        {
            MutationGate.Release();
        }
    }

    protected override Task DeleteFileCoreAsync(string relativePath, CancellationToken cancellationToken) =>
        RunMutationAsync(
            () => BrowserTemporaryFileSystemInterop.DeleteFileAsync(relativePath),
            cancellationToken);

    protected override Task DeleteFolderCoreAsync(string relativePath, CancellationToken cancellationToken) =>
        RunMutationAsync(
            () => BrowserTemporaryFileSystemInterop.DeleteFolderAsync(relativePath),
            cancellationToken);

    protected override Task ClearCoreAsync(CancellationToken cancellationToken) =>
        RunMutationAsync(BrowserTemporaryFileSystemInterop.ClearAsync, cancellationToken);

    private static async Task CommitBufferAsync(
        string relativePath,
        byte[] data,
        Func<string, int, Task> commit)
    {
        int handle = Interlocked.Increment(ref nextWriteBufferHandle);
        try
        {
            BrowserTemporaryFileSystemInterop.SetWriteBuffer(handle, data, data.Length);
            await commit(relativePath, handle).ConfigureAwait(false);
        }
        finally
        {
            BrowserTemporaryFileSystemInterop.ReleaseWriteBuffer(handle);
        }
    }

    private static async Task RunMutationAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private static async Task<T> RunMutationAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            MutationGate.Release();
        }
    }
}
