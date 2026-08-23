using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests;

public sealed class DesktopTemporaryFolderProviderTests
{
    [Fact]
    public void DefaultRootPath_PreservesCompatibleFolderName()
    {
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "NagekiFumenEditorTempFolder"),
            DesktopTemporaryFolderProvider.DefaultRootPath);
        Assert.Equal(
            "NagekiFumenEditorTempFolder",
            DesktopTemporaryFolderProvider.RootFolderName);
    }

    [Fact]
    public async Task RootIsLazy_AndUniqueFileIsPhysicalContainedPlaceholder()
    {
        using var directory = new TemporaryDirectory();
        string rootPath = directory.PathFor("provider");
        var provider = new DesktopTemporaryFolderProvider(rootPath);

        var root = provider.Root;
        Assert.Same(root, provider.Root);
        Assert.False(Directory.Exists(rootPath));

        var nested = await root.GetOrCreateDirectoryAsync("nested");
        var file = await provider.CreateUniqueFileAsync("asset", ".bin", nested);
        string localPath = Assert.IsType<string>(file.LocalPath);
        string relativeToRoot = Path.GetRelativePath(rootPath, localPath);

        Assert.True(provider.IsAvailable);
        Assert.True(File.Exists(localPath));
        Assert.False(Path.IsPathRooted(file.FullPath));
        Assert.Equal($"nested/{file.FileName}", file.FullPath);
        Assert.DoesNotContain("..", relativeToRoot.Split(Path.DirectorySeparatorChar));
        Assert.Equal(Path.GetFullPath(rootPath), Path.GetFullPath(Assert.IsType<string>(root.LocalPath)));
    }

    [Fact]
    public async Task DataPersistsAcrossProviderInstancesUsingSameRoot()
    {
        using var directory = new TemporaryDirectory();
        string rootPath = directory.PathFor("provider");
        var firstProvider = new DesktopTemporaryFolderProvider(rootPath);
        var folder = await firstProvider.Root.GetOrCreateDirectoryAsync("persistent");
        var file = await folder.GetOrCreateFileAsync("data.bin");
        await file.WriteAllBytesAsync(new byte[] { 1, 2, 3, 4 });

        var secondProvider = new DesktopTemporaryFolderProvider(rootPath);
        var reopenedFolder = await secondProvider.Root.TryGetDirectoryAsync("persistent");
        Assert.NotNull(reopenedFolder);
        var reopenedFile = await reopenedFolder.TryGetFileAsync("data.bin");

        Assert.NotNull(reopenedFile);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await reopenedFile.ReadAllBytesAsync());
    }

    [Fact]
    public async Task ConcurrentUniqueAllocation_CreatesDistinctOccupiedFiles()
    {
        using var directory = new TemporaryDirectory();
        var provider = new DesktopTemporaryFolderProvider(directory.PathFor("provider"));

        ISimpleFile[] files = await Task.WhenAll(
            Enumerable.Range(0, 64)
                .Select(_ => provider.CreateUniqueFileAsync("parallel", ".tmp")));

        Assert.Equal(files.Length, files.Select(file => file.FileName).Distinct().Count());
        Assert.All(files, file => Assert.True(File.Exists(file.LocalPath)));
    }

    [Fact]
    public async Task TransactionalWrite_RollsBackFailureAndCommitsAfterProducerCancellation()
    {
        using var directory = new TemporaryDirectory();
        var provider = new DesktopTemporaryFolderProvider(directory.PathFor("provider"));
        var file = await provider.Root.GetOrCreateFileAsync("transaction.bin");
        await file.WriteAllBytesAsync(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 9, 9 }, cancellationToken);
                throw new InvalidOperationException("producer failed");
            }));
        Assert.Equal(new byte[] { 1, 2, 3 }, await file.ReadAllBytesAsync());

        using var cancellation = new CancellationTokenSource();
        await file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 4, 5, 6 }, cancellationToken);
                cancellation.Cancel();
            },
            cancellation.Token);

        Assert.Equal(new byte[] { 4, 5, 6 }, await file.ReadAllBytesAsync());
        Assert.Empty(Directory.EnumerateFiles(directory.PathFor("provider"), ".*.tmp"));
    }

    [Fact]
    public async Task Clear_RemovesOnlyProviderContentsAndLeavesRootReusable()
    {
        using var directory = new TemporaryDirectory();
        string rootPath = directory.PathFor("provider");
        string siblingPath = directory.PathFor("sibling.txt");
        await File.WriteAllTextAsync(siblingPath, "outside");
        var provider = new DesktopTemporaryFolderProvider(rootPath);
        var child = await provider.Root.GetOrCreateDirectoryAsync("child");
        await (await child.GetOrCreateFileAsync("data.bin"))
            .WriteAllBytesAsync(new byte[] { 1 });

        await provider.ClearAsync();

        Assert.True(File.Exists(siblingPath));
        Assert.True(Directory.Exists(rootPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(rootPath));
        Assert.NotNull(await provider.Root.GetOrCreateFileAsync("after-clear.bin"));
    }

    [Fact]
    public void DesktopRegistration_ProvidesTemporaryFolderProviderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvaloniaDesktop();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        var first = serviceProvider.GetRequiredService<ITemporaryFolderProvider>();
        var second = serviceProvider.GetRequiredService<ITemporaryFolderProvider>();

        Assert.IsType<DesktopTemporaryFolderProvider>(first);
        Assert.Same(first, second);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.DesktopTemporaryFolderProviderTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);

        public string PathFor(string name) => Path.Combine(RootPath, name);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
