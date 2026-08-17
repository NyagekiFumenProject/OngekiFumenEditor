#nullable enable

using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Parser;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

public abstract partial class FumenVisualEditorProviderBase
{
    private async Task<bool> CreateNewProjectAsync(FumenVisualEditorViewModel editor)
    {
        if (!CanCreateNew)
            return false;

        var picker = CreateSetupFilePicker();
        EditorProjectSetupDialogViewModel? dialog = null;
        try
        {
            using var initialDirectory = await picker.PickProjectDirectoryAsync();
            if (initialDirectory is null)
                return false;

            using var session = new EditorProjectSetupSession(initialDirectory, picker);
            var parserManager = IoC.Get<IFumenParserManager>();
            var audioManager = IoC.Get<IAudioManager>();
            var coordinator = new EditorProjectCreationCoordinator(
                parserManager,
                audioManager,
                (context, cancellationToken) => editor.TryAttachProjectAsync(
                    context,
                    context.ProjectFile?.FileName ?? string.Empty,
                    cancellationToken));
            dialog = new EditorProjectSetupDialogViewModel(
                session,
                parserManager,
                audioManager,
                coordinator,
                message => DialogManager.ShowMessageDialog(message, DialogMessageType.Error));

            var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialog);
            if (result != true)
                return false;

            if (editor.EditorContext is { } projectContext)
            {
                await TryStoreRecentFromContextAsync(
                    projectContext,
                    projectContext.ProjectName,
                    projectContext.LocationDescription);
            }
            return true;
        }
        catch (Exception exception)
        {
            Log.LogError($"Failed to create a project: {exception.Message}");
            await DialogManager.ShowMessageDialog(
                $"Unable to create the project: {exception.Message}",
                DialogMessageType.Error);
            return false;
        }
        finally
        {
            dialog?.Dispose();
        }
    }
}
