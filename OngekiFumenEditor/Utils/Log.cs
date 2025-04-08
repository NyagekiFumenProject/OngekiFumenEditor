using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using OngekiFumenEditor.Utils.Logs;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils;

[Export(typeof(Log))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class Log
{
    private record LogRecord(Severity Severity, string Message, bool NewLine, bool Time, string Prefix, string FilePath, int LineNumber);

    private static Log cacheInstance;
    private readonly List<ILogOutput> outputs = new();

    private static ConcurrentQueue<LogRecord> logRecordQueue = new();

    [ImportingConstructor]
    public Log([ImportMany] IEnumerable<ILogOutput> outputs)
    {
        this.outputs.AddRange(outputs);
    }

    private IEnumerable<ILogOutput> LogOutputs => outputs;
    public static Log Instance => cacheInstance ?? (cacheInstance = IoC.Get<Log>());

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

    internal void Output(Severity severity, string message)
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

        using var _d = ObjectPool.ObjectPool.GetWithUsingDisposable<StringBuilder>(out var sb, out _);
        sb.Clear();

        sb.AppendFormat("[{0} {1}:{2}]", record.Time ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : string.Empty,
            record.Severity.ToString().ToUpper(), Thread.CurrentThread.ManagedThreadId);

        if (!string.IsNullOrWhiteSpace(prefix))
            sb.AppendFormat("<{0}>", prefix);

        sb.AppendFormat(" {0}", record.Message.TrimStart());

        if (record.NewLine)
            sb.AppendLine();

        return sb.ToString();
    }

    private static void EnqueueLogRecord(string message, Severity severity, bool new_line, bool time, string prefix, string filePath, int lineNumber)
    {
        logRecordQueue.Enqueue(new LogRecord(severity, message, new_line, time, prefix, filePath, lineNumber));
    }

    [Conditional("DEBUG")]
    public static void LogDebug(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        var severity = Severity.Debug;
        EnqueueLogRecord(message, severity, newLine, time, prefix, filePath, lineNumber);
        AwakeLogger();
    }

    private static volatile bool isRunning = false;

    private static void AwakeLogger()
    {
        if (isRunning)
            return;

        isRunning = true;

        Task.Run(() =>
        {
            while (logRecordQueue.TryDequeue(out var logRecord))
            {
                try
                {
                    var msg = BuildLogMessage(logRecord);
                    Instance.Output(logRecord.Severity, msg);
                }
                catch (Exception e)
                {
#if DEBUG
                    throw;
#endif
                }
            }

            isRunning = false;
        });
    }

    public static void LogInfo(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        var severity = Severity.Info;
        EnqueueLogRecord(message, severity, newLine, time, prefix, filePath, lineNumber);
        AwakeLogger();
    }

    public static void LogWarn(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        var severity = Severity.Warn;
        EnqueueLogRecord(message, severity, newLine, time, prefix, filePath, lineNumber);
        AwakeLogger();
    }

    public static void LogError(string message, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        var severity = Severity.Error;
        EnqueueLogRecord(message, severity, newLine, time, prefix, filePath, lineNumber);
        AwakeLogger();
    }

    public static void LogError(string message, Exception e, bool newLine = true, bool time = true,
        [CallerMemberName] string prefix = "<Unknown>", [CallerFilePath] string filePath = default, [CallerLineNumber] int lineNumber = 0)
    {
        var severity = Severity.Error;
        EnqueueLogRecord(message, severity, newLine, time, prefix, filePath, lineNumber);
        AwakeLogger();
    }
}