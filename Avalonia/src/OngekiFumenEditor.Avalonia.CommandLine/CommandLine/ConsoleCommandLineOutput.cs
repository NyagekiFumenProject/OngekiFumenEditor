using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.CommandLine;

[RegisterSingleton<ICommandLineOutput>]
internal sealed class ConsoleCommandLineOutput : ICommandLineOutput
{
    public Task WriteErrorLineAsync(string message) => Console.Error.WriteLineAsync(message);
}
