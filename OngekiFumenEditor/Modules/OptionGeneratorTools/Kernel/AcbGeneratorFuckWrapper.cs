using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel
{
	public static class AcbGeneratorFuckWrapper
	{
		public static async Task<GenerateResult> Generate(AcbGenerateOption option)
		{
			if (!File.Exists(option.InputAudioFilePath))
				return new(false, Resources.ConvertAudioFileNotFound);

			if (option.MusicId < 0 || option.MusicId > 9999)
				return new(false, Resources.MusicIDInvaild.Format(option.MusicId));

			if (string.IsNullOrWhiteSpace(option.OutputFolderPath))
				return new(false, Resources.OutputFolderIsEmpty);
			try
			{
				var musicIdStr = option.MusicId.ToString().PadLeft(4, '0');
				var musicSourceName = $"musicsource{musicIdStr}";
				var tempFolder = TempFileHelper.GetTempFolderPath("AcbGen", musicSourceName);
				Log.LogDebug($"AcbGenerateProgram.Generate() tempFolder: {tempFolder}");

				var result = await Task.Run(() => AcbGeneratorFuck.Generator.Generate(
						option.InputAudioFilePath,
						$"music{musicIdStr}",
						tempFolder,
						false,
						new VGAudio.Cli.Options()
						{
							Bitrate = 192 * 1024,
						},
						previewBeginTime: TimeSpan.FromMilliseconds(option.PreviewBeginTime),
						previewEndTime: TimeSpan.FromMilliseconds(option.PreviewEndTime)
						));

				var genResult = await GenerateMusicSourceXmlAsync(tempFolder, option.MusicId);
				if (!genResult.IsSuccess)
					return genResult;

				var genFiles = Directory.GetFiles(tempFolder);
				if (genFiles.Length < 2)
					return new(false, Resources.CallAcbGeneratorFuckFail);

				foreach (var genFile in genFiles)
				{
					var outputPath = Path.Combine(option.OutputFolderPath, Path.GetFileName(genFile));
					File.Copy(genFile, outputPath, true);
				}

				return new(true);
			}
			catch (Exception e)
			{
				Log.LogError($"AcbGenerateProgram.Generate() throw exception:{e.Message}\n{e.StackTrace}");
				return new(false, $"{Resources.ThrowExceptionWhenConvert}{e.Message}");
			}
		}

		private static async Task<GenerateResult> GenerateMusicSourceXmlAsync(string tempFolder, int musicId)
		{
			using var resStream = typeof(JacketGenerateWrapper).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources.MusicSource.xml");
			var musicSourceXml = await XDocument.LoadAsync(resStream, LoadOptions.None, default);

			var musicIdStr = musicId.ToString().PadLeft(4, '0');

			musicSourceXml.XPathSelectElement("//Name/str").Value = $"{musicIdStr}";
			musicSourceXml.XPathSelectElement("//Name/id").Value = $"{musicIdStr}";

			musicSourceXml.XPathSelectElement("//acbFile/path").Value = $"music{musicIdStr}.acb";
			musicSourceXml.XPathSelectElement("//awbFile/path").Value = $"music{musicIdStr}.awb";

			musicSourceXml.XPathSelectElement("//dataName").Value = $"musicsource{musicIdStr}";

			var output = Path.Combine(tempFolder, "MusicSource.xml");
			using var fs = File.OpenWrite(output);
			using var writer = XmlWriter.Create(fs, new XmlWriterSettings()
			{
				Async = true,
				Encoding = Encoding.UTF8,
				Indent = true
			});
			await musicSourceXml.SaveAsync(writer, default);

			return new(true);
		}
	}
}
