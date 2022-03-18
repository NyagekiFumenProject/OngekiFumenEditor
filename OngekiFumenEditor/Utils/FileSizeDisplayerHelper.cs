using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class FileSizeDisplayerHelper
    {
        private static string[] SIZES = { "B", "KB", "MB", "GB", "TB" };

        public static string Format(long bytes)
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
    }
}
