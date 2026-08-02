using OngekiFumenEditor.Avalonia.Utils.Logs;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.CommandLine;

internal sealed class CommandLineLogOutput : ILogOutput
{
    public void WriteLog(Severity severity, string content) => Console.Write(content);
}
