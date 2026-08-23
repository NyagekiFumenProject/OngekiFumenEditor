using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Gekimini.Avalonia.Framework.Dialogs;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public partial class FileInfoTypeUIViewModel : CommonUIViewModelBase<ISimpleFile>
{
    public ISimpleFile File
    {
        get => TypedProxyValue;
        set
        {
            TypedProxyValue = value;
            OnPropertyChanged(nameof(File));
        }
    }

    public FileInfoTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }

    [RelayCommand]
    private Task SelectFileAsync()
    {
        Log.LogInfo("SelectFileAsync triggered (SVG prefab import currently disabled).");
        // SVG prefab file selection/import is temporarily disabled with the prefab feature.
        return Task.CompletedTask;

        /*
        var projectRoot = IoC.Get<IEditorDocumentManager>()
            .CurrentActivatedEditor?
            .EditorProjectData?
            .ProjectRoot;
        if (projectRoot is null)
        {
            await IoC.Get<IDialogManager>().ShowMessageDialog(
                "Save or open a project folder before importing an SVG.",
                DialogMessageType.Error);
            return;
        }

        var selectedFile = await FileDialogHelper.OpenFileAsync(Lang.SelectSvgFile, [(".svg", "SVG")]);
        if (selectedFile is null)
            return;

        ISimpleFile importedFile = null;
        try
        {
            importedFile = await SvgProjectFileImporter.ImportAsync(projectRoot, selectedFile);
            File = importedFile;
            importedFile = null;
        }
        catch (Exception exception)
        {
            await IoC.Get<IDialogManager>().ShowMessageDialog(
                $"Unable to import SVG: {exception.Message}",
                DialogMessageType.Error);
        }
        finally
        {
            importedFile?.Dispose();
            selectedFile.Dispose();
        }
        */
    }
}

