using System.Diagnostics;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class DesktopCommandLineHostProcessTests
{
    [Fact]
    public async Task RootHelp_FullDesktopLifecycleReturnsZeroWithoutCreatingWindow()
    {
        var result = await RunCommandLineAsync("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.SawWindow);
        Assert.Contains("acb", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("convert", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("svg", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("jacket", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("updater", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task ConvertRelativePath_HandlerExitCodePassesThroughDesktopLifecycleWithoutCreatingWindow()
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.DesktopCommandLineHostProcessTests",
            Guid.NewGuid().ToString("N"),
            "output.ogkr");

        var result = await RunCommandLineAsync(
            "convert",
            "--inputFile", "relative.nyageki",
            "--outputFile", outputPath);

        Assert.Equal(-3, result.ExitCode);
        Assert.False(result.SawWindow);
        // Debug builds may include MEL diagnostic records on stdout; the command's
        // contract is the exit code/error stream and the absence of a partial file.
        Assert.NotEqual(string.Empty, result.StandardError);
        Assert.False(File.Exists(outputPath));
    }

    private static async Task<ProcessResult> RunCommandLineAsync(params string[] arguments)
    {
        var executablePath = GetCommandLineExecutablePath();
        Assert.True(File.Exists(executablePath), $"CommandLine executable was not built: {executablePath}");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the CommandLine process.");
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

        return new ProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask,
            sawWindow);
    }

    private static string GetCommandLineExecutablePath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test configuration.");
        return Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.CommandLine",
            "bin",
            configuration,
            "net11.0-windows10.0.19041.0",
            "OngekiFumenEditor.Avalonia.CommandLine.exe");
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

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool SawWindow);
}
