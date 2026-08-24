using System.Text;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Logs;

namespace OngekiFumenEditor.Avalonia.Utils.Logging;

[RegisterSingleton<ILoggerProvider>]
public sealed class MELTransportLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TransportLogger(categoryName);

    public void Dispose()
    {
    }
}

internal sealed class TransportLogger(string categoryName) : ILogger
{
    private readonly string shortCategory = categoryName.Split('.').LastOrDefault() ?? categoryName;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable BeginScope<TState>(TState state) => null;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
            return;
        if (exception is not null)
            message = AppendException(message, exception);

        // 常规路径(eventId==0)零新增分配：短类名在构造时缓存，直接透传。
        var prefix = eventId.Id == 0
            ? shortCategory
            : string.Concat(shortCategory, "#", eventId.Id.ToString());
        Utils.Log.Instance.Emit(ToSeverity(logLevel), message, prefix);
    }

    private static StringBuilder sb = new(256);
    private static string AppendException(string message, Exception e)
    {
        sb.Clear();
        for (var level = 0; e is not null; level++, e = e.InnerException)
        {
            var tab = new string('\t', 2 * level);
            sb.AppendLine().Append(tab).Append($"Exception lv.{level} : {e.Message}")
              .AppendLine().Append(tab).Append($"Stack : {e.StackTrace}");
        }

        return sb.ToString();
    }

    private static ILogOutput.Severity ToSeverity(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => ILogOutput.Severity.Debug,
        LogLevel.Information => ILogOutput.Severity.Info,
        LogLevel.Warning => ILogOutput.Severity.Warn,
        LogLevel.Error or LogLevel.Critical => ILogOutput.Severity.Error,
        _ => ILogOutput.Severity.Info,
    };
}
