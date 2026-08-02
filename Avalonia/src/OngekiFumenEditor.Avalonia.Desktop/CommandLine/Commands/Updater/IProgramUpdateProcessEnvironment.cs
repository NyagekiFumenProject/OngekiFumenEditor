namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

internal interface IProgramUpdateProcessEnvironment
{
    int CurrentProcessId { get; }
    IEnumerable<int> GetProcessIdsByName(string processName);
    void KillProcess(int processId);
    void StartProcess(string fileName, IReadOnlyList<string> arguments);
}
