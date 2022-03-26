using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    internal static class FileLogOutput
    {
        static StreamWriter writer;
        static Queue<string> contents = new Queue<string>();
        static volatile bool writing = false;

        public static void Init()
        {
            try
            {
                var logDir = LogSetting.Default.LogFileDirPath;
                Directory.CreateDirectory(logDir);
                var filePath = "";
                do
                {
                    filePath = Path.Combine(logDir, FileNameHelper.FilterFileName(DateTime.Now.ToString() + ".log"));
                } while (File.Exists(filePath));
                writer = new StreamWriter(File.OpenWrite(filePath));
            }
            catch (Exception e)
            {
                writer = null;
                Debug.WriteLine($"Create log file failed : {e.Message}");
            }
        }

        public static async Task Term()
        {
            while (writing)
                await Task.Delay(10);
            if (writer is null)
                return;
            await writer.FlushAsync();
            await writer.DisposeAsync();
            writer = null;
        }

        public static void WriteLog(string content)
        {
            if (writer is null)
                return;
            contents.Enqueue(content);

            if (writing)
                return;

            NotifyWrite();
        }

        private static async void NotifyWrite()
        {
            writing = true;
            while (contents.Count > 0 && writer is not null)
            {
                var msg = contents.Dequeue();
                await writer.WriteLineAsync(msg);
            }
            writing = false;
        }
    }

    [Export(typeof(ILogOutput))]
    public class FileLogOutputWrapper : ILogOutput
    {
        public void WriteLog(string content)
        {
            FileLogOutput.WriteLog(content);
        }
    }
}
