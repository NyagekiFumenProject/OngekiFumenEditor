namespace OngekiFumenEditor.Avalonia.CommandLine;

public interface ICommandExecutor
{
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
