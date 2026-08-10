using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Browser.Utils;

internal sealed class BrowserFileLoggerProvider : ILoggerProvider
{
    private readonly FileLogOutputWrapper output;

    public BrowserFileLoggerProvider(IEnumerable<ILogOutput> outputs)
    {
        output = outputs.OfType<FileLogOutputWrapper>().Single();
    }

    public ILogger CreateLogger(string categoryName) => new BrowserFileLogger(categoryName, output);

    public void Dispose()
    {
    }

    private sealed class BrowserFileLogger(string categoryName, FileLogOutputWrapper output) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null)
                return;

            string eventIdText = eventId.Id == 0 ? string.Empty : $":{eventId}";
            string record = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {logLevel.ToString().ToUpperInvariant()}]" +
                            $"<{categoryName}{eventIdText}> {message}";
            if (exception is not null)
                record += $"{Environment.NewLine}{exception}";
            record += Environment.NewLine;

            output.WriteLog(ToSeverity(logLevel), record);
        }

        private static Severity ToSeverity(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace or LogLevel.Debug => Severity.Debug,
            LogLevel.Information => Severity.Info,
            LogLevel.Warning => Severity.Warn,
            LogLevel.Error or LogLevel.Critical => Severity.Error,
            _ => Severity.Info
        };
    }
}
