namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;

internal interface IProgramUpdateProcessEnvironment
{
    int CurrentProcessId { get; }
    IEnumerable<int> GetProcessIdsByName(string processName);
    void KillProcess(int processId);
    void WaitForProcessExit(int processId, int timeoutMilliseconds);
    void StartProcess(string fileName, string workingDirectory, IReadOnlyList<string> arguments);
}
