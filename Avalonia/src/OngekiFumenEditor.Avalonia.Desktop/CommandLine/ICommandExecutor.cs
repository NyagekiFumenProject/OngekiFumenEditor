namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine;

public interface ICommandExecutor
{
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
