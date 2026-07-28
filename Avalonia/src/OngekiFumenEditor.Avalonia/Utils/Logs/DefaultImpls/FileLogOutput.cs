using Injectio.Attributes;
using System.Collections.Concurrent;
using System.Threading;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;

internal static class FileLogOutput
{
    private static readonly ConcurrentQueue<string> contents = [];
    private static readonly object locker = new();
    private static volatile bool isWriting;

    private static readonly string filePath = Path.Combine(
        TempFileHelper.GetTempFolderPath("logs", "runtime", random: false),
        $"{DateTime.Now:yyyyMMdd_HHmmss}.log");

    public static void WaitForWriteDone()
    {
        while (isWriting)
            Thread.Sleep(0);
    }

    public static Task WriteLog(string content)
    {
        contents.Enqueue(content);
        return NotifyWrite();
    }

    public static string GetCurrentLogFile() => filePath;

    private static async Task NotifyWrite()
    {
        if (isWriting)
            return;

        lock (locker)
        {
            if (isWriting)
                return;
            isWriting = true;
        }

        await Task.Run(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            while (contents.TryDequeue(out var msg))
                File.AppendAllText(filePath, msg);
            isWriting = false;
        });
    }
}

[RegisterSingleton<ILogOutput>]
public class FileLogOutputWrapper : ILogOutput
{
    public void WriteLog(Severity severity, string content) => FileLogOutput.WriteLog(content);
}


