using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xv2CoreLib;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    internal static class FileLogOutput
    {
        static ConcurrentQueue<string> contents = new();
        static readonly object writeLock = new();
        static string filePath;
        static volatile bool isWriting = false;

        public static void Init()
        {
            try
            {
                var logDir = AppDirectoryHelper.ResolveRelative(LogSetting.Default.LogFileDirPath);
                Directory.CreateDirectory(logDir);
                do
                {
                    filePath = Path.GetFullPath(Path.Combine(logDir, FileHelper.FilterFileName(DateTime.Now.ToString() + ".log")));
                } while (File.Exists(filePath));

                WriteLog("----------BEGIN FILE LOG OUTPUT----------\n");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Create log file failed : {e.Message}");
            }
        }

        public static void WaitForWriteDone()
        {
            var spinWait = new SpinWait();
            while (isWriting)
                spinWait.SpinOnce();
        }

        public static Task WriteLog(string content)
        {
            lock (writeLock)
            {
                contents.Enqueue(content);

                if (isWriting)
                    return Task.CompletedTask;

                isWriting = true;
            }

            return Task.Run(WritePendingContents);
        }

        public static string GetCurrentLogFile()
        {
            return filePath;
        }

        // Everything queued so far is written in one go: opening and closing the file once per log
        // line costs far more than the write itself. Queuing while holding the lock keeps a line
        // that arrives as the writer is about to stop from waiting for the next log line.
        private static void WritePendingContents()
        {
            while (true)
            {
                try
                {
                    var pending = new StringBuilder();
                    while (contents.TryDequeue(out var msg))
                        pending.Append(msg);

                    if (filePath != null && pending.Length > 0)
                        File.AppendAllText(filePath, pending.ToString());
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Write log file failed : {e.Message}");
                }

                lock (writeLock)
                {
                    if (!contents.IsEmpty)
                        continue;

                    isWriting = false;
                    return;
                }
            }
        }
    }

    [Export(typeof(ILogOutput))]
    public class FileLogOutputWrapper : ILogOutput
    {
        public void WriteLog(Severity severity , string content) => FileLogOutput.WriteLog(content);
    }
}
