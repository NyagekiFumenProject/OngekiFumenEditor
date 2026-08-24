#nullable enable

using System.Runtime.InteropServices.JavaScript;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using System.Runtime.Versioning;
using Injectio.Attributes;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Logging;

/// <summary>
/// Browser-owned console sink: records are forwarded to the DevTools console with
/// severity-matched methods (debug/log/warn/error).
/// </summary>
[SupportedOSPlatform("browser")]
[RegisterSingleton<ILogOutput>]
public sealed partial class BrowserConsoleLogOutput : ILogOutput
{
    public void WriteLog(Severity severity, string content)
    {
        switch (severity)
        {
            case Severity.Debug:
                ConsoleDebug(content);
                break;
            case Severity.Warn:
                ConsoleWarn(content);
                break;
            case Severity.Error:
                ConsoleError(content);
                break;
            default:
                ConsoleLog(content);
                break;
        }
    }

    [JSImport("globalThis.console.debug")]
    private static partial void ConsoleDebug(string message);

    [JSImport("globalThis.console.log")]
    private static partial void ConsoleLog(string message);

    [JSImport("globalThis.console.warn")]
    private static partial void ConsoleWarn(string message);

    [JSImport("globalThis.console.error")]
    private static partial void ConsoleError(string message);
}
