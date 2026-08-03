using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using VGAudio.Cli;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;

[RegisterSingleton<IAcbGenerateService>]
internal sealed class DefaultAcbGenerateService : IAcbGenerateService
{
    public async Task<AcbGenerateResult> GenerateAsync(
        AcbGenerateOption option,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (!File.Exists(option.InputAudioFilePath))
            return new(false, Lang.ConvertAudioFileNotFound);

        if (option.MusicId < 0)
            return new(false, Lang.MusicIDInvaild.Format(option.MusicId));

        if (string.IsNullOrWhiteSpace(option.OutputFolderPath))
            return new(false, Lang.OutputFolderIsEmpty);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var musicIdStr = option.MusicId.ToString().PadLeft(4, '0');
            var musicSourceName = $"musicsource{musicIdStr}";
            var tempFolder = TempFileHelper.GetTempFolderPath("AcbGen", musicSourceName);
            Log.LogDebug($"AcbGenerateProgram.Generate() tempFolder: {tempFolder}");

            var generated = await Task.Run(
                () => AcbGeneratorFuck.Generator.Generate(
                    option.InputAudioFilePath,
                    $"music{musicIdStr}",
                    tempFolder,
                    false,
                    new Options
                    {
                        Bitrate = 192 * 1024
                    },
                    previewBeginTime: TimeSpan.FromMilliseconds(option.PreviewBeginTime),
                    previewEndTime: TimeSpan.FromMilliseconds(option.PreviewEndTime)),
                cancellationToken);
            if (!generated)
                return new(false, Lang.CallAcbGeneratorFuckFail);

            var generateXmlResult = await GenerateMusicSourceXmlAsync(
                tempFolder,
                option.MusicId,
                cancellationToken);
            if (!generateXmlResult.IsSuccess)
                return generateXmlResult;

            var generatedFiles = Directory.GetFiles(tempFolder);
            if (generatedFiles.Length < 3)
                return new(false, Lang.CallAcbGeneratorFuckFail);

            foreach (var generatedFile in generatedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outputPath = Path.Combine(option.OutputFolderPath, Path.GetFileName(generatedFile));
                File.Copy(generatedFile, outputPath, true);
            }

            return new(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.LogError($"AcbGenerateProgram.Generate() throw exception:{exception.Message}\n{exception.StackTrace}");
            return new(false, $"{Lang.ThrowExceptionWhenConvert}{exception.Message}");
        }
    }

    private static async Task<AcbGenerateResult> GenerateMusicSourceXmlAsync(
        string tempFolder,
        int musicId,
        CancellationToken cancellationToken)
    {
        await using var resourceStream = ResourceUtils.OpenReadResourceStream("MusicSource.xml");
        var musicSourceXml = await XDocument.LoadAsync(
            resourceStream,
            LoadOptions.None,
            cancellationToken);

        var musicIdStr = musicId.ToString().PadLeft(4, '0');

        musicSourceXml.XPathSelectElement("//Name/str")!.Value = musicIdStr;
        musicSourceXml.XPathSelectElement("//Name/id")!.Value = musicIdStr;

        musicSourceXml.XPathSelectElement("//acbFile/path")!.Value = $"music{musicIdStr}.acb";
        musicSourceXml.XPathSelectElement("//awbFile/path")!.Value = $"music{musicIdStr}.awb";

        musicSourceXml.XPathSelectElement("//dataName")!.Value = $"musicsource{musicIdStr}";

        var output = Path.Combine(tempFolder, "MusicSource.xml");
        await using var fileStream = File.Create(output);
        await using var writer = XmlWriter.Create(fileStream, new XmlWriterSettings
        {
            Async = true,
            Encoding = new UTF8Encoding(false),
            Indent = true
        });
        await musicSourceXml.SaveAsync(writer, cancellationToken);

        return new(true);
    }
}
