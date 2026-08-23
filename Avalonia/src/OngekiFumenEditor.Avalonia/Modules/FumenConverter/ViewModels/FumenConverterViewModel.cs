using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Microsoft.Extensions.Logging;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.ViewModels;

[RegisterSingleton<IFumenConverterWindow>]
public partial class FumenConverterViewModel : WindowViewModelBase, IFumenConverterWindow, IDisposable
{
    private readonly IEditorDocumentManager editorDocumentManager;
    private ISimpleFile inputFumenFile;
    private ISimpleFile outputFumenFile;
    private readonly ILogger<FumenConverterViewModel> logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteConverterCommand))]
    public partial string InputFumenFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteConverterCommand))]
    public partial string OutputFumenFilePath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteConverterCommand))]
    public partial bool IsUseInputFile { get; set; } = true;

    public bool IsCurrentEditorAsInputFumen
    {
        get => !IsUseInputFile;
        set => IsUseInputFile = !value;
    }

    public string CurrentEditorName => editorDocumentManager?.CurrentActivatedEditor?.DisplayName;

    public FumenConverterViewModel(ILogger<FumenConverterViewModel> logger)
    {
        this.logger = logger;

        editorDocumentManager = IoC.Get<IEditorDocumentManager>();
        editorDocumentManager.OnActivateEditorChanged += OnActivateEditorChanged;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        OnPropertyChanged(nameof(CurrentEditorName));
        OnPropertyChanged(nameof(IsCurrentEditorAsInputFumen));
        ExecuteConverterCommand.NotifyCanExecuteChanged();
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

    [RelayCommand]
    private async Task OpenSelectInputFileAsync()
    {
        logger.LogInformation("OpenSelectInputFile triggered.");
        var file = await FileDialogHelper.OpenFileAsync(string.Empty,
            FileDialogHelper.GetSupportFumenFileExtensionFilterList());
        if (file is null)
            return;

        inputFumenFile?.Dispose();
        inputFumenFile = file;
        InputFumenFilePath = file.FullPath;
        IsUseInputFile = true;
    }

    [RelayCommand]
    private async Task OpenSelectOutputFileAsync()
    {
        logger.LogInformation("OpenSelectOutputFile triggered.");
        var file = await FileDialogHelper.SaveFileAsync(string.Empty,
            FileDialogHelper.GetSupportFumenFileExtensionFilterList());
        if (file is null)
            return;

        outputFumenFile?.Dispose();
        outputFumenFile = file;
        OutputFumenFilePath = file.FullPath;
    }

    private bool CanExecuteConverter() =>
        outputFumenFile is not null &&
        (IsUseInputFile ? inputFumenFile is not null : CurrentEditorName is not null);

    [RelayCommand(CanExecute = nameof(CanExecuteConverter))]
    private async Task ExecuteConverterAsync()
    {
        logger.LogInformation("ExecuteConverter triggered.");
        var option = new FumenConvertOption
        {
            InputFumenFile = IsUseInputFile ? inputFumenFile : null,
            OutputFumenFile = outputFumenFile
        };

        OngekiFumen input = null;

        if (!IsUseInputFile)
        {
            var editor = editorDocumentManager.CurrentActivatedEditor;
            if (editor is not null)
            {
                input = editor.EditorContext.Fumen;
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

    public void Dispose()
    {
        editorDocumentManager.OnActivateEditorChanged -= OnActivateEditorChanged;
        inputFumenFile?.Dispose();
        outputFumenFile?.Dispose();
        inputFumenFile = null;
        outputFumenFile = null;
        GC.SuppressFinalize(this);
    }
}
