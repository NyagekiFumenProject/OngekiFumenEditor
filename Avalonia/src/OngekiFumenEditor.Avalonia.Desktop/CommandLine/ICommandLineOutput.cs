namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine;

public interface ICommandLineOutput
{
    Task WriteErrorLineAsync(string message);
}
