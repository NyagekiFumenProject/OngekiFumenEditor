using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xv2CoreLib;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    internal static class FileLogOutput
    {
        private static readonly Queue<string> contents = new();
        private static readonly object writeLock = new();
        private static TaskCompletionSource<bool> writeCompletion = CreateCompletionSource();
        private static string filePath;
        private static bool isWriting;

        private static TaskCompletionSource<bool> CreateCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            while (true)
            {
                Task completion;
                var startWriter = false;

                lock (writeLock)
                {
                    if (!isWriting && contents.Count == 0)
                        return;

                    if (!isWriting)
                    {
                        isWriting = true;
                        writeCompletion = CreateCompletionSource();
                        startWriter = true;
                    }

                    completion = writeCompletion.Task;
                }

                if (startWriter)
                    StartWriter();

                try
                {
                    completion.GetAwaiter().GetResult();
                }
                catch
                {
                    // The failed batch remains queued for a later retry. There is no useful
                    // synchronous recovery available from the process-exit callback.
                    return;
                }
            }
        }

        public static Task WriteLog(string content)
        {
            ArgumentNullException.ThrowIfNull(content);

            TaskCompletionSource<bool> completion;
            var startWriter = false;

            lock (writeLock)
            {
                contents.Enqueue(content);

                if (!isWriting)
                {
                    isWriting = true;
                    writeCompletion = CreateCompletionSource();
                    startWriter = true;
                }

                completion = writeCompletion;
            }

            if (startWriter)
                StartWriter();

            // Every caller receives the completion for the active batch. In particular,
            // awaiting a log written during shutdown now waits for earlier queued records too.
            return completion.Task;
        }

        public static string GetCurrentLogFile()
        {
            return filePath;
        }

        private static void StartWriter()
        {
            try
            {
                _ = Task.Run(WritePendingContents);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Start file log writer failed : {exception}");
                // Task scheduling normally cannot fail, but keep the queue drainable if the
                // runtime is already shutting down.
                WritePendingContents();
            }
        }

        // Everything queued so far is written in one go: opening and closing the file once per log
        // line costs far more than the write itself. Queuing while holding the lock keeps a line
        // that arrives while the writer is about to stop from waiting for the next log line.
        private static void WritePendingContents()
        {
            while (true)
            {
                string pending;
                lock (writeLock)
                {
                    if (contents.Count == 0)
                    {
                        isWriting = false;
                        writeCompletion.TrySetResult(true);
                        return;
                    }

                    var pendingBuilder = new StringBuilder();
                    while (contents.Count > 0)
                        pendingBuilder.Append(contents.Dequeue());
                    pending = pendingBuilder.ToString();
                }

                try
                {
                    if (filePath is null)
                        throw new InvalidOperationException("File log output is not initialized.");

                    File.AppendAllText(filePath, pending, Encoding.UTF8);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Write log file failed : {exception}");

                    lock (writeLock)
                    {
                        // Restore the failed batch before records that arrived while the file
                        // write was in progress. No message is silently discarded on an I/O error.
                        var later = contents.ToArray();
                        contents.Clear();
                        contents.Enqueue(pending);
                        foreach (var message in later)
                            contents.Enqueue(message);

                        isWriting = false;
                        writeCompletion.TrySetException(exception);
                    }

                    return;
                }
            }
        }
    }

    [Export(typeof(ILogOutput))]
    public class FileLogOutputWrapper : ILogOutput
    {
        public void WriteLog(Severity severity, string content) => FileLogOutput.WriteLog(content);
    }
}
