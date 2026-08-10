using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Logging;
using OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;
using OngekiFumenEditor.Avalonia.Platforms.Services.Logging;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests;

public sealed class DesktopLogFileStorageTests
{
    [Fact]
    public void DefaultLogDirectoryPath_IsLogsFolderBesideExecutable()
    {
        var storage = new DesktopLogFileStorage();

        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs")),
            DesktopLogFileStorage.DefaultLogDirectoryPath);
        Assert.Equal(DesktopLogFileStorage.DefaultLogDirectoryPath, storage.LogDirectoryPath);
        Assert.Equal("logs", DesktopLogFileStorage.LogFolderName);
    }

    [Fact]
    public async Task CreateUniqueFileAsync_CreatesPhysicalFilesDirectlyUnderExecutableLogsFolder()
    {
        using var directory = new TemporaryDirectory();
        var storage = new DesktopLogFileStorage(directory.RootPath);

        var first = Assert.IsAssignableFrom<ILogFile>(
            await storage.CreateUniqueFileAsync("2026-08-10 15-04-05"));
        var second = Assert.IsAssignableFrom<ILogFile>(
            await storage.CreateUniqueFileAsync("2026-08-10 15-04-05"));
        await first.AppendAsync("first"u8.ToArray());

        string expectedDirectory = Path.GetFullPath(directory.PathFor("logs"));
        Assert.Equal(expectedDirectory, storage.LogDirectoryPath);
        Assert.Equal(expectedDirectory, Path.GetDirectoryName(first.Path));
        Assert.Equal(expectedDirectory, Path.GetDirectoryName(second.Path));
        Assert.Equal("2026-08-10 15-04-05.log", Path.GetFileName(first.Path));
        Assert.Equal("2026-08-10 15-04-05_1.log", Path.GetFileName(second.Path));
        Assert.Equal("first", await File.ReadAllTextAsync(first.Path));
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}runtime{Path.DirectorySeparatorChar}", first.Path);
    }

    [Fact]
    public async Task DesktopLoggerAndApplicationLogOutput_ShareTheSameSessionFile()
    {
        using var directory = new TemporaryDirectory();
        var storage = new DesktopLogFileStorage(directory.RootPath);
        var now = new DateTime(2026, 8, 10, 15, 4, 5, DateTimeKind.Local);
        var output = new FileLogOutputWrapper(storage, () => now);
        using var provider = new FileLoggerProvider(new ILogOutput[] { output });
        ILogger logger = provider.CreateLogger("OngekiFumenEditor.Tests.Category");

        await output.WriteLogAsync("application-record\n");
        logger.LogInformation("microsoft-logger-record");
        await output.FlushAsync();

        string currentPath = output.GetCurrentLogFile();
        string content = await File.ReadAllTextAsync(currentPath);
        string[] files = Directory.GetFiles(storage.LogDirectoryPath, "*.log");

        Assert.Single(files);
        Assert.Equal(Path.GetFullPath(files[0]), Path.GetFullPath(currentPath));
        Assert.StartsWith(FileLogOutputWrapper.BeginFileLogOutputMarker, content, StringComparison.Ordinal);
        Assert.Contains("application-record", content, StringComparison.Ordinal);
        Assert.Contains("[Category] microsoft-logger-record", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopRegistration_ProvidesLogFileStorageAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvaloniaDesktop();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        var first = serviceProvider.GetRequiredService<ILogFileStorage>();
        var second = serviceProvider.GetRequiredService<ILogFileStorage>();

        Assert.IsType<DesktopLogFileStorage>(first);
        Assert.Same(first, second);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.DesktopLogFileStorageTests",
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
