using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;

/// <summary>
///     Desktop 的 FastOpen：选择 .ogkr/.nyageki 谱面，自动发现（或手动选择）音频，
///     构造 ProjectFile 为空的内存工程并交给编辑器加载，不创建 .nyagekiProj。
/// </summary>
[RegisterSingleton]
public sealed class DesktopFastOpenService
{
    private readonly IAudioManager audioManager;
    private readonly IFumenVisualEditorProvider editorProvider;
    private readonly IFumenParserManager parserManager;
    private readonly IDialogManager dialogManager;
    private readonly IServiceProvider serviceProvider;

    public DesktopFastOpenService(
        IAudioManager audioManager,
        IFumenVisualEditorProvider editorProvider,
        IFumenParserManager parserManager,
        IDialogManager dialogManager,
        IServiceProvider serviceProvider)
    {
        this.audioManager = audioManager;
        this.editorProvider = editorProvider;
        this.parserManager = parserManager;
        this.dialogManager = dialogManager;
        this.serviceProvider = serviceProvider;
    }

    public async Task OpenAsync()
    {
        var fumenFile = await FileDialogHelper.OpenFileAsync(
            DesktopLang.FastOpenOgkrFumen,
            FileDialogHelper.GetSupportFumenOpenFileExtensionFilterList(parserManager));
        if (fumenFile is null)
            return;

        if (!IsSupportedFumenFile(fumenFile.FileName))
        {
            fumenFile.Dispose();
            return;
        }

        await TryOpenAsync(fumenFile);
    }

    public async Task<bool> TryOpenAsync(ISimpleFile file)
    {
        EditorContext context = null;
        var ownershipTransferred = false;
        try
        {
            context = await TryCreateContextAsync(file);
            if (context is null)
                return false;

            var documentName = await FormatOpenFileNameAsync(file);
            var editor = editorProvider.Create();
            try
            {
                if (!await editorProvider.TryOpen(editor, context))
                {
                    await dialogManager.ShowMessageDialog(
                        DesktopLang.CantFastOpenFumen, DialogMessageType.Error);
                    return false;
                }

                if (editor is FumenVisualEditorViewModel viewModel)
                    viewModel.DisplayName = documentName;

                // Shell 会依赖命令服务，不能在 FastOpenService 构造阶段解析；
                // 走到真正打开文档时命令服务已经完成构造，再解析即可。
                await serviceProvider.GetRequiredService<IShell>().OpenDocumentAsync(editor);
                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred && editor is IDisposable disposable)
                    disposable.Dispose();
            }
        }
        catch (Exception exception)
        {
            Log.LogError($"FastOpen failed: {exception}", exception);
            await dialogManager.ShowMessageDialog(
                $"{DesktopLang.CantFastOpenFumen}{exception.Message}", DialogMessageType.Error);
            return false;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                // 上下文已构造时其 Dispose 会级联释放谱面/音频文件；
                // 构造前抛出则谱面文件仍归调用方所有，需要在此释放。
                context?.Dispose();
                if (context is null)
                    file.Dispose();
            }
        }
    }

    private async Task<EditorContext> TryCreateContextAsync(ISimpleFile ogkrFile)
    {
        ISimpleFile audioFile = null;
        ISimpleFile audioAwbFile = null;
        try
        {
            var audioFilePath = string.IsNullOrWhiteSpace(ogkrFile.FullPath)
                ? null
                : await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(
                    ogkrFile.FullPath,
                    audioManager.SupportAudioFileExtensionList
                        .Select(x => x.fileExt.TrimStart('.'))
                        .ToArray());

            if (audioFilePath is null || !File.Exists(audioFilePath))
            {
                audioFile = await FileDialogHelper.OpenFileAsync(
                    DesktopLang.SelectAudioFileManually,
                    audioManager.SupportAudioFileExtensionList);
                if (audioFile is null)
                    return null;
            }
            else
            {
                audioFile = new LocalSimpleFile(audioFilePath);
            }

            var deserializer = parserManager.GetDeserializer(ogkrFile.FileName);
            if (deserializer is null)
            {
                Log.LogError($"FastOpen: no deserializer for {ogkrFile.FileName}.");
                await dialogManager.ShowMessageDialog(
                    DesktopLang.CantFastOpenFumen, DialogMessageType.Error);
                return null;
            }

            await using var fumenStream = await ogkrFile.OpenRead();
            var fumen = await deserializer.DeserializeAsync(fumenStream);

            audioAwbFile = TryResolveExternalAwbFile(audioFile);
            var audioDuration = await CalcAudioDurationAsync(audioFile, audioAwbFile);
            var context = new EditorContext
            {
                ProjectData = new EditorProjectDataModel
                {
                    AudioDuration = audioDuration
                },
                Fumen = fumen,
                FileAccessContext = new EditorFileAccessContext
                {
                    FumenFile = ogkrFile,
                    AudioFile = audioFile,
                    AudioAwbFile = audioAwbFile
                }
            };
            audioFile = null;
            audioAwbFile = null;
            return context;
        }
        finally
        {
            // 未转交进上下文的音频能力在此释放；谱面文件由调用方按所有权规则处理。
            audioAwbFile?.Dispose();
            audioFile?.Dispose();
        }
    }

    private static ISimpleFile TryResolveExternalAwbFile(ISimpleFile audioFile)
    {
        if (!Path.GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
            return null;

        var candidateNames = DesktopFastOpenAudioResolver
            .GetExternalAwbFileNameCandidates(audioFile.FileName);
        var siblingMatches = audioFile.ParentDictionary?.ChildFiles
            .Where(file => candidateNames.Any(candidateName =>
                file.FileName.Equals(candidateName, StringComparison.OrdinalIgnoreCase)))
            .ToArray() ?? [];
        if (siblingMatches.Length > 1)
        {
            throw new InvalidDataException(
                $"Multiple external AWB files were found for '{audioFile.FileName}'.");
        }

        if (siblingMatches.Length == 1)
            return siblingMatches[0];

        if (audioFile.FullPath is not { } localPath)
            return null;

        var externalAwbPath = DesktopFastOpenAudioResolver
            .TryResolveExternalAwbFilePath(localPath);
        return externalAwbPath is null ? null : new LocalSimpleFile(externalAwbPath);
    }

    private async Task<TimeSpan> CalcAudioDurationAsync(
        ISimpleFile audioFile,
        ISimpleFile externalAwbFile)
    {
        await using var audioStream = await audioFile.CopyToNewMemoryStreamAsync();
        using var audio = externalAwbFile is null
            ? await audioManager.LoadAudioAsync(audioStream)
            : await LoadAudioWithExternalAwbAsync(audioStream, externalAwbFile);
        return audio.Duration;
    }

    private async Task<IAudioPlayer> LoadAudioWithExternalAwbAsync(
        Stream audioStream,
        ISimpleFile externalAwbFile)
    {
        await using var externalAwbStream = await externalAwbFile.OpenRead();
        return await audioManager.LoadAudioAsync(audioStream, externalAwbStream);
    }

    private static async Task<string> FormatOpenFileNameAsync(ISimpleFile ogkrFile)
    {
        var result = ogkrFile.FileName;
        if (!string.IsNullOrWhiteSpace(ogkrFile.FullPath))
        {
            var musicXmlFilePath = Path.Combine(
                Path.GetDirectoryName(ogkrFile.FullPath) ?? string.Empty, "Music.xml");
            if (File.Exists(musicXmlFilePath))
            {
                await using var xmlStream = File.OpenRead(musicXmlFilePath);
                var musicXml = await XDocument.LoadAsync(xmlStream, LoadOptions.None, default);
                var element = musicXml.XPathSelectElement(@"//Name[1]/str[1]");
                if (element?.Value is string name)
                    result = name;
            }
        }

        return $"[{DesktopLang.FastOpen}] {result}";
    }

    private bool IsSupportedFumenFile(string fileName)
    {
        return parserManager.GetDeserializer(fileName) is not null;
    }
}
