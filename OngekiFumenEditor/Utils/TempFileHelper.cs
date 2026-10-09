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

            // A deterministic name is also used as a lookup key (for example, the
            // persistent image cache).  Once that file exists, returning the same
            // path is the intended behavior; checking for existence here would
            // otherwise spin forever.
            if (!random)
                return Path.Combine(tempFolder, prefix + extension);

            while (true)
            {
                var actualPrefix = prefix + "." + RandomHepler.RandomString(RandomStringLength);
                var fullTempFileName = Path.Combine(tempFolder, actualPrefix + extension);
                if (!File.Exists(fullTempFileName))
                    return fullTempFileName;
            }
        }

        public static string GetTempFolderPath(string subTempFolderName = "misc", string prefix = "tempFolder", bool random = true)
        {
            var baseFolder = Path.Combine(Path.GetTempPath(), TempFolder, subTempFolderName);
            if (!random)
            {
                var deterministicFolder = Path.Combine(baseFolder, prefix);
                Directory.CreateDirectory(deterministicFolder);
                return deterministicFolder;
            }

            while (true)
            {
                var actualPrefix = prefix + "_" + RandomHepler.RandomString(RandomStringLength);
                var tempFolder = Path.Combine(baseFolder, actualPrefix);
                if (!Directory.Exists(tempFolder) && !File.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                    return tempFolder;
                }
            }
        }
    }
}

