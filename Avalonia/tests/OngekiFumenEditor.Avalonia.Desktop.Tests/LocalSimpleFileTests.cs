using System.Text;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests;

public sealed class LocalSimpleFileTests
{
    [Fact]
    public async Task WriteAsync_WriterCancellationPreservesTargetAndRemovesStagingFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.File("output.ogkr");
        var original = Encoding.UTF8.GetBytes("original content");
        await File.WriteAllBytesAsync(filePath, original);

        using var file = new LocalSimpleFile(filePath);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.WriteAsync(
            async (stream, cancellationToken) =>
            {
                await stream.WriteAsync("partial replacement"u8.ToArray(), cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            },
            cancellation.Token));

        Assert.Equal(original, await File.ReadAllBytesAsync(filePath));
        Assert.Equal(["output.ogkr"], Directory.GetFiles(temporaryDirectory.RootPath).Select(Path.GetFileName));
    }

    [Fact]
    public async Task WriteAsync_ProducerCompletesBeforeCancellationCommitsTarget()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.File("output.ogkr");
        await File.WriteAllTextAsync(filePath, "original content");

        using var file = new LocalSimpleFile(filePath);
        using var cancellation = new CancellationTokenSource();
        var replacement = Encoding.UTF8.GetBytes("replacement");

        await file.WriteAsync(async (stream, cancellationToken) =>
        {
            await stream.WriteAsync(replacement, cancellationToken);
            cancellation.Cancel();
        }, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(replacement, await File.ReadAllBytesAsync(filePath));
        Assert.Equal(replacement.LongLength, file.FileLength);
        Assert.Equal(["output.ogkr"], Directory.GetFiles(temporaryDirectory.RootPath).Select(Path.GetFileName));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "OngekiFumenEditor.LocalSimpleFile.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string File(string relativePath) => Path.Combine(RootPath, relativePath);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
