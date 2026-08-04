using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Utils;

[RegisterSingleton]
public class Log
{
    private record LogRecord(Severity Severity, string Message, bool NewLine, bool Time, string Prefix, string FilePath, int LineNumber);

    private static Log cacheInstance;
    private readonly List<ILogOutput> outputs = [];
    private readonly ConcurrentQueue<LogRecord> logRecordQueue = [];
    private volatile bool isRunning;

    public Log(IEnumerable<ILogOutput> outputs)
    {
        this.outputs.AddRange(outputs);
    }

    private IEnumerable<ILogOutput> LogOutputs => outputs;
    public static Log Instance => cacheInstance ??= IoC.Get<Log>();

    /// <summary>
    /// Configures logging for hosts that do not expose services through Avalonia Application.Current.
    /// </summary>
    public static void Initialize(Log instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        cacheInstance = instance;
    }

    public void RemoveOutput<T>() where T : ILogOutput
    {
        outputs.RemoveAll(x => x is T);
    }

    public void AddOutputIfNotExist<T>() where T : ILogOutput, new()
    {
        if (outputs.OfType<T>().Any())
            return;
        outputs.Add(new T());
    }

    private void Output(Severity severity, string message)
    {
        foreach (var output in LogOutputs)
            output.WriteLog(severity, message);
    }

    private static string BuildLogMessage(LogRecord record)
    {
        var prefix = record.Prefix;
        if (prefix == ".ctor")
            prefix = Path.GetFileNameWithoutExtension(record.FilePath) + prefix;
        prefix += $":{record.LineNumber}";

        var sb = new StringBuilder(256);
        sb.AppendFormat("[{0} {1}:{2}]",
            record.Time ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : string.Empty,
            record.Severity.ToString().ToUpper(),
            Thread.CurrentThread.ManagedThreadId);

        if (!string.IsNullOrWhiteSpace(prefix))
            sb.AppendFormat("<{0}>", prefix);

        sb.AppendFormat(" {0}", record.Message.TrimStart());
        if (record.NewLine)
            sb.AppendLine();

        return sb.ToString();
    }

    private void EnqueueLogRecord(string message, Severity severity, bool newLine, bool time, string prefix, string filePath, int lineNumber)
    {
        logRecordQueue.Enqueue(new LogRecord(severity, message, newLine, time, prefix, filePath, lineNumber));
    }

    private static void BeginLogRecord(string message, Severity severity, bool newLine, bool time, string prefix, string filePath, int lineNumber)
    {
        var log = Instance;
        log.EnqueueLogRecord(message, severity, newLine, time, prefix, filePath, lineNumber);
        log.AwakeLogger();
    }

    [Conditional("DEBUG")]
    public static void LogDebug(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        BeginLogRecord(message, Severity.Debug, newLine, time, prefix, filePath, lineNumber);
    }

    private void AwakeLogger()
    {
        if (isRunning)
            return;

        isRunning = true;
        if (OperatingSystem.IsBrowser())
        {
            ProcessLogRecords();
            return;
        }

        _ = Task.Run(ProcessLogRecords);
    }

    private void ProcessLogRecords()
    {
        try
        {
            while (logRecordQueue.TryDequeue(out var logRecord))
            {
                try
                {
                    var msg = BuildLogMessage(logRecord);
                    Output(logRecord.Severity, msg);
                }
                catch
                {
                }
            }
        }
        finally
        {
            isRunning = false;
            if (!logRecordQueue.IsEmpty)
                AwakeLogger();
        }
    }

    public static void LogInfo(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        BeginLogRecord(message, Severity.Info, newLine, time, prefix, filePath, lineNumber);
    }

    public static void LogWarn(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        BeginLogRecord(message, Severity.Warn, newLine, time, prefix, filePath, lineNumber);
    }

    public static void LogWarning(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        LogWarn(message, newLine, time, prefix, filePath, lineNumber);
    }

    public static void LogError(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        BeginLogRecord(message, Severity.Error, newLine, time, prefix, filePath, lineNumber);
    }

    public static void LogError(string message, Exception e, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        var actualMessage = $"{message}\nContains exception:{e.Message}\n{e.StackTrace}";
        BeginLogRecord(actualMessage, Severity.Error, newLine, time, prefix, filePath, lineNumber);
    }

    public static async Task WaitForAllLogWriteDone()
    {
        var instance = Instance;
        while (instance.isRunning)
            await Task.Delay(10);
    }
}


