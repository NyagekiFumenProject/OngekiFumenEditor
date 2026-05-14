using System.IO;
using System.Linq;

namespace OngekiFumenEditor.Core.Utils
{
    public static class FileHelper
    {
        private static readonly char[] INVAILD_CHARS = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/' }).ToArray();
        private static readonly string[] SIZES = { "B", "KB", "MB", "GB", "TB" };

        public static string FormatFileSize(long bytes)
        {
            var len = (double)bytes;
            var order = 0;
            while (len >= 1024 && order < SIZES.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return string.Format("{0:0.##} {1}", len, SIZES[order]);
        }

        public static string FilterFileName(string fileName, char replaceChar = '_')
        {
            var result = fileName;

            foreach (var ch in INVAILD_CHARS)
                result = result.Replace(ch, replaceChar);

            return result;
        }

        public static bool IsPathWritable(string filePath)
        {
            try
            {
                using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return !File.Exists(filePath);
            }

            return true;
        }
    }
}

