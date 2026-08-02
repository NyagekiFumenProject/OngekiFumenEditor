namespace OngekiFumenEditor.Avalonia.CommandLine;

public interface ICommandLineOutput
{
    Task WriteErrorLineAsync(string message);
}
