using System.IO;

namespace OngekiFumenEditor.Utils
{
    public static class TempFileHelper
    {
        private const string TempFolder = "NagekiFumenEditorTempFolder";
        private const int RandomStringLength = 10;

        public static string GetTempFilePath(string subTempFolderName = "misc", string prefix = "tempFile", string extension = ".dat", bool random = true)
        {
            extension = extension ?? ".unk";
            if (!extension.StartsWith("."))
                extension = "." + extension;

            var tempFolder = Path.Combine(Path.GetTempPath(), TempFolder, subTempFolderName);
            Directory.CreateDirectory(tempFolder);

            while (true)
            {
                var actualPrefix = random ? prefix + "." + RandomHepler.RandomString(RandomStringLength) : prefix;
                var fullTempFileName = Path.Combine(tempFolder, actualPrefix + extension);
                if (!File.Exists(fullTempFileName))
                    return fullTempFileName;
            }
        }

        public static string GetTempFolderPath(string subTempFolderName = "misc", string prefix = "tempFolder", bool random = true)
        {
            while (true)
            {
                var actualPrefix = random ? prefix + "_" + RandomHepler.RandomString(RandomStringLength) : prefix;
                var tempFolder = Path.Combine(Path.GetTempPath(), TempFolder, subTempFolderName, actualPrefix);
                if (!File.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                    return tempFolder;
                }
            }
        }
    }
}
