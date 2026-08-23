using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels.Dialogs;

public partial class BulletPalleteSelectDialogViewModel : WindowViewModelBase
{
    [ObservableProperty]
    private BulletPallete selectedPallete;

    public IEnumerable<BulletPallete> BulletPalleteList { get; }

    public BulletPalleteSelectDialogViewModel(IEnumerable<BulletPallete> list, BulletPallete initSelectedPallete)
    {
        BulletPalleteList = list ?? [];
        SelectedPallete = initSelectedPallete;
    }

    [RelayCommand]
    private async Task SelectAndConfirmAsync(BulletPallete bulletPallete)
    {
        Log.LogInfo($"SelectAndConfirmAsync triggered with {bulletPallete?.StrID}.");
        SelectedPallete = bulletPallete;
        await TryCloseAsync(true);
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        Log.LogInfo($"Bullet pallete select dialog confirmed (selected='{SelectedPallete?.StrID}').");
        await TryCloseAsync(true);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        Log.LogInfo("Bullet pallete select dialog cancelled.");
        await TryCloseAsync(false);
    }
}
