using System.CommandLine;

namespace OngekiFumenEditor.Avalonia.CommandLine;

public interface ICommandLineDefinition
{
    Command CreateCommand();
}
