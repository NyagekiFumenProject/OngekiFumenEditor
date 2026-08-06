#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;

public partial class ProjectFileSelectionDialogViewModel : WindowViewModelBase
{
    public ProjectFileSelectionDialogViewModel(IEnumerable<string> projectLocators)
    {
        ArgumentNullException.ThrowIfNull(projectLocators);
        ProjectLocators = projectLocators.ToArray();
    }

    public IReadOnlyList<string> ProjectLocators { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? SelectedProjectLocator { get; set; }

    private bool CanConfirm() => !string.IsNullOrWhiteSpace(SelectedProjectLocator);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private Task ConfirmAsync() => TryCloseAsync(true);

    [RelayCommand]
    private Task CancelAsync() => TryCloseAsync(false);
}
