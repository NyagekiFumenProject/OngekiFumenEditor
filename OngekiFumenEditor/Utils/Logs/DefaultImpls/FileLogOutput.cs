using OngekiFumenEditor.Properties;
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
        static string filePath;
        static volatile bool isWriting = false;

        public static void Init()
        {
            try
            {
                var logDir = LogSetting.Default.LogFileDirPath;
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
            while (isWriting)
                Thread.Sleep(0);
        }

        public static Task WriteLog(string content)
        {
            contents.Enqueue(content);
            return NotifyWrite();
        }

        public static string GetCurrentLogFile()
        {
            return filePath;
        }

        private static async Task NotifyWrite()
        {
            if (isWriting)
                return;
            isWriting = true;
            await Task.Run(() =>
            {
                while (filePath != null && contents.TryDequeue(out var msg))
                    File.AppendAllText(filePath, msg);
                isWriting = false;
            });
        }
    }

    [Export(typeof(ILogOutput))]
    public class FileLogOutputWrapper : ILogOutput
    {
        public void WriteLog(Severity severity , string content) => FileLogOutput.WriteLog(content);
    }
}
