#nullable enable

using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;

public partial class AwbReplaceConfirmationDialogViewModel : WindowViewModelBase
{
    public AwbReplaceConfirmationDialogViewModel(AwbReplaceCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ExistingAwbDisplayPath = candidate.ExistingAwbFullPath;
        PickedAwbDisplayPath = candidate.PickedAwbFullPath;
        ExistingAwbSizeText = FileHelper.FormatFileSize(candidate.ExistingAwbLength);
        PickedAwbSizeText = FileHelper.FormatFileSize(candidate.PickedAwbLength);
    }

    public string ExistingAwbDisplayPath { get; }

    public string PickedAwbDisplayPath { get; }

    public string ExistingAwbSizeText { get; }

    public string PickedAwbSizeText { get; }

    [RelayCommand]
    private Task ReplaceAsync() => TryCloseAsync(true);

    [RelayCommand]
    private Task CancelAsync() => TryCloseAsync(false);
}
