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

                WriteLog("----------BEGIN FILE LOG OUTPUT----------\n");
            }
            catch (Exception e)
            {
                writer = null;
                Debug.WriteLine($"Create log file failed : {e.Message}");
            }
        }

        public static void Term()
        {
            while (writing);
            if (writer is null)
                return;
            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        public static void WaitForWriteDone()
        {
            while (writing)
                Thread.Sleep(0);
        }

        public static void WriteLog(string content)
        {
            if (writer is null)
                return;
            contents.Enqueue(content);

            NotifyWrite();
        }

        private static async void NotifyWrite()
        {
            if (writing)
                return;
            await Task.Run(() =>
            {
                writing = true;
                while (contents.Count > 0 && writer is not null)
                {
                    var msg = contents.Dequeue();
                    writer.Write(msg);
                }
                writer.Flush();
                writing = false;
            });
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
