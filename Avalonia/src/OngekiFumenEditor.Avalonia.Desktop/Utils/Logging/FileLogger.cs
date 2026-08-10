using System;
using System.Linq;
using System.Text;
using System.Threading;
using Gekimini.Avalonia.Utils;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;

public class FileLogger : ILogger
{
    private readonly FileLogOutputWrapper output;
    private readonly string simpliedCategoryName;
    private readonly DateTime startTime;

    public FileLogger(string categoryName, FileLogOutputWrapper output, DateTime startTime)
    {
        this.output = output;
        this.startTime = startTime;
        simpliedCategoryName = categoryName.Split(".").LastOrDefault();
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
#if DEBUG
        if (DesignModeHelper.IsDesignMode)
            return;
#endif

        if (!IsEnabled(logLevel))
            return;

        var now = DateTime.Now;

        var levelStr = logLevel switch
        {
            LogLevel.Information => "Info",
            _ => logLevel.ToString()
        };

        var overDays = (int) (now - startTime).TotalDays;
        var overDaysStr = overDays > 0 ? $"+{overDays}d " : string.Empty;
        var eventIdStr = eventId == 0 ? string.Empty : eventId.ToString();
        var threadId = Thread.CurrentThread.ManagedThreadId;
        var threadIdStr = threadId switch
        {
            1 => string.Empty,
            _ => threadId.ToString()
        };

        var logRecord =
            $"{overDaysStr}{now:HH:mm:ss.fff} {levelStr}:{eventIdStr}:{threadIdStr} [{simpliedCategoryName}] {formatter(state, exception)}{Environment.NewLine}";
        if (logLevel == LogLevel.Error && exception is not null)
        {
            var fullExceptionStack = BuildExceptionMessageContent(exception);
            logRecord += $"--------------------------{Environment.NewLine}";
            logRecord += $"print full exception info{Environment.NewLine}";
            logRecord += fullExceptionStack;
            logRecord += $"--------------------------{Environment.NewLine}";
        }

        output.WriteLog(ToSeverity(logLevel), logRecord);
    }

    private static Severity ToSeverity(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace or LogLevel.Debug => Severity.Debug,
        LogLevel.Information => Severity.Info,
        LogLevel.Warning => Severity.Warn,
        LogLevel.Error or LogLevel.Critical => Severity.Error,
        _ => Severity.Info
    };

    private string BuildExceptionMessageContent(Exception e)
    {
        var sb = new StringBuilder();

        void exceptionDump(Exception e, int level = 0)
        {
            if (e is null)
                return;
            var tab = string.Concat(Enumerable.Repeat("\t", 2 * level));

            sb.AppendLine();
            sb.AppendLine(tab + $"Exception lv.{level} : {e.Message}");
            sb.AppendLine(tab + $"Stack : {e.StackTrace}");

            exceptionDump(e.InnerException, level + 1);
        }

        exceptionDump(e);

        return sb.ToString();
    }
}
