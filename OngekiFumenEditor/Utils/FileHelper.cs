using System.IO;
using System.Linq;

namespace OngekiFumenEditor.Utils
{
	public static class FileHelper
	{
		readonly static char[] INVAILD_CHARS = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/' }).ToArray();
		private static string[] SIZES = { "B", "KB", "MB", "GB", "TB" };

		public static string FormatFileSize(long bytes)
		{
			var len = (double)bytes;
			int order = 0;
			while (len >= 1024 && order < SIZES.Length - 1)
			{
				order++;
				len = len / 1024;
			}

			return string.Format("{0:0.##} {1}", len, SIZES[order]);
		}

		public static string FilterFileName(string file_name, char repleace_char = '_')
		{
			var result = file_name;

			foreach (var ch in INVAILD_CHARS)
				result = result.Replace(ch, repleace_char);

			return result;
		}

		public static bool IsPathWritable(string filePath)
		{
			//https://stackoverflow.com/questions/876473/is-there-a-way-to-check-if-a-file-is-in-use
			try
			{
				using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
			}
			catch (IOException)
			{
				//the file is unavailable because it is:
				//still being written to
				//or being processed by another thread
				//or does not exist (has already been processed)
				return !File.Exists(filePath);
			}

			return true;
		}
	}
}
