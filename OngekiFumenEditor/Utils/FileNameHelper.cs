using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class FileNameHelper
    {
        readonly static char[] INVAILD_CHARS = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/' }).ToArray();

        public static string FilterFileName(string file_name, char repleace_char = '_')
        {
            var result = file_name;

            foreach (var ch in INVAILD_CHARS)
                result = result.Replace(ch, repleace_char);

            return result;
        }
    }
}
