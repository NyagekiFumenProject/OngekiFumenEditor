using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class TemporaryStorageConsumerTests
{
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

    [Fact]
    public async Task FileLogOutput_AppendsInCallOrderUnderRuntimeFolder()
    {
        var provider = new InMemoryTemporaryFolderProvider();
        var output = new FileLogOutputWrapper(provider);

        Task first = output.WriteLogAsync("first\n");
        Task second = output.WriteLogAsync("second\n");
        await Task.WhenAll(first, second);
        await output.FlushAsync();
        var file = await output.GetCurrentFileAsync();

        Assert.StartsWith("logs/runtime/", file.RelativePath, StringComparison.Ordinal);
        Assert.Equal(file.RelativePath, output.GetCurrentLogFile());
        Assert.Equal("first\nsecond\n", Encoding.UTF8.GetString(await file.ReadAllBytesAsync()));
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
