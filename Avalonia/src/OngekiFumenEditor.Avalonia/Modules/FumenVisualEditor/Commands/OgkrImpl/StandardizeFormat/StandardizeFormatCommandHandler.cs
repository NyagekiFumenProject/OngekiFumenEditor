using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat;

[RegisterSingleton<ICommandHandler>]
public partial class StandardizeFormatCommandHandler : CommandHandlerBase<StandardizeFormatCommandDefinition>
{
    private readonly IEditorDocumentManager editorDocumentManager;
    private readonly IDialogManager dialogManager;
    private readonly IFumenConvertService convertService;
    private readonly IStandardizeFormatOutputService outputService;

    public StandardizeFormatCommandHandler(
        IEditorDocumentManager editorDocumentManager,
        IDialogManager dialogManager,
        IFumenConvertService convertService,
        IStandardizeFormatOutputService outputService)
    {
        this.editorDocumentManager = editorDocumentManager;
        this.dialogManager = dialogManager;
        this.convertService = convertService;
        this.outputService = outputService;
    }

    public override Task Update(Command command)
    {
        command.Enabled = editorDocumentManager.CurrentActivatedEditor?.EditorContext?.Fumen is not null;
        return Task.CompletedTask;
    }

    public override async Task Run(Command command)
    {
        if (editorDocumentManager.CurrentActivatedEditor is not { EditorContext.Fumen: not null } editor)
            return;
        var fumen = editor.EditorContext.Fumen;

        ISimpleFile outputFile;
        try
        {
            outputFile = await outputService.PickOutputFileAsync();
        }
        catch (Exception exception)
        {
            Log.LogError("Selecting a standardization output file failed.", exception);
            await dialogManager.ShowMessageDialog($"{Lang.ConvertFail} {exception.Message}", DialogMessageType.Error);
            return;
        }

        if (outputFile is null)
            return;
        using var outputFileScope = outputFile;

        FumenConverterWrapper.GenerateResult result = null;
        Exception failure = null;
        editor.LockAllUserInteraction();
        try
        {
            result = await convertService.GenerateAsync(
                new FumenConvertOption
                {
                    OutputFumenFile = outputFile,
                    IsStandarizeFumen = true
                },
                fumen);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            editor.UnlockAllUserInteraction();
        }

        if (failure is not null)
        {
            Log.LogError("Standardizing the current fumen failed.", failure);
            await dialogManager.ShowMessageDialog($"{Lang.ConvertFail} {failure.Message}", DialogMessageType.Error);
            return;
        }

        if (result is not { IsSuccess: true })
        {
            var message = string.IsNullOrWhiteSpace(result?.Message) ? Lang.ConvertFail : result.Message;
            await dialogManager.ShowMessageDialog(message, DialogMessageType.Error);
            return;
        }

        if (!outputService.CanRevealOutputDirectory(outputFile))
        {
            await dialogManager.ShowMessageDialog(Lang.ConvertSuccess);
            return;
        }

        if (!await dialogManager.ShowComfirmDialog(Lang.NewFumenFileSaveDone, Lang.StandardizeFormat))
            return;

        try
        {
            if (await outputService.RevealOutputDirectoryAsync(outputFile))
                return;
        }
        catch (Exception exception)
        {
            Log.LogError("Opening the standardization output directory failed.", exception);
        }

        await dialogManager.ShowMessageDialog(Lang.OpenOutputFolderFailed, DialogMessageType.Error);
    }
}
