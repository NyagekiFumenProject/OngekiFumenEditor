using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using OngekiFumenEditor.Avalonia.Utils;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class TemporaryStorageConsumerTests
{
    [AvaloniaFact]
    public async Task EditorProjectFileManager_LegacyV052Project_UpgradesToLatestModel()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var file = await provider.Root.GetOrCreateFileAsync("legacy.nyagekiProj");
        Guid projectId = Guid.NewGuid();
        var legacy = new EditorProjectDataModel_V0_5_2
        {
            Id = projectId,
            AudioFilePath = "legacy-audio.wav",
            FumenFilePath = "legacy-chart.ogkr",
            AudioDuration = TimeSpan.FromSeconds(84),
            RememberLastDisplayTime = TimeSpan.FromSeconds(21)
        };

        var serializer = new EditorProjectDataModelSerializer_V0_5_2();
        await file.WriteAsync((stream, _) => serializer.WriteAsync(stream, legacy));
        string serialized = Encoding.UTF8.GetString(await file.ReadAllBytesAsync());
        var reloaded = await new EditorProjectFileManager().Load(file);

        Assert.Contains("\"Version\": \"0.5.2\"", serialized, StringComparison.Ordinal);
        Assert.IsType<EditorProjectDataModel>(reloaded);
        Assert.Equal(EditorProjectDataModel.VERSION, reloaded.Version);
        Assert.Equal(projectId, reloaded.Id);
        Assert.Equal(legacy.AudioFilePath, reloaded.AudioFilePath);
        Assert.Equal(legacy.FumenFilePath, reloaded.FumenFilePath);
        Assert.Equal(legacy.AudioDuration, reloaded.AudioDuration);
        Assert.Equal(legacy.RememberLastDisplayTime, reloaded.RememberLastDisplayTime);
    }

    [Fact]
    public async Task ImageLoader_NetworkCacheSurvivesLoaderInstances()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        const string url = "https://example.invalid/jacket.png";
        byte[] expected = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        int downloadCount = 0;

        Task<byte[]> Download(string _, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref downloadCount);
            return Task.FromResult(expected.ToArray());
        }

        var firstLoader = new ImageLoader(provider, Download);
        byte[] first = await firstLoader.LoadImage(url, CancellationToken.None);
        var secondLoader = new ImageLoader(provider, Download);
        byte[] second = await secondLoader.LoadImage(url, CancellationToken.None);

        string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
        var images = await provider.Root.TryGetFolderAsync("images");
        Assert.NotNull(images);
        var cacheFile = await images.TryGetFileAsync($"{hash}.img.cache");

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.Equal(1, downloadCount);
        Assert.NotNull(cacheFile);
        Assert.Equal(expected, await cacheFile.ReadAllBytesAsync());
    }

    [AvaloniaFact]
    public async Task RescueProjectSerialization_WritesAndReloadsTemporaryFileHandle()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var rescue = await provider.Root.GetOrCreateFolderAsync("Rescue");
        var file = await rescue.GetOrCreateFileAsync("project.nyagekiProj");
        Guid projectId = Guid.NewGuid();
        var project = new EditorProjectDataModel
        {
            Id = projectId,
            AudioFilePath = "audio.wav",
            FumenFilePath = "chart.ogkr",
            AudioDuration = TimeSpan.FromSeconds(42),
            RememberLastDisplayTime = TimeSpan.FromSeconds(7)
        };

        EditorProjectDataUtils.Result result = await EditorProjectDataUtils.TrySaveProjFileAsync(file, project);
        var reloaded = await new EditorProjectFileManager().Load(file);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(await file.GetLengthAsync() > 0);
        Assert.Equal(projectId, reloaded.Id);
        Assert.Equal(project.AudioFilePath, reloaded.AudioFilePath);
        Assert.Equal(project.FumenFilePath, reloaded.FumenFilePath);
        Assert.Equal(project.AudioDuration, reloaded.AudioDuration);
        Assert.Equal(project.RememberLastDisplayTime, reloaded.RememberLastDisplayTime);
    }
}
