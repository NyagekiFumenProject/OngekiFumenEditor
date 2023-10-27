using OngekiFumenEditor.Properties;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
	internal static class FileLogOutput
	{
		static StreamWriter writer;
		static ConcurrentQueue<string> contents = new();
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
					filePath = Path.Combine(logDir, FileHelper.FilterFileName(DateTime.Now.ToString() + ".log"));
				} while (File.Exists(filePath));
				writer = new StreamWriter(File.OpenWrite(filePath), Encoding.UTF8);

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
			while (writing) ;
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

		public static Task WriteLog(string content)
		{
			if (writer is null)
				return Task.CompletedTask;
			contents.Enqueue(content);

			return NotifyWrite();
		}

		private static async Task NotifyWrite()
		{
			if (writing)
				return;
			await Task.Run(() =>
			{
				writing = true;
				while (writer is not null && contents.TryDequeue(out var msg))
					writer.Write(msg);
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
