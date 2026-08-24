using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Logging;

[RegisterSingleton<ILogOutput>]
public sealed class DesktopConsoleLogOutput : ILogOutput
{
    public void WriteLog(Severity severity, string content)
    {
        var backup = Console.ForegroundColor;
        Console.ForegroundColor = severity switch
        {
            Severity.Debug => ConsoleColor.Gray,
            Severity.Info => ConsoleColor.Green,
            Severity.Warn => ConsoleColor.Yellow,
            Severity.Error => ConsoleColor.Red,
            _ => ConsoleColor.Cyan,
        };
        Console.Write(content);
        Console.ForegroundColor = backup;
    }
}
