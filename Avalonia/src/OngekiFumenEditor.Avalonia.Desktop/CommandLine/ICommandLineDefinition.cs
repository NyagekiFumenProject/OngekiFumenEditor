using System.CommandLine;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine;

public interface ICommandLineDefinition
{
    Command CreateCommand();
}
