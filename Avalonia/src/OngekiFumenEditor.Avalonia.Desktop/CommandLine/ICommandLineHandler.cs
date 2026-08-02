namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine;

public interface ICommandLineHandler
{
}

public interface ICommandLineHandler<in TOptions> : ICommandLineHandler
{
    Task<int> HandleAsync(TOptions options, CancellationToken cancellationToken);
}
