using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Browser.Utils;

public class ConsoleLoggerProvider : ILoggerProvider
{
    private static readonly DateTime startTime;
    private readonly Dictionary<string, ILogger> cachedLoggers = new();

    static ConsoleLoggerProvider()
    {
        startTime = DateTime.Now;
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (cachedLoggers.TryGetValue(categoryName, out var logger))
            return logger;

        return cachedLoggers[categoryName] = new ConsoleLogger(categoryName, startTime);
    }

    public void Dispose()
    {
    }

    private class ConsoleLogger : ILogger
    {
        private readonly string categoryName;
        private readonly string simpliedCategoryName;
        private readonly DateTime startTime;

        public ConsoleLogger(string categoryName, DateTime startTime)
        {
            this.categoryName = categoryName;
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
            if (!IsEnabled(logLevel))
                return;

            var now = DateTime.Now;

            var levelStr = logLevel switch
            {
                LogLevel.Information => "Info",
                _ => logLevel.ToString()
            };

            var overDays = (int) (now - ConsoleLoggerProvider.startTime).TotalDays;
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

            Interops.JsConsoleLogInterop.Log(logRecord);
            //Console.Write(logRecord);
        }

        private string BuildExceptionMessageContent(Exception e)
        {
            StringBuilder sb = new();

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
}