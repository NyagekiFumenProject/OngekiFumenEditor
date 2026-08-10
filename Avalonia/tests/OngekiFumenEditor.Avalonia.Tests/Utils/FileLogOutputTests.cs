using System.Text;
using OngekiFumenEditor.Avalonia.Platforms.Services.Logging;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class FileLogOutputTests
{
    [Fact]
    public async Task FileLogOutput_CreatesTimestampedSessionFileWithOriginalMarkerAndOrderedContent()
    {
        var storage = new InMemoryLogFileStorage();
        var now = new DateTime(2026, 8, 10, 15, 4, 5, DateTimeKind.Local);
        var output = new FileLogOutputWrapper(storage, () => now);

        Task first = output.WriteLogAsync("first\n");
        Task second = output.WriteLogAsync("second\n");
        await Task.WhenAll(first, second);
        await output.FlushAsync();
        var file = Assert.IsType<InMemoryLogFile>(await output.GetCurrentFileAsync());

        Assert.Equal("2026-08-10 15-04-05", storage.CreatedPrefix);
        Assert.Equal(".log", storage.CreatedExtension);
        Assert.Equal("memory:/logs/2026-08-10 15-04-05.log", output.GetCurrentLogFile());
        Assert.Equal(
            FileLogOutputWrapper.BeginFileLogOutputMarker + "first\nsecond\n",
            Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public async Task FileLogOutput_UnavailableStorageDoesNotReportFabricatedCurrentPath()
    {
        var output = new FileLogOutputWrapper(new DiscardLogFileStorage());

        await output.WriteLogAsync("discarded\n");
        await output.FlushAsync();

        Assert.Null(await output.GetCurrentFileAsync());
        Assert.Equal(string.Empty, output.GetCurrentLogFile());
    }

    private sealed class InMemoryLogFileStorage : ILogFileStorage
    {
        public bool IsAvailable => true;
        public string LogDirectoryPath => "memory:/logs";
        public string? CreatedPrefix { get; private set; }
        public string? CreatedExtension { get; private set; }

        public Task<ILogFile?> CreateUniqueFileAsync(
            string prefix,
            string extension = ".log",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreatedPrefix = prefix;
            CreatedExtension = extension;
            return Task.FromResult<ILogFile?>(new InMemoryLogFile($"{LogDirectoryPath}/{prefix}{extension}"));
        }
    }

    private sealed class InMemoryLogFile(string path) : ILogFile
    {
        private readonly MemoryStream content = new();

        public string Path { get; } = path;
        public byte[] Content => content.ToArray();

        public Task AppendAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            content.Write(data.Span);
            return Task.CompletedTask;
        }
    }
}
