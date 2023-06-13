using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class TempFileHelper
    {
        const string TempFolder = "NagekiFumenEditorTempFolder";
        const int RandomStringLength = 10;

        /// <summary>
        /// Get new and unused temp file path
        /// </summary>
        /// <param name="subTempFolderName"></param>
        /// <param name="prefix"></param>
        /// <param name="extension"></param>
        /// <returns>will return like "C:\Users\mikir\AppData\Local\Temp\NagekiFumenEditorTempFolder\ParseAndDecodeACBFile\music2857.stUKmOg0Ev.wav"</returns>
        public static string GetTempFilePath(string subTempFolderName = "misc", string prefix = "tempFile", string extension = ".dat")
        {
            extension = extension ?? ".unk";
            if (!extension.StartsWith("."))
                extension = "." + extension;

            var tempFolder = Path.Combine(Path.GetTempPath(), TempFolder, subTempFolderName);
            Directory.CreateDirectory(tempFolder);

            while (true)
            {
                var fullTempFileName = Path.Combine(tempFolder, prefix + "." + RandomHepler.RandomString(RandomStringLength) + extension);
                if (!File.Exists(fullTempFileName))
                    return fullTempFileName;
            }
        }

        public static string GetTempFolderPath(string subTempFolderName = "misc", string prefix = "tempFolder")
        {
            while (true)
            {
                var tempFolder = Path.Combine(Path.GetTempPath(), TempFolder, subTempFolderName, prefix + "_" + RandomHepler.RandomString(RandomStringLength));
                if (!File.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                    return tempFolder;
                }
            }
        }
    }
}
