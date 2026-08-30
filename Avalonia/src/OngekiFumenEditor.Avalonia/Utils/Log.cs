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
    private readonly record struct LogRecord(Severity Severity, string Message, bool NewLine, bool Time, string Prefix, string FilePath, int LineNumber);

    private static Log cacheInstance;
    private readonly List<ILogOutput> outputs = [];
    private readonly ConcurrentQueue<LogRecord> logRecordQueue = [];

    [ThreadStatic]
    private static StringBuilder messageBuilder;
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

    /// <summary>供外部日志体系(MEL等)向门面广播队列提交已格式化的消息。</summary>
    public void Emit(ILogOutput.Severity severity, string message, string prefix = null)
    {
        EnqueueLogRecord(message, severity, newLine: true, time: true, prefix, filePath: null, lineNumber: -1);
        AwakeLogger();
    }

    private void Output(Severity severity, string message)
    {
        foreach (var output in LogOutputs)
            output.WriteLog(severity, message);
    }

    private static string SeverityText(Severity severity) => severity switch
    {
        Severity.Debug => "DEBUG",
        Severity.Info => "INFO",
        Severity.Warn => "WARN",
        Severity.Error => "ERROR",
        _ => severity.ToString(),
    };

    private static string BuildLogMessage(LogRecord record)
    {
        // 复用线程级 StringBuilder，热路径仅最终 ToString 产生一次分配。
        var sb = messageBuilder ??= new StringBuilder(256);
        sb.Clear();

        sb.Append('[');
        if (record.Time)
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.Append(' ').Append(SeverityText(record.Severity))
          .Append(':').Append(Thread.CurrentThread.ManagedThreadId).Append(']');

        var prefix = record.Prefix;
        var filePath = Path.GetFileNameWithoutExtension(record.FilePath);

        var hasFilePath = !string.IsNullOrWhiteSpace(filePath);
        var hasPrefix = !string.IsNullOrWhiteSpace(prefix);

        if (hasFilePath || hasPrefix)
        {
            sb.Append('<');
            if (hasFilePath)
                sb.Append(Path.GetFileNameWithoutExtension(record.FilePath));

            if (hasPrefix)
            {
                if (hasFilePath)
                    sb.Append('.');
                sb.Append(prefix);
            }

            if (record.LineNumber > 0)
                sb.Append(':').Append(record.LineNumber);
            sb.Append('>');
        }

        sb.Append(' ').Append(record.Message.TrimStart());
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

    public static void LogWarn(string message, Exception e, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        LogWarn($"{message}\nContains exception:{e.Message}\n{e.StackTrace}", newLine, time, prefix, filePath, lineNumber);
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


