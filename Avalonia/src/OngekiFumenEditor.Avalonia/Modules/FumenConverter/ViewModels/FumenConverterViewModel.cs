using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.ViewModels;

[RegisterSingleton<IFumenConverterWindow>]
public partial class FumenConverterViewModel : WindowViewModelBase, IFumenConverterWindow
{
    [ObservableProperty]
    public partial string InputFumenFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputFumenFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUseInputFile { get; set; } = true;

    public bool IsCurrentEditorAsInputFumen
    {
        get => !IsUseInputFile;
        set => IsUseInputFile = !value;
    }

    public string CurrentEditorName => IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.DisplayName;

    public FumenConverterViewModel()
    {
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentEditorName));
            OnPropertyChanged(nameof(IsCurrentEditorAsInputFumen));
        };
    }

    partial void OnIsUseInputFileChanged(bool value)
    {
        if (!value && CurrentEditorName is null)
        {
            IsUseInputFile = true;
            return;
        }

        OnPropertyChanged(nameof(IsCurrentEditorAsInputFumen));
    }

    public async void OnOpenSelectInputFileDialog()
    {
        var path = await FileDialogHelper.OpenFileAsync(string.Empty,
            FileDialogHelper.GetSupportFumenFileExtensionFilterList());
        if (string.IsNullOrWhiteSpace(path))
            return;

        InputFumenFilePath = path;
        IsUseInputFile = true;
    }

    public async void OnOpenSelectOutputFileDialog()
    {
        var path = await FileDialogHelper.SaveFileAsync(string.Empty,
            FileDialogHelper.GetSupportFumenFileExtensionFilterList());
        if (string.IsNullOrWhiteSpace(path))
            return;

        OutputFumenFilePath = path;
    }

    public async void OnExecuteConverter()
    {
        var option = new FumenConvertOption
        {
            InputFumenFilePath = IsUseInputFile ? InputFumenFilePath : string.Empty,
            OutputFumenFilePath = OutputFumenFilePath
        };

        OngekiFumen input = null;

        if (!IsUseInputFile)
        {
            var editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
            if (editor is not null)
            {
                input = editor.Fumen;
            }
            else
            {
                await IoC.Get<IDialogManager>().ShowMessageDialog(Lang.NoEditorTarget);
                return;
            }
        }

        var dialogManager = IoC.Get<IDialogManager>();
        try
        {
            var result = await FumenConverterWrapper.Generate(option, input);
            await dialogManager.ShowMessageDialog(result.IsSuccess
                    ? Lang.ConvertSuccess
                    : $"{Lang.ConvertFail} {result.Message}",
                result.IsSuccess ? DialogMessageType.Info : DialogMessageType.Error);
        }
        catch (Exception e)
        {
            Log.LogError("Fumen conversion failed.", e);
            await dialogManager.ShowMessageDialog($"{Lang.ConvertFail} {e.Message}", DialogMessageType.Error);
        }
    }
}
