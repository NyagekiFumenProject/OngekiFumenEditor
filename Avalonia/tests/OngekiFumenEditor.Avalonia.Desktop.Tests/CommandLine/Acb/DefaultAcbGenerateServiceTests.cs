using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Acb;

public sealed class DefaultAcbGenerateServiceTests
{
    [Theory]
    [InlineData("missing-input")]
    [InlineData("negative-music-id")]
    [InlineData("blank-output")]
    public async Task GenerateAsync_InvalidRequest_ReturnsFailureWithoutGeneratedArtifacts(string scenario)
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("source.wav");
        File.WriteAllBytes(inputPath, [0x52, 0x49, 0x46, 0x46]);
        var options = CreateOptions(inputPath, directory.RootPath);
        string expectedMessage;

        switch (scenario)
        {
            case "missing-input":
                options.InputAudioFilePath = directory.File("missing.wav");
                expectedMessage = Lang.ConvertAudioFileNotFound;
                break;
            case "negative-music-id":
                options.MusicId = -1;
                expectedMessage = Lang.MusicIDInvaild.Format(options.MusicId);
                break;
            case "blank-output":
                options.OutputFolderPath = "   ";
                expectedMessage = Lang.OutputFolderIsEmpty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var result = await CreateService().GenerateAsync(options);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedMessage, result.Message);
        AssertGeneratedArtifactsDoNotExist(directory.RootPath);
    }

    [Fact]
    public async Task GenerateAsync_PreCanceledToken_PropagatesCancellationWithoutGeneratedArtifacts()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("source.wav");
        File.WriteAllBytes(inputPath, [0x52, 0x49, 0x46, 0x46]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().GenerateAsync(
                CreateOptions(inputPath, directory.RootPath),
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        AssertGeneratedArtifactsDoNotExist(directory.RootPath);
    }

    private static DefaultAcbGenerateService CreateService()
    {
        Log.Initialize(new Log([]));
        return new DefaultAcbGenerateService();
    }

    private static AcbGenerateOption CreateOptions(string inputPath, string outputPath) => new()
    {
        MusicId = 427,
        InputAudioFilePath = inputPath,
        OutputFolderPath = outputPath
    };

    private static void AssertGeneratedArtifactsDoNotExist(string outputPath)
    {
        Assert.False(File.Exists(Path.Combine(outputPath, "music0427.acb")));
        Assert.False(File.Exists(Path.Combine(outputPath, "music0427.awb")));
        Assert.False(File.Exists(Path.Combine(outputPath, "MusicSource.xml")));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.AcbServiceTests",
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
