using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class DiscardTemporaryFolderProviderTests
{
    [Fact]
    public async Task WritesRunProducerButNeverCreateReadableData()
    {
        var provider = new DiscardTemporaryFolderProvider();
        var folder = await provider.Root.GetOrCreateDirectoryAsync("discarded");
        var file = await folder.GetOrCreateFileAsync("data.bin");
        bool producerRan = false;

        await file.WriteAsync(async (stream, cancellationToken) =>
        {
            producerRan = true;
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
        });
        await file.WriteAllBytesAsync(new byte[] { 4, 5 });
        await file.AppendAsync(new byte[] { 6 });

        Assert.False(provider.IsAvailable);
        Assert.True(producerRan);
        Assert.Null(file.LocalPath);
        Assert.Null(await provider.Root.TryGetDirectoryAsync("discarded"));
        Assert.Null(await folder.TryGetFileAsync("data.bin"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => file.GetLengthAsync());
        await Assert.ThrowsAsync<FileNotFoundException>(() => file.ReadAllBytesAsync());
        await Assert.ThrowsAsync<FileNotFoundException>(() => file.OpenReadAsync());
    }

    [Fact]
    public async Task UniqueDeleteAndClear_AreSafeNoOps()
    {
        var provider = new DiscardTemporaryFolderProvider();
        ISimpleDirectory folder = await provider.CreateUniqueFolderAsync("folder");
        var file = await provider.CreateUniqueFileAsync("file", ".tmp", folder);

        await file.DeleteAsync();
        await file.DeleteAsync();
        await folder.DeleteAsync();
        await provider.ClearAsync();

        Assert.Null(await provider.Root.TryGetDirectoryAsync(folder.DirectoryName));
        Assert.Null(await provider.Root.TryGetFileAsync(file.FileName));
    }

    [Fact]
    public async Task PreCanceledWrite_DoesNotRunProducer()
    {
        var provider = new DiscardTemporaryFolderProvider();
        var file = await provider.Root.GetOrCreateFileAsync("canceled.bin");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool producerRan = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.WriteAsync(
            (_, _) =>
            {
                producerRan = true;
                return Task.CompletedTask;
            },
            cancellation.Token));

        Assert.False(producerRan);
    }
}
