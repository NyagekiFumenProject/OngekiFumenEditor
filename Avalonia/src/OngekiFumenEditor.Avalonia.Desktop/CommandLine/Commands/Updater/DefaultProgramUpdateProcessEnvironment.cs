using Injectio.Attributes;
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

    public void StartProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start process '{fileName}'.");
    }
}
