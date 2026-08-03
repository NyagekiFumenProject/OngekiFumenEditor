using System.Diagnostics;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Updater;

public sealed class UpdaterExecutableSmokeTests
{
    [Fact]
    public async Task UpdaterExecutable_TemporaryFoldersAndHarmlessDesktopStub_CompletesWithoutTouchingWorkspace()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test configuration.");
        var commandLineExecutable = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.CommandLine",
            "bin",
            configuration,
            "net10.0-windows10.0.19041.0",
            "OngekiFumenEditor.Avalonia.CommandLine.exe");
        var stubOutputPath = Path.Combine(
            repositoryRoot,
            "tests",
            "OngekiFumenEditor.Avalonia.UpdaterStub",
            "bin",
            configuration,
            "net10.0-windows10.0.19041.0");
        Assert.True(File.Exists(commandLineExecutable), $"CommandLine executable was not built: {commandLineExecutable}");
        Assert.True(Directory.Exists(stubOutputPath), $"Updater stub was not built: {stubOutputPath}");

        using var directory = new TemporaryDirectory();
        var sourcePath = directory.CreateSubdirectory("source");
        var targetPath = directory.CreateSubdirectory("target");
        CopyDirectory(stubOutputPath, sourcePath);
        var originalStubPath = Path.Combine(sourcePath, "OngekiFumenEditor.Avalonia.UpdaterStub.exe");
        var desktopStubPath = Path.Combine(sourcePath, "OngekiFumenEditor.Avalonia.Desktop.exe");
        File.Copy(originalStubPath, desktopStubPath);
        var markerPath = directory.File("stub-arguments.txt");
        var startInfo = new ProcessStartInfo(commandLineExecutable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 {
                     "updater",
                     "--sourceFolder", sourcePath,
                     "--targetFolder", targetPath,
                     "--sourceVersion", "1.2.3.4"
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment["ONGEKI_UPDATER_STUB_MARKER"] = markerPath;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start CommandLine updater smoke process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var sawWindow = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!process.HasExited)
        {
            process.Refresh();
            sawWindow |= process.MainWindowHandle != IntPtr.Zero;
            await Task.Delay(20, timeout.Token);
        }
        await process.WaitForExitAsync(timeout.Token);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.True(
            process.ExitCode == 0,
            $"Updater exited with {process.ExitCode}.{Environment.NewLine}stdout:{stdout}{Environment.NewLine}stderr:{stderr}");
        Assert.False(sawWindow);
        await WaitForFileAsync(markerPath, timeout.Token);
        Assert.Equal(
            new[] { "--wait", "--notifySucess", "--sourceVersion", "1.2.3.4" },
            await File.ReadAllLinesAsync(markerPath, timeout.Token));
        Assert.True(File.Exists(Path.Combine(targetPath, "OngekiFumenEditor.Avalonia.Desktop.exe")));
        Assert.Empty(Directory.GetFiles(targetPath, "*.bak_*", SearchOption.AllDirectories));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor.Avalonia.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Avalonia repository root.");
    }

    private static void CopyDirectory(string sourcePath, string targetPath)
    {
        foreach (var sourceFilePath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, sourceFilePath);
            var targetFilePath = Path.Combine(targetPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            File.Copy(sourceFilePath, targetFilePath);
        }
    }

    private static async Task WaitForFileAsync(string filePath, CancellationToken cancellationToken)
    {
        while (!File.Exists(filePath))
            await Task.Delay(20, cancellationToken);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.UpdaterExecutableSmokeTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => System.IO.Directory.CreateDirectory(RootPath);
        public string File(string fileName) => Path.Combine(RootPath, fileName);

        public string CreateSubdirectory(string directoryName)
        {
            var directoryPath = Path.Combine(RootPath, directoryName);
            System.IO.Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(RootPath))
                System.IO.Directory.Delete(RootPath, recursive: true);
        }
    }
}
