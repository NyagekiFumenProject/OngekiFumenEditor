using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Avalonia.Utils;

internal static class DocumentOpenHelper
{
    public static async Task<bool> TryOpenAsDocument(ISimpleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.FileName.EndsWith(".ogkr", StringComparison.OrdinalIgnoreCase) ||
            file.FileName.EndsWith(".nyageki", StringComparison.OrdinalIgnoreCase))
        {
            return await TryOpenOgkrFileAsDocument(file);
        }

        file.Dispose();
        return false;
    }

    public static async Task<bool> TryOpenAsDocument(string filePath)
    {
        var provider = PickEditorProvider(filePath);
        if (provider is not null)
        {
            Log.LogInfo($"Open document by provider {provider.GetType().Name}: {filePath}");
            var document = provider.Create();
            var shouldShow = provider switch
            {
                IFumenVisualEditorProvider fumenProvider => await fumenProvider.TryOpen(document, filePath),
                _ => await provider.TryOpen(document)
            };

            if (shouldShow)
            {
                await IoC.Get<IShell>().OpenDocumentAsync(document);
                return true;
            }
        }

        if (filePath.EndsWith(".ogkr", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".nyageki", StringComparison.OrdinalIgnoreCase))
            return await TryOpenOgkrFileAsDocument(filePath);

        return false;
    }

    public static async Task<bool> TryOpenOgkrFileAsDocument(string ogkrFilePath)
    {
        var newProj = await TryCreateEditorProjectDataModel(ogkrFilePath);
        if (newProj is null)
            return false;

        var ownershipTransferred = false;
        try
        {
            var docName = await TryFormatOpenFileName(ogkrFilePath);
            var provider = IoC.Get<IFumenVisualEditorProvider>();
            var editor = provider.Create();
            var shouldShow = await provider.TryOpen(editor, newProj);
            if (!shouldShow)
                return false;

            if (editor is FumenVisualEditorViewModel vm)
                vm.DisplayName = docName;

            await IoC.Get<IShell>().OpenDocumentAsync(editor);
            ownershipTransferred = true;
            return true;
        }
        finally
        {
            if (!ownershipTransferred)
                newProj.DisposeRuntimeFiles();
        }
    }

    public static async Task<bool> TryOpenOgkrFileAsDocument(ISimpleFile ogkrFile)
    {
        ArgumentNullException.ThrowIfNull(ogkrFile);

        EditorProjectDataModel newProj = null;
        var ownershipTransferred = false;
        try
        {
            newProj = await TryCreateEditorProjectDataModel(ogkrFile);
            if (newProj is null)
                return false;

            var provider = IoC.Get<IFumenVisualEditorProvider>();
            var editor = provider.Create();
            var shouldShow = await provider.TryOpen(editor, newProj);
            if (!shouldShow)
                return false;

            if (editor is FumenVisualEditorViewModel vm)
                vm.DisplayName = $"[{Lang.FastOpen}] {ogkrFile.FileName}";

            await IoC.Get<IShell>().OpenDocumentAsync(editor);
            ownershipTransferred = true;
            return true;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                if (newProj is null)
                    ogkrFile.Dispose();
                else
                    newProj.DisposeRuntimeFiles();
            }
        }
    }

    public static async Task<bool> TryOpenProject(EditorProjectDataModel proj)
    {
        if (proj is null)
            return false;

        var ownershipTransferred = false;
        try
        {
            var provider = IoC.Get<IFumenVisualEditorProvider>();
            var editor = provider.Create();
            var shouldShow = await provider.TryOpen(editor, proj);
            if (!shouldShow)
                return false;

            await IoC.Get<IShell>().OpenDocumentAsync(editor);
            ownershipTransferred = true;
            return true;
        }
        finally
        {
            if (!ownershipTransferred)
                proj.DisposeRuntimeFiles();
        }
    }

    public static async Task<EditorProjectDataModel> TryCreateEditorProjectDataModel(string ogkrFilePath)
    {
        ISimpleFile selectedAudioFile = null;
        try
        {
            (var audioFile, var audioDuration) = await GetAudioFilePath(ogkrFilePath);
            if (!File.Exists(audioFile))
            {
                selectedAudioFile = await FileDialogHelper.OpenFileAsync(
                    Lang.SelectAudioFileManually,
                    IoC.Get<IAudioManager>().SupportAudioFileExtensionList);
                if (selectedAudioFile is null)
                    return null;
                audioDuration = await CalcAudioDuration(selectedAudioFile);
                audioFile = selectedAudioFile.LocalPath ?? selectedAudioFile.FullPath;
            }

            using var fs = File.OpenRead(ogkrFilePath);
            var parserManager = IoC.Get<IFumenParserManager>();
            var deserializer = parserManager.GetDeserializer(ogkrFilePath);
            if (deserializer is null)
                return null;
            var fumen = await deserializer.DeserializeAsync(fs);

            var model = new EditorProjectDataModel
            {
                FumenFilePath = ogkrFilePath,
                Fumen = fumen,
                AudioFilePath = audioFile,
                AudioDuration = audioDuration,
                AudioFile = selectedAudioFile
            };
            selectedAudioFile = null;
            return model;
        }
        finally
        {
            selectedAudioFile?.Dispose();
        }
    }

    public static async Task<EditorProjectDataModel> TryCreateEditorProjectDataModel(ISimpleFile ogkrFile)
    {
        ArgumentNullException.ThrowIfNull(ogkrFile);

        ISimpleFile selectedAudioFile = null;
        try
        {
            string audioFilePath = null;
            TimeSpan audioDuration = default;

            if (!string.IsNullOrWhiteSpace(ogkrFile.LocalPath))
                (audioFilePath, audioDuration) = await GetAudioFilePath(ogkrFile.LocalPath);

            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
            {
                selectedAudioFile = await FileDialogHelper.OpenFileAsync(
                    Lang.SelectAudioFileManually,
                    IoC.Get<IAudioManager>().SupportAudioFileExtensionList);
                if (selectedAudioFile is null)
                    return null;

                audioDuration = await CalcAudioDuration(selectedAudioFile);
                audioFilePath = selectedAudioFile.LocalPath ?? selectedAudioFile.FullPath;
            }

            var parserManager = IoC.Get<IFumenParserManager>();
            var deserializer = parserManager.GetDeserializer(ogkrFile.FileName);
            if (deserializer is null)
                return null;

            await using var fumenStream = await ogkrFile.OpenRead();
            var fumen = await deserializer.DeserializeAsync(fumenStream);
            var model = new EditorProjectDataModel
            {
                FumenFilePath = ogkrFile.LocalPath ?? ogkrFile.FullPath,
                FumenFile = ogkrFile,
                Fumen = fumen,
                AudioFilePath = audioFilePath,
                AudioDuration = audioDuration,
                AudioFile = selectedAudioFile
            };
            selectedAudioFile = null;
            return model;
        }
        finally
        {
            selectedAudioFile?.Dispose();
        }
    }

    public static async Task<string> TryFormatOpenFileName(string ogkrFilePath)
    {
        var result = Path.GetFileName(ogkrFilePath);
        var ogkrFileDir = Path.GetDirectoryName(ogkrFilePath) ?? string.Empty;
        var musicXmlFilePath = Path.Combine(ogkrFileDir, "Music.xml");

        if (File.Exists(musicXmlFilePath))
        {
            await using var xmlStream = File.OpenRead(musicXmlFilePath);
            var musicXml = await XDocument.LoadAsync(xmlStream, LoadOptions.None, default);
            var element = musicXml.XPathSelectElement(@"//Name[1]/str[1]");
            if (element?.Value is string name)
                result = name;
        }

        return $"[{Lang.FastOpen}] {result}";
    }

    private static async Task<(string, TimeSpan)> GetAudioFilePath(string ogkrFilePath)
    {
        var ogkrFileDir = Path.GetDirectoryName(ogkrFilePath) ?? string.Empty;
        var musicXmlFilePath = Path.Combine(ogkrFileDir, "Music.xml");
        var musicId = -2857;

        if (File.Exists(musicXmlFilePath))
        {
            await using var xmlStream = File.OpenRead(musicXmlFilePath);
            var musicXml = await XDocument.LoadAsync(xmlStream, LoadOptions.None, default);
            var element = musicXml.XPathSelectElement(@"//MusicSourceName[1]/id[1]");
            if (element is not null && int.TryParse(element.Value, out var parsed))
                musicId = parsed;
        }

        if (musicId < 0)
        {
            var match = new Regex(@"(\d+)_\d+").Match(Path.GetFileNameWithoutExtension(ogkrFilePath));
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
                musicId = parsed;
        }

        if (musicId < 0)
            return default;

        var musicIdStr = musicId < 1000 ? string.Concat("0".Repeat(4 - musicId.ToString().Length)) + musicId : musicId.ToString();
        var musicSourceFolder = Path.GetFullPath(Path.Combine(ogkrFileDir, "..", "..", "musicsource", $"musicsource{musicIdStr}"));
        var audioExts = IoC.Get<IAudioManager>().SupportAudioFileExtensionList.Select(x => x.fileExt.TrimStart('.')).ToArray();
        var audioFile = string.Empty;

        if (!Directory.Exists(musicSourceFolder))
        {
            var idx = ogkrFileDir.LastIndexOf("/package", StringComparison.OrdinalIgnoreCase);
            idx = idx < 0 ? ogkrFileDir.LastIndexOf("\\package", StringComparison.OrdinalIgnoreCase) : idx;
            if (idx >= 0)
            {
                var packageFolder = ogkrFilePath.Substring(0, "/package".Length + idx);
                musicSourceFolder = Directory.GetDirectories(packageFolder, $"musicsource{musicIdStr}", SearchOption.AllDirectories).FirstOrDefault();
            }
        }

        if (Directory.Exists(musicSourceFolder))
        {
            audioFile = Directory.GetFiles(musicSourceFolder, $"music{musicIdStr}.*")
                .FirstOrDefault(x => audioExts.Any(t => x.EndsWith(t, StringComparison.OrdinalIgnoreCase)));
        }

        if (!File.Exists(audioFile))
            return default;

        return (audioFile, await CalcAudioDuration(audioFile));
    }

    private static async Task<TimeSpan> CalcAudioDuration(string audioFilePath)
    {
        using var audioFile = new LocalSimpleFile(audioFilePath);
        using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(audioFile);
        return audio.Duration;
    }

    private static async Task<TimeSpan> CalcAudioDuration(ISimpleFile audioFile)
    {
        using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(audioFile);
        return audio.Duration;
    }

    private static IEditorProvider PickEditorProvider(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return IoC.GetAll<IEditorProvider>().FirstOrDefault(x => x.FileTypes.Any(t =>
            (t.Patterns ?? []).Any(p => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase))));
    }
}

