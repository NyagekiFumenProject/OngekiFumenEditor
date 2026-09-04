using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

[RegisterSingleton<IProgramUpdateProcessEnvironment>]
internal sealed class DefaultProgramUpdateProcessEnvironment : IProgramUpdateProcessEnvironment
{
    public int CurrentProcessId => Environment.ProcessId;

    public IEnumerable<int> GetProcessIdsByName(string processName) =>
        Process.GetProcessesByName(processName).Select(process => process.Id).ToArray();

    public void KillProcess(int processId)
    {
        using var process = Process.GetProcessById(processId);
        process.Kill();
    }

    public void WaitForProcessExit(int processId, int timeoutMilliseconds)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            Log.LogInfo($"waiting for parent editor process to exit, pid: {processId}");
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                Log.LogWarn($"parent editor process did not exit in time, force killing it, pid: {processId}");
                process.Kill();
                process.WaitForExit();
            }
        }
        catch (ArgumentException)
        {
            Log.LogInfo($"parent editor process already exited, pid: {processId}");
        }
    }

    public void StartProcess(string fileName, string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start process '{fileName}'.");
    }
}
