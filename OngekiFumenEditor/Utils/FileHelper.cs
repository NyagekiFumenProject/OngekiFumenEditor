using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
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

        public static string GetBackupFilePath(string filePath)
            => Path.GetFullPath(filePath) + ".bak";

        public static void WriteAllBytesAtomic(string filePath, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new IOException($"Unable to determine destination directory: {fullPath}");
            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                ReplaceWithBackup(tempPath, fullPath);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static async Task WriteAllBytesAtomicAsync(string filePath, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new IOException($"Unable to determine destination directory: {fullPath}");
            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(data.AsMemory());
                    await stream.FlushAsync();
                    stream.Flush(true);
                }

                ReplaceWithBackup(tempPath, fullPath);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static Task WriteAllTextAtomicAsync(string filePath, string content, Encoding encoding = null)
            => WriteAllBytesAtomicAsync(filePath, (encoding ?? new UTF8Encoding(false)).GetBytes(content ?? string.Empty));

        public static void WriteAllTextAtomic(string filePath, string content, Encoding encoding = null)
            => WriteAllBytesAtomic(filePath, (encoding ?? new UTF8Encoding(false)).GetBytes(content ?? string.Empty));

        private static void ReplaceWithBackup(string tempPath, string destinationPath)
        {
            var backupPath = GetBackupFilePath(destinationPath);
            if (File.Exists(destinationPath))
                File.Replace(tempPath, destinationPath, backupPath, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, destinationPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Keep the original file intact when cleanup itself fails.
            }
        }
    }
}

