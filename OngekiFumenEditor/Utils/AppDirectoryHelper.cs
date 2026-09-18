using System;
using System.IO;

namespace OngekiFumenEditor.Utils
{
    public static class AppDirectoryHelper
    {
        public static string ExecutableDirectory { get; } =
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        public static string ResolveRelative(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ExecutableDirectory;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ExecutableDirectory, path));
        }

        public static string Combine(params string[] paths)
        {
            var parts = new string[paths.Length + 1];
            parts[0] = ExecutableDirectory;
            Array.Copy(paths, 0, parts, 1, paths.Length);
            return Path.Combine(parts);
        }
    }
}
