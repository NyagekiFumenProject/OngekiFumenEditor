using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Logging;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Logging;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests;

public sealed class DesktopFileLogOutputTests
{
    [Fact]
    public void DefaultLogDirectoryPath_IsLogsFolderBesideExecutable()
    {
        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs")),
            DesktopFileLogOutput.DefaultLogDirectoryPath);
        Assert.Equal("logs", DesktopFileLogOutput.LogFolderName);
    }

    [Fact]
    public async Task WriteLog_CreatesTimestampedSessionFileWithMarkerAndOrderedContent()
    {
        using var directory = new TemporaryDirectory();
        var now = new DateTime(2026, 8, 10, 15, 4, 5, DateTimeKind.Local);
        var output = new DesktopFileLogOutput(directory.RootPath, () => now);

        Task first = output.WriteLogAsync("first\n");
        Task second = output.WriteLogAsync("second\n");
        await Task.WhenAll(first, second);
        await output.FlushAsync();

        string currentPath = output.GetCurrentLogFile();
        string expectedDirectory = Path.GetFullPath(directory.PathFor(DesktopFileLogOutput.LogFolderName));
        Assert.Equal(expectedDirectory, output.LogDirectoryPath);
        Assert.Equal(expectedDirectory, Path.GetDirectoryName(currentPath));
        Assert.Equal("2026-08-10 15-04-05.log", Path.GetFileName(currentPath));
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}runtime{Path.DirectorySeparatorChar}", currentPath);
        Assert.Equal(
            IFileLogOutput.BeginFileLogOutputMarker + "first\nsecond\n",
            await File.ReadAllTextAsync(currentPath));
    }

    [Fact]
    public async Task WriteLog_PreservesOneSessionFilePerRunWithoutOverwrite()
    {
        using var directory = new TemporaryDirectory();
        var now = new DateTime(2026, 8, 10, 15, 4, 5, DateTimeKind.Local);
        var first = new DesktopFileLogOutput(directory.RootPath, () => now);
        var second = new DesktopFileLogOutput(directory.RootPath, () => now);

        await first.WriteLogAsync("first-run\n");
        await first.FlushAsync();
        await second.WriteLogAsync("second-run\n");
        await second.FlushAsync();

        string[] files = Directory.GetFiles(second.LogDirectoryPath, "*.log");
        Assert.Equal(2, files.Length);
        Assert.Contains(files, file => Path.GetFileName(file) == "2026-08-10 15-04-05.log");
        Assert.Contains(files, file => Path.GetFileName(file) == "2026-08-10 15-04-05_1.log");
        Assert.Contains("first-run\n", await File.ReadAllTextAsync(first.GetCurrentLogFile()));
        Assert.Contains("second-run\n", await File.ReadAllTextAsync(second.GetCurrentLogFile()));
    }

    [Fact]
    public async Task MelTransportAndApplicationLogOutput_ShareTheSameSessionFile()
    {
        using var directory = new TemporaryDirectory();
        var now = new DateTime(2026, 8, 10, 15, 4, 5, DateTimeKind.Local);
        var output = new DesktopFileLogOutput(directory.RootPath, () => now);
        Log? previous;
        try
        {
            previous = Log.Instance;
        }
        catch (InvalidOperationException)
        {
            // 普通单测没有 IoC 容器，门面尚未初始化；结束后自然无需还原。
            previous = null;
        }

        Log.Initialize(new Log(new ILogOutput[] { output }));
        try
        {
            await output.WriteLogAsync("application-record\n");
            using var provider = new MELTransportLoggerProvider();
            ILogger logger = provider.CreateLogger("OngekiFumenEditor.Tests.Category");
            logger.LogInformation("microsoft-logger-record");
            await Log.WaitForAllLogWriteDone();
            await output.FlushAsync();

            string currentPath = output.GetCurrentLogFile();
            string content = await File.ReadAllTextAsync(currentPath);
            string[] files = Directory.GetFiles(output.LogDirectoryPath, "*.log");

            Assert.Single(files);
            Assert.Equal(Path.GetFullPath(files[0]), Path.GetFullPath(currentPath));
            Assert.StartsWith(IFileLogOutput.BeginFileLogOutputMarker, content, StringComparison.Ordinal);
            Assert.Contains("application-record", content, StringComparison.Ordinal);
            Assert.Contains("<Category> microsoft-logger-record", content, StringComparison.Ordinal);
        }
        finally
        {
            if (previous is not null)
                Log.Initialize(previous);
        }
    }

    [Fact]
    public void DesktopRegistration_ProvidesPlatformLogOutputsAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvaloniaDesktop();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        var outputs = serviceProvider.GetServices<ILogOutput>().ToArray();

        var fileOutput = Assert.IsType<DesktopFileLogOutput>(outputs.OfType<IFileLogOutput>().Single());
        Assert.Same(fileOutput, Assert.Single(outputs.OfType<DesktopFileLogOutput>()));
        Assert.Single(outputs.OfType<DesktopConsoleLogOutput>());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.DesktopFileLogOutputTests",
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
