using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;
using OngekiFumenEditor.Avalonia.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Jacket;

public sealed class JacketGenerateServiceIntegrationTests
{
    private static readonly string[] RequiredNativeDependencyNames =
    [
        "TexturePlugin.dll",
        "TexToolWrap.dll",
        "crnlib.dll",
        "PVRTexLib.dll",
        "ispc_texcomp.dll"
    ];

    [Fact]
    public async Task Generate_RealTemplate_ProducesNormalAndSmallBundlesWithRequestedTextureDimensions()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("jacket.png");
        await CreateInputImageAsync(inputPath);
        var service = CreateService();
        var options = new JacketGenerateOption
        {
            MusicId = 666,
            InputImageFilePath = inputPath,
            OutputAssetbundleFolderPath = directory.RootPath,
            UpdateAssetBytesFile = false
        };

        var result = await service.GenerateAsync(options);

        Assert.True(result.IsSuccess, result.Message);
        var normalPath = directory.File("ui_jacket_0666");
        var smallPath = directory.File("ui_jacket_0666_s");
        Assert.True(new FileInfo(normalPath).Length > 1000);
        Assert.True(new FileInfo(smallPath).Length > 1000);
        var normalImage = Assert.IsType<JacketImageData>(
            await service.GetMainImageDataAsync(null!, normalPath));
        var smallImage = Assert.IsType<JacketImageData>(
            await service.GetMainImageDataAsync(null!, smallPath));
        Assert.Equal((520, 520), (normalImage.Width, normalImage.Height));
        Assert.Equal(520 * 520 * 4, normalImage.Data.Length);
        Assert.Equal((220, 220), (smallImage.Width, smallImage.Height));
        Assert.Equal(220 * 220 * 4, smallImage.Data.Length);
    }

    [Fact]
    public async Task Generate_UpdateAssetBytes_PreservesExistingRecordAndAppendsBothJacketNames()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("jacket.png");
        await CreateInputImageAsync(inputPath);
        var assetsBytesPath = directory.File("assets.bytes");
        var originalRecord = new AssetBytesAssertions.AssetRecord(
            42,
            "existing_bundle",
            [7, 9]);
        AssetBytesAssertions.Write(assetsBytesPath, originalRecord);
        var service = CreateService();
        var options = new JacketGenerateOption
        {
            MusicId = 666,
            InputImageFilePath = inputPath,
            OutputAssetbundleFolderPath = directory.RootPath,
            Width = 64,
            Height = 48,
            WidthSmall = 32,
            HeightSmall = 24,
            UpdateAssetBytesFile = true
        };

        var result = await service.GenerateAsync(options);

        Assert.True(result.IsSuccess, result.Message);
        var records = AssetBytesAssertions.Read(assetsBytesPath);
        Assert.Equal(3, records.Length);
        Assert.Equal(originalRecord.Id, records[0].Id);
        Assert.Equal(originalRecord.Name, records[0].Name);
        Assert.Equal(originalRecord.Dependencies, records[0].Dependencies);
        Assert.Equal(new[] { "ui_jacket_0666", "ui_jacket_0666_s" }, records.Skip(1).Select(x => x.Name));
        Assert.All(records.Skip(1), record => Assert.Empty(record.Dependencies));
    }

    [Fact]
    public void DesktopOutput_ContainsAllNativeDependencies()
    {
        foreach (var resourceName in RequiredNativeDependencyNames)
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, resourceName);
            Assert.True(File.Exists(filePath), $"Desktop resource was not copied: {filePath}");
            Assert.True(new FileInfo(filePath).Length > 0, $"Desktop resource is empty: {filePath}");
        }

    }

    private static DefaultJacketGenerateService CreateService()
    {
        Log.Initialize(new Log([]));
        return new DefaultJacketGenerateService();
    }

    private static async Task CreateInputImageAsync(string filePath)
    {
        using var image = new Image<Rgba32>(640, 360, new Rgba32(29, 113, 197, 255));
        await image.SaveAsPngAsync(filePath);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.JacketIntegrationTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);
        public string File(string fileName) => Path.Combine(RootPath, fileName);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
