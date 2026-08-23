using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class TemporaryFolderProviderContractTests
{
    [Fact]
    public async Task TemporaryEntries_UseSimpleFileSystemContracts()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        ISimpleDirectory directory = await provider.Root.GetOrCreateDirectoryAsync("simple");
        ISimpleFile file = await directory.GetOrCreateFileAsync("data.txt");

        Assert.Equal("simple", directory.FullPath);
        Assert.Equal("simple/data.txt", file.FullPath);
        Assert.Same(directory, file.ParentDictionary);
        Assert.True(directory.ExistsFile("data.txt"));
        Assert.Single(directory.ChildFiles);

        await file.WriteAsync(
            (stream, cancellationToken) => stream.WriteAsync("temporary"u8.ToArray(), cancellationToken).AsTask());

        Assert.Equal(9, file.FileLength);
        Assert.Equal("temporary", System.Text.Encoding.UTF8.GetString(await file.ReadAllBytes()));
        Assert.Equal("data.txt", Assert.Single(directory.GetFiles()).FileName);
    }

    [Fact]
    public async Task CreateUniqueFileAsync_CreatesPlaceholderBeforeReturningAndUsesDistinctNames()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        ISimpleDirectory parent = await provider.Root.GetOrCreateDirectoryAsync("unique");

        ISimpleFile first = await provider.CreateUniqueFileAsync("asset", ".bin", parent);
        ISimpleFile[] concurrent = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => provider.CreateUniqueFileAsync("asset", ".bin", parent)));

        Assert.Equal(string.Empty, provider.Root.FullPath);
        Assert.Null(provider.Root.LocalPath);
        Assert.Equal(0, await first.GetLengthAsync());
        Assert.NotNull(await parent.TryGetFileAsync(first.FileName));
        Assert.StartsWith("asset.", first.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".bin", first.FileName, StringComparison.Ordinal);
        Assert.Equal(33, concurrent.Append(first).Select(file => file.FileName).Distinct().Count());
    }

    [Fact]
    public async Task FixedNamesAndNestedFolders_AreReusable()
    {
        var provider = new InMemoryTemporaryFolderProvider();

        var firstFolder = await provider.Root.GetOrCreateDirectoryAsync("level1");
        var secondFolder = await provider.Root.GetOrCreateDirectoryAsync("level1");
        var nested = await firstFolder.GetOrCreateDirectoryAsync("level2");
        var firstFile = await nested.GetOrCreateFileAsync("fixed.dat");
        await firstFile.WriteAllBytesAsync(new byte[] { 1, 2, 3 });
        var secondFile = await nested.GetOrCreateFileAsync("fixed.dat");

        Assert.Equal(firstFolder.FullPath, secondFolder.FullPath);
        Assert.Equal("level1/level2", nested.FullPath);
        Assert.Equal("level1/level2/fixed.dat", secondFile.FullPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, await secondFile.ReadAllBytesAsync());
        Assert.NotNull(await firstFolder.TryGetDirectoryAsync("level2"));
    }

    [Fact]
    public async Task ReadWriteAppendAndOpenRead_RoundTrip()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var file = await provider.Root.GetOrCreateFileAsync("roundtrip.dat");

        await file.WriteAllBytesAsync(new byte[] { 1, 2 });
        await file.AppendAsync(new byte[] { 3, 4 });
        await using Stream stream = await file.OpenReadAsync();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.False(stream.CanWrite);
        Assert.Equal(4, await file.GetLengthAsync());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await file.ReadAllBytesAsync());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, copy.ToArray());
    }

    [Fact]
    public async Task WriteAsync_WhenProducerFailsOrCancels_RollsBackOriginalContent()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var file = await provider.Root.GetOrCreateFileAsync("transaction.dat");
        await file.WriteAllBytesAsync(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 9, 9 }, cancellationToken);
                throw new InvalidOperationException("producer failed");
            }));
        Assert.Equal(new byte[] { 1, 2, 3 }, await file.ReadAllBytesAsync());

        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 8, 8 }, cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellation.Token));
        Assert.Equal(new byte[] { 1, 2, 3 }, await file.ReadAllBytesAsync());
    }

    [Fact]
    public async Task WriteAsync_WhenProducerSucceeds_CommitsEvenIfTokenWasCanceledAtCommitBoundary()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var file = await provider.Root.GetOrCreateFileAsync("commit.dat");
        await file.WriteAllBytesAsync(new byte[] { 1 });
        using var cancellation = new CancellationTokenSource();

        await file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 4, 5, 6 }, cancellationToken);
                cancellation.Cancel();
            },
            cancellation.Token);

        Assert.Equal(new byte[] { 4, 5, 6 }, await file.ReadAllBytesAsync());
    }

    [Fact]
    public async Task DeleteAndClear_AreIdempotentAndProviderRemainsReusable()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var removed = await provider.Root.GetOrCreateDirectoryAsync("removed");
        var child = await removed.GetOrCreateDirectoryAsync("child");
        await (await child.GetOrCreateFileAsync("data.bin")).WriteAllBytesAsync(new byte[] { 1 });
        var retained = await provider.Root.GetOrCreateFileAsync("retained.bin");
        await retained.WriteAllBytesAsync(new byte[] { 2 });

        await removed.DeleteAsync();
        await removed.DeleteAsync();

        Assert.Null(await provider.Root.TryGetDirectoryAsync("removed"));
        Assert.Equal(new byte[] { 2 }, await retained.ReadAllBytesAsync());

        await provider.ClearAsync();
        Assert.Null(await provider.Root.TryGetFileAsync("retained.bin"));
        Assert.Empty(((ISimpleDirectory)provider.Root).ChildFiles);
        Assert.False(((ISimpleDirectory)provider.Root).ExistsFile("retained.bin"));
        Assert.NotNull(await provider.Root.GetOrCreateFileAsync("after-clear.bin"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/escape")]
    [InlineData("C:\\escape")]
    [InlineData("parent/child")]
    [InlineData("parent\\child")]
    [InlineData("bad:name")]
    [InlineData("bad?")]
    [InlineData("trailing.")]
    [InlineData("CON")]
    [InlineData("COM1.txt")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("control\u0001")]
    public async Task EntryOperations_RejectUnsafeSingleSegmentNames(string name)
    {
        var provider = new InMemoryTemporaryFolderProvider();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.Root.GetOrCreateFileAsync(name));
        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.Root.GetOrCreateDirectoryAsync(name));
    }

    [Fact]
    public async Task UniqueAllocation_RejectsUnsafePrefixExtensionAndForeignParent()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var otherProvider = new InMemoryTemporaryFolderProvider();
        ISimpleDirectory foreignParent = await otherProvider.Root.GetOrCreateDirectoryAsync("foreign");

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateUniqueFileAsync("../escape", ".dat"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateUniqueFileAsync("safe", "../dat"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateUniqueFolderAsync("parent/child"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CreateUniqueFileAsync(parent: foreignParent));
    }

    [Fact]
    public async Task PreCanceledOperations_DoNotCreateEntries()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.Root.GetOrCreateFileAsync("canceled.dat", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CreateUniqueFolderAsync(cancellationToken: cancellation.Token));

        Assert.Null(await provider.Root.TryGetFileAsync("canceled.dat"));
    }

    [Fact]
    public async Task GetRequiredLocalPath_WhenBackendHasNoLocalPath_ThrowsExplicitly()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var file = await provider.Root.GetOrCreateFileAsync("browser-like.dat");

        var exception = Assert.Throws<PlatformNotSupportedException>(file.GetRequiredLocalPath);

        Assert.Contains(file.FullPath, exception.Message, StringComparison.Ordinal);
    }
}
