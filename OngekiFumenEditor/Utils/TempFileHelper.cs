using System.IO;

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
		public static string GetTempFilePath(string subTempFolderName = "misc", string prefix = "tempFile", string extension = ".dat", bool random = true)
		{
			extension = extension ?? ".unk";
			if (!extension.StartsWith("."))
				extension = "." + extension;

			var tempFolder = Path.Combine(Path.GetTempPath(), TempFolder, subTempFolderName);
			Directory.CreateDirectory(tempFolder);

			while (true)
			{
				var actualPrefix = random ? (prefix + "." + RandomHepler.RandomString(RandomStringLength)) : prefix;
				var fullTempFileName = Path.Combine(tempFolder, actualPrefix + extension);
				if (!File.Exists(fullTempFileName))
					return fullTempFileName;
			}
		}

		public static string GetTempFolderPath(string subTempFolderName = "misc", string prefix = "tempFolder", bool random = true)
		{
			while (true)
			{
				var actualPrefix = random ? (prefix + "_" + RandomHepler.RandomString(RandomStringLength)) : prefix;
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
