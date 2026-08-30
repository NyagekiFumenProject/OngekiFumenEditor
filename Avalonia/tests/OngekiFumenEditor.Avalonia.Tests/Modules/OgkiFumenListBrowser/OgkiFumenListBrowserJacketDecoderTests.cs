using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;
using OngekiFumenEditor.Avalonia.Tests.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.OgkiFumenListBrowser;

public sealed class OgkiFumenListBrowserJacketDecoderTests
{
    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadPngBytesAsync_RepositoryUnityAssetBundle_ProducesAvaloniaBitmap()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));
        var bundlePath = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia",
            "Resources",
            "ui_jacket_0666");
        var pluginPath = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "..",
            "OngekiFumenEditor",
            "Dependencies",
            "JacketGenerator",
            "TexturePlugin.dll"));

        Assert.True(File.Exists(bundlePath), bundlePath);
        Assert.True(File.Exists(pluginPath), pluginPath);
        Assembly.LoadFrom(pluginPath);

        using var source = await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFile(await GetStorageFile(bundlePath));
        var temporaryFolderProvider = new InMemoryTemporaryFolderProvider();
        var decoder = new OgkiFumenListBrowserJacketDecoder(temporaryFolderProvider);
        var pngBytes = await decoder.LoadPngBytesAsync(source);

        Assert.NotNull(pngBytes);
        Assert.NotEmpty(pngBytes);
        var cacheDirectory = await temporaryFolderProvider.Root
            .TryGetDirectoryAsync("OgkiFumenListBrowserJackets");
        Assert.NotNull(cacheDirectory);
        var cacheFile = Assert.Single(cacheDirectory!.ChildFiles);
        Assert.EndsWith(".png", cacheFile.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(pngBytes, await cacheFile.ReadAllBytesAsync());
        using var bitmap = new Bitmap(new MemoryStream(pngBytes!, writable: false));
        Assert.True(bitmap.PixelSize.Width > 0);
        Assert.True(bitmap.PixelSize.Height > 0);
    }

    private static async Task<IStorageFile> GetStorageFile(string path)
    {
        var window = new Window();
        return await window.StorageProvider.TryGetFileFromPathAsync(path)
            ?? throw new InvalidOperationException($"Unable to create a storage file for '{path}'.");
    }
}
